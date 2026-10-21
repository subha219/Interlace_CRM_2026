const App = {
    setup() {
        const token = new URLSearchParams(location.search).get('token') || '';
        const state = Vue.reactive({ mode: 'loading', number: '', title: '', score: 0, comment: '', busy: false, error: '' });
        const submit = async () => {
            state.busy = true; state.error = '';
            try {
                await axios.post('/api/Public/SubmitCsat', { token, score: state.score, comment: state.comment });
                state.mode = 'done';
            } catch (e) {
                if (e.response && e.response.status === 404) state.mode = 'invalid';
                else state.error = e.response?.data?.message || 'Could not send your feedback. Please try again.';
            } finally { state.busy = false; }
        };
        Vue.onMounted(async () => {
            try {
                const c = (await axios.get('/api/Public/GetCsatSurvey', { params: { token } })).data.content;
                state.number = c.number; state.title = c.title;
                state.mode = c.alreadyAnswered ? 'done' : 'form';
            } catch (e) { state.mode = 'invalid'; }
        });
        return { state, submit };
    }
};
Vue.createApp(App).mount('#app');
