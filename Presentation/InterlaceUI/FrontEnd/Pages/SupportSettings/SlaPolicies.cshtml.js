const App = {
    setup() {
        const state = Vue.reactive({ policies: [], users: [] });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const load = async () => {
            try {
                state.policies = (await AxiosManager.get('/SupportInsight/GetSla', {})).data.content.data || [];
                state.users = (await AxiosManager.get('/Activity/GetAssignees', {})).data.content || [];
            } catch (e) { fail(e, 'Could not load the SLA targets'); }
        };
        const save = async () => {
            try {
                await AxiosManager.post('/SupportInsight/SaveSla', { policies: state.policies.map(p => ({ priority: p.priority, firstResponseHours: Number(p.firstResponseHours) || 0, resolutionHours: Number(p.resolutionHours) || 0, escalationUserId: p.escalationUserId || null })) });
                Swal.fire({ icon: 'success', title: 'Saved', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not save'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['SupportSettings']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, save };
    }
};
Vue.createApp(App).mount('#app');
