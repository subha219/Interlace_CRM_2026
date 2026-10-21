const App = {
    setup() {
        const now = new Date();
        const state = Vue.reactive({
            period: 'Month', number: now.getMonth() + 1, year: now.getFullYear(), label: '', rows: [], stages: [],
            editing: false, users: [], targetUser: '', months: Array(12).fill(0)
        });
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        const money = (v) => (v || 0).toLocaleString(undefined, { maximumFractionDigits: 0 });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const load = async () => {
            if (state.period === 'Quarter' && state.number > 4) state.number = 1;
            try {
                const r = await AxiosManager.get(`/SalesTarget/GetLeaderboard?year=${state.year}&period=${state.period}&number=${state.number}`, {});
                state.label = r.data.content.label;
                state.rows = r.data.content.rows || [];
            } catch (e) { fail(e, 'Could not load the leaderboard'); }
        };
        const loadStages = async () => {
            try { state.stages = (await AxiosManager.get('/SalesTarget/GetStageVelocity', {})).data.content.stages || []; } catch (e) { /* optional */ }
        };
        const loadUsers = async () => {
            try { state.users = (await AxiosManager.get('/Activity/GetAssignees', {})).data.content || []; } catch (e) { /* optional */ }
        };
        const loadTargets = async () => {
            if (!state.targetUser) return;
            try {
                const r = await AxiosManager.get(`/SalesTarget/GetTargets?userId=${encodeURIComponent(state.targetUser)}&year=${state.year}`, {});
                state.months = r.data.content.months || Array(12).fill(0);
            } catch (e) { fail(e, 'Could not load targets'); }
        };
        const saveTargets = async () => {
            try {
                await AxiosManager.post('/SalesTarget/SaveTargets', { userId: state.targetUser, year: state.year, months: state.months.map(v => Number(v) || 0) });
                Swal.fire({ icon: 'success', title: 'Targets saved', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not save targets'); }
        };
        const fillYear = () => { const v = Number(state.months[0]) || 0; state.months = Array(12).fill(v); };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['SalesTargets']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await Promise.all([load(), loadStages(), loadUsers()]);
        });

        return { state, monthNames, money, load, loadTargets, saveTargets, fillYear };
    }
};

Vue.createApp(App).mount('#app');
