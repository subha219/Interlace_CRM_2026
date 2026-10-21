const App = {
    setup() {
        const blank = () => ({ id: null, name: '', description: '', defaultOwnerUserId: null, defaultPriority: null, isActive: true });
        const state = Vue.reactive({ rows: [], users: [], form: blank() });
        const priorities = ['Critical', 'High', 'Normal', 'Low'];
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const userName = (id) => (state.users.find(u => u.id === id) || {}).name || '-';
        const load = async () => {
            try {
                state.rows = (await AxiosManager.get('/Knowledge/GetCategories', {})).data.content.data || [];
                state.users = (await AxiosManager.get('/Activity/GetAssignees', {})).data.content || [];
            } catch (e) { fail(e, 'Could not load the categories'); }
        };
        const reset = () => { state.form = blank(); };
        const edit = (c) => { state.form = { id: c.id, name: c.name, description: c.description || '', defaultOwnerUserId: c.defaultOwnerUserId || null, defaultPriority: c.defaultPriority || null, isActive: c.isActive }; };
        const save = async () => {
            try { await AxiosManager.post('/Knowledge/SaveCategory', state.form); reset(); await load(); }
            catch (e) { fail(e, 'Could not save'); }
        };
        const remove = async (c) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Delete "' + c.name + '"?', text: 'Cases keep working; they just lose this category name.', showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Knowledge/DeleteCategory', { id: c.id }); await load(); } catch (e) { fail(e, 'Could not delete'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['SupportSettings']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, priorities, userName, save, edit, reset, remove };
    }
};
Vue.createApp(App).mount('#app');
