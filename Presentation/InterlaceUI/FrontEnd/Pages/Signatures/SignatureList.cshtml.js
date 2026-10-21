const App = {
    setup() {
        const state = Vue.reactive({ rows: [], open: null });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const iso = (v) => v ? new Date(String(v).endsWith('Z') ? v : v + 'Z') : null;
        const day = (v) => v ? iso(v).toLocaleDateString() : '-';
        const when = (v) => v ? iso(v).toLocaleString() : '-';
        const badge = (s) => s === 'Signed' ? 'bg-success' : s === 'Declined' ? 'bg-danger' : s === 'Expired' || s === 'Cancelled' ? 'bg-secondary' : 'bg-info';
        const load = async () => {
            try { state.rows = (await AxiosManager.get('/Signature/GetRequests', {})).data.content.data || []; }
            catch (e) { fail(e, 'Could not load the requests'); }
        };
        const cancel = async (r) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Cancel ' + r.number + '?', text: 'The signer will no longer be able to sign.', showCancelButton: true, confirmButtonText: 'Cancel request' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Signature/Cancel', { id: r.id }); await load(); } catch (e) { fail(e, 'Could not cancel'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Signatures']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, day, when, badge, cancel };
    }
};
Vue.createApp(App).mount('#app');
