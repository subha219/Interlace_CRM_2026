const App = {
    setup() {
        const state = Vue.reactive({ items: [], threshold: 10, notes: {} });
        const money = (v) => (v || 0).toLocaleString(undefined, { maximumFractionDigits: 2 });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const load = async () => {
            try {
                const c = (await AxiosManager.get('/QuotationApproval/ListPending', {})).data.content;
                state.items = c.data || [];
                state.threshold = c.threshold;
            } catch (e) { fail(e, 'Could not load approvals'); }
        };
        const savePolicy = async () => {
            try {
                await AxiosManager.post('/QuotationApproval/SavePolicy', { discountApprovalThresholdPercent: Number(state.threshold) || 0 });
                Swal.fire({ icon: 'success', title: 'Policy saved', timer: 1200, showConfirmButton: false });
            } catch (e) { fail(e, 'Could not save the policy'); }
        };
        const decide = async (q, approve) => {
            try {
                await AxiosManager.post('/QuotationApproval/Decide', { quotationId: q.id, approve, note: state.notes[q.id] || '' });
                await load();
            } catch (e) { fail(e, approve ? 'Could not approve' : 'Could not reject'); }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['QuotationApprovals']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
        });

        return { state, money, savePolicy, decide, load };
    }
};

Vue.createApp(App).mount('#app');
