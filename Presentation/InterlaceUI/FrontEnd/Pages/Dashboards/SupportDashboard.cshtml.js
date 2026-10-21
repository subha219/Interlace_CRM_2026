const App = {
    setup() {
        const state = Vue.reactive({ d: null, loading: true });
        const load = async () => {
            state.loading = true;
            try { state.d = (await AxiosManager.get('/SupportInsight/GetDashboard', {})).data.content; }
            catch (e) { Swal.fire({ icon: 'error', title: 'Could not load the dashboard', text: e.response?.data?.message || 'Please try again.' }); }
            finally { state.loading = false; }
        };
        const kpis = Vue.computed(() => {
            const d = state.d; if (!d) return [];
            return [
                { label: 'Open cases', value: d.open, sub: d.dueIn24Hours + ' due in the next 24 h' },
                { label: 'SLA breached', value: d.breached, sub: d.unanswered + ' without a first response', bad: d.breached > 0 },
                { label: 'SLA compliance (30 days)', value: d.slaCompliancePercent + '%', sub: d.resolvedLast30Days + ' resolved' },
                { label: 'Customer satisfaction', value: d.csatCount ? d.csatAverage.toFixed(2) + ' / 5' : '-', sub: d.csatCount + ' answers' },
                { label: 'Avg first response', value: d.avgFirstResponseHours + ' h', sub: 'last 30 days' },
                { label: 'Avg resolution', value: d.avgResolutionHours + ' h', sub: 'last 30 days' }
            ];
        });
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Dashboards']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, kpis, load };
    }
};
Vue.createApp(App).mount('#app');
