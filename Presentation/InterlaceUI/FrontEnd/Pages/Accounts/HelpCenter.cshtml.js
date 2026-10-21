const App = {
    setup() {
        const state = Vue.reactive({ q: '', rows: [], loaded: false, article: null, rated: false });
        const search = async () => {
            try { state.rows = (await axios.get('/api/Public/SearchHelp', { params: { q: state.q } })).data.content.data || []; }
            catch (e) { state.rows = []; }
            state.loaded = true;
        };
        const open = async (id) => {
            try { state.article = (await axios.get('/api/Public/GetHelpArticle', { params: { id } })).data.content; state.rated = false; }
            catch (e) { state.article = null; }
        };
        const back = () => { state.article = null; };
        const rate = async (helpful) => {
            try { await axios.post('/api/Public/RateHelpArticle', { id: state.article.id, helpful }); } catch (e) { /* ignore */ }
            state.rated = true;
        };
        Vue.onMounted(search);
        return { state, search, open, back, rate };
    }
};
Vue.createApp(App).mount('#app');
