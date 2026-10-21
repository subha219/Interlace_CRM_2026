const App = {
    setup() {
        const state = Vue.reactive({ d: null, customers: [], filter: '', form: null });
        const filters = [{ key: '', label: 'All' }, { key: 'Active', label: 'Active' }, { key: 'Expiring', label: 'Due for renewal' }, { key: 'Expired', label: 'Expired' }, { key: 'Draft', label: 'Draft' }];
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const money = (v) => v === null || v === undefined ? '-' : Number(v).toLocaleString(undefined, { maximumFractionDigits: 2 });
        const day = (v) => v ? String(v).substring(0, 10) : '-';
        const badge = (s) => s === 'Active' ? 'bg-success' : s === 'Expired' ? 'bg-danger' : s === 'Renewed' ? 'bg-info' : 'bg-secondary';
        const rows = Vue.computed(() => state.d ? state.d.data : []);
        const load = async () => {
            try { state.d = (await AxiosManager.get('/Contract/GetContracts?filter=' + encodeURIComponent(state.filter), {})).data.content; }
            catch (e) { fail(e, 'Could not load the contracts'); }
        };
        const setFilter = async (k) => { state.filter = k; await load(); };
        const add = () => { state.form = { id: null, title: '', customerId: null, status: 'Draft', billingCycle: null, startDate: '', endDate: '', value: null, renewalNoticeDays: 60, autoRenew: false, notes: '' }; };
        const edit = (c) => { state.form = { id: c.id, title: c.title, customerId: c.customerId, status: c.status, billingCycle: c.billingCycle, startDate: day(c.startDate) === '-' ? '' : day(c.startDate), endDate: day(c.endDate) === '-' ? '' : day(c.endDate), value: c.value, renewalNoticeDays: c.renewalNoticeDays, autoRenew: c.autoRenew, notes: c.notes || '' }; window.scrollTo({ top: 0, behavior: 'smooth' }); };
        const save = async () => {
            const f = state.form;
            try { await AxiosManager.post('/Contract/SaveContract', { ...f, startDate: f.startDate || null, endDate: f.endDate || null, value: f.value === '' ? null : f.value }); state.form = null; await load(); }
            catch (e) { fail(e, 'Could not save'); }
        };
        const renew = async (c) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Renew ' + c.number + '?', html: 'A new contract starts the day after this one ends, for the same length. Optionally enter a new value:', input: 'number', inputValue: c.value ?? '', showCancelButton: true, confirmButtonText: 'Renew' });
            if (!ask.isConfirmed) return;
            try {
                const r = (await AxiosManager.post('/Contract/RenewContract', { id: c.id, value: ask.value === '' ? null : Number(ask.value) })).data.content;
                Swal.fire({ icon: 'success', title: 'Renewed as ' + r.number, timer: 1500, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not renew'); }
        };
        const sign = async (c) => {
            const ask = await Swal.fire({
                title: 'Send ' + c.number + ' for signature', showCancelButton: true, confirmButtonText: 'Send',
                html: '<input id="sg-name" class="swal2-input" maxlength="200" placeholder="Signer full name"><input id="sg-mail" type="email" class="swal2-input" maxlength="255" placeholder="Signer e-mail">',
                preConfirm: () => {
                    const name = document.getElementById('sg-name').value.trim(), mail = document.getElementById('sg-mail').value.trim();
                    if (!name || !mail) { Swal.showValidationMessage('Enter the signer name and e-mail.'); return false; }
                    return { name, mail };
                }
            });
            if (!ask.isConfirmed) return;
            try {
                const r = (await AxiosManager.post('/Signature/Create', { relatedType: 'Contract', relatedId: c.id, signerName: ask.value.name, signerEmail: ask.value.mail, expiryDays: 14 })).data.content;
                Swal.fire({ icon: 'success', title: 'Sent as ' + r.number, text: 'The signer has been e-mailed a link.', timer: 2200, showConfirmButton: false });
            } catch (e) { fail(e, 'Could not send for signature'); }
        };
        const remove = async (c) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Delete ' + c.number + '?', showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Contract/DeleteContract', { id: c.id }); await load(); } catch (e) { fail(e, 'Could not delete'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Contracts']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            try { state.customers = (await AxiosManager.get('/Customer/GetCustomerList', {})).data.content.data || []; } catch (e) { /* the form just has no customers */ }
            await load();
        });
        return { state, filters, rows, money, day, badge, setFilter, add, edit, save, renew, sign, remove };
    }
};
Vue.createApp(App).mount('#app');
