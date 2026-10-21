const App = {
    setup() {
        const state = Vue.reactive({ loading: true, score: 0, checks: [], open: '' });

        const color = (p) => p >= 90 ? '#16a34a' : p >= 70 ? '#d97706' : '#dc2626';

        const load = async () => {
            state.loading = true;
            try {
                const r = await AxiosManager.get('/DataQuality/GetDataQuality', {});
                state.score = r.data.content.score;
                state.checks = r.data.content.checks || [];
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Could not check the data', text: e.response?.data?.message || 'Please try again.' });
            } finally { state.loading = false; }
        };

        const groups = Vue.computed(() => {
            const map = {};
            state.checks.forEach(c => { (map[c.entity] = map[c.entity] || { entity: c.entity, checks: [] }).checks.push(c); });
            return Object.values(map);
        });

        const assign = async (c) => {
            const entity = c.entity === 'Leads' ? 'Lead' : 'Customer';
            const ask = await Swal.fire({ icon: 'question', title: 'Assign to you?', text: `All ${c.count} ${c.entity.toLowerCase()} without an owner become yours. You can reassign them later.`, showCancelButton: true, confirmButtonText: 'Assign' });
            if (!ask.isConfirmed) return;
            try {
                const r = await AxiosManager.post('/DataQuality/AssignOwner', { entity, ownerUserId: StorageManager.getUserId() });
                Swal.fire({ icon: 'success', title: `${r.data.content.assigned} assigned`, timer: 1500, showConfirmButton: false });
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Could not assign', text: e.response?.data?.message || 'You may not have access.' });
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Dashboards']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
        });

        return { state, groups, color, load, assign };
    }
};

Vue.createApp(App).mount('#app');
