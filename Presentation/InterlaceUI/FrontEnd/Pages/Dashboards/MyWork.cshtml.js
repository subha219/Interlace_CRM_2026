const App = {
    setup() {
        const state = Vue.reactive({ loading: true, sections: [] });

        const money = (v) => (v || 0).toLocaleString(undefined, { maximumFractionDigits: 0 });
        const day = (d) => d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '';
        const withItems = Vue.computed(() => state.sections.filter(s => s.items && s.items.length));

        const load = async () => {
            state.loading = true;
            try {
                const r = await AxiosManager.get('/MyWork/GetMyWork', {});
                state.sections = r.data.content.sections || [];
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Could not load your work', text: e.response?.data?.message || 'Please try again.' });
            } finally { state.loading = false; }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Dashboards']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
        });

        return { state, withItems, money, day, load };
    }
};

Vue.createApp(App).mount('#app');
