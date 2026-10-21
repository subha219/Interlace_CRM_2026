const App = {
    setup() {
        const state = Vue.reactive({ d: null, loading: true, band: '' });
        const money = (v) => Number(v || 0).toLocaleString(undefined, { maximumFractionDigits: 0 });
        const rows = Vue.computed(() => (state.d ? state.d.data : []).filter(r => !state.band || r.band === state.band));
        const load = async () => {
            state.loading = true;
            try { state.d = (await AxiosManager.get('/Contract/GetHealth', {})).data.content; }
            catch (e) { Swal.fire({ icon: 'error', title: 'Could not load customer health', text: e.response?.data?.message || 'Please try again.' }); }
            finally { state.loading = false; }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Dashboards']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, rows, money, load };
    }
};
Vue.createApp(App).mount('#app');
