const App = {
    setup() {
        const state = Vue.reactive({ rows: [], form: null });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const load = async () => {
            try { state.rows = (await AxiosManager.get('/Messaging/GetTemplates', {})).data.content.data || []; }
            catch (e) { fail(e, 'Could not load the templates'); }
        };
        const add = () => { state.form = { id: null, name: '', channel: 'WhatsApp', providerTemplateId: '', language: 'en', body: '', variables: '', isActive: true }; };
        const edit = (t) => { state.form = { ...t, language: t.language || '', variables: t.variables || '' }; window.scrollTo({ top: 0, behavior: 'smooth' }); };
        const save = async () => {
            try { await AxiosManager.post('/Messaging/SaveTemplate', state.form); state.form = null; await load(); }
            catch (e) { fail(e, 'Could not save'); }
        };
        const remove = async (t) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Delete "' + t.name + '"?', showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Messaging/DeleteTemplate', { id: t.id }); await load(); } catch (e) { fail(e, 'Could not delete'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Messaging']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, add, edit, save, remove };
    }
};
Vue.createApp(App).mount('#app');
