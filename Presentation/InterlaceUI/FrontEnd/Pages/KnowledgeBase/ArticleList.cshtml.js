const App = {
    setup() {
        const state = Vue.reactive({ rows: [], categories: [], q: '', form: null });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const catName = (id) => (state.categories.find(c => c.id === id) || {}).name || '-';
        const search = async () => {
            try { state.rows = (await AxiosManager.get('/Knowledge/Search?query=' + encodeURIComponent(state.q || ''), {})).data.content.data || []; }
            catch (e) { fail(e, 'Could not load the articles'); }
        };
        const add = () => { state.form = { id: null, title: '', summary: '', body: '', categoryId: null, tags: '', isPublished: false }; };
        const edit = async (a) => {
            try {
                const x = (await AxiosManager.get('/Knowledge/GetArticle?id=' + encodeURIComponent(a.id), {})).data.content;
                state.form = { id: x.id, title: x.title, summary: x.summary || '', body: x.body || '', categoryId: x.categoryId || null, tags: x.tags || '', isPublished: x.isPublished };
                window.scrollTo({ top: 0, behavior: 'smooth' });
            } catch (e) { fail(e, 'Could not open the article'); }
        };
        const save = async () => {
            try { await AxiosManager.post('/Knowledge/SaveArticle', state.form); state.form = null; await search(); }
            catch (e) { fail(e, 'Could not save'); }
        };
        const remove = async (a) => {
            const ask = await Swal.fire({ icon: 'question', title: 'Delete "' + a.title + '"?', showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Knowledge/DeleteArticle', { id: a.id }); await search(); } catch (e) { fail(e, 'Could not delete'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['KnowledgeBase']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            try { state.categories = (await AxiosManager.get('/Knowledge/GetCategories', {})).data.content.data || []; } catch (e) { /* categories are optional here */ }
            await search();
        });
        return { state, catName, search, add, edit, save, remove };
    }
};
Vue.createApp(App).mount('#app');
