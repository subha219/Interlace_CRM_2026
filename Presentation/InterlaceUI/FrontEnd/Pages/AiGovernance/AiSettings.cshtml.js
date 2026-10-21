const App = {
    setup() {
        const state = Vue.reactive({ d: null });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const load = async () => {
            try { state.d = (await AxiosManager.get('/Ai/GetGovernance', {})).data.content; }
            catch (e) { fail(e, 'Could not load AI governance'); }
        };
        const save = async () => {
            const s = state.d.settings;
            try {
                await AxiosManager.post('/Ai/SaveSettings', { enabled: s.enabled, dailyLimitPerUser: Number(s.dailyLimitPerUser) || 0, monthlyLimitTotal: Number(s.monthlyLimitTotal) || 0 });
                Swal.fire({ icon: 'success', title: 'Saved', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not save'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['AiGovernance']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, save };
    }
};
Vue.createApp(App).mount('#app');
