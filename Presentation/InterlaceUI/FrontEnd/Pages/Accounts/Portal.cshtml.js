const App = {
    setup() {
        const KEY = 'portalSession';
        const state = Vue.reactive({ mode: 'busy', email: '', working: false, error: '', data: null, tab: 'cases', cs: null, reply: '', form: { title: '', description: '', priority: 'Normal' } });
        const tabs = [{ key: 'cases', label: 'My cases' }, { key: 'new', label: 'New case' }, { key: 'invoices', label: 'Invoices' }, { key: 'contracts', label: 'Contracts' }];
        const token = () => { try { return sessionStorage.getItem(KEY) || ''; } catch (e) { return ''; } };
        const setToken = (t) => { try { if (t) sessionStorage.setItem(KEY, t); else sessionStorage.removeItem(KEY); } catch (e) { /* storage blocked */ } };
        const api = (method, url, body) => axios({ method, url: '/api/Portal/' + url, data: body, headers: { 'X-Portal-Token': token() } }).then(r => r.data.content);
        const msg = (e, d) => (e.response && e.response.data && (e.response.data.message || e.response.data.title)) || d;
        const day = (v) => v ? String(v).substring(0, 10) : '-';
        const money = (v) => v === null || v === undefined ? '-' : Number(v).toLocaleString(undefined, { maximumFractionDigits: 2 });

        const load = async () => {
            try { state.data = await api('get', 'Overview'); state.mode = 'home'; }
            catch (e) { setToken(''); state.mode = 'login'; }
        };
        const requestLink = async () => {
            state.working = true; state.error = '';
            try { await axios.post('/api/Portal/RequestLogin', { email: state.email }); state.mode = 'sent'; }
            catch (e) { state.error = msg(e, 'Could not send the link. Please try again.'); }
            finally { state.working = false; }
        };
        const openCase = async (id) => {
            state.error = ''; state.reply = '';
            try { state.cs = await api('get', 'Case?id=' + encodeURIComponent(id)); state.mode = 'case'; }
            catch (e) { state.error = msg(e, 'Could not open the case.'); }
        };
        const back = async () => { state.cs = null; await load(); state.tab = 'cases'; };
        const sendReply = async () => {
            state.working = true; state.error = '';
            try { await api('post', 'AddComment', { caseId: state.cs.case.id, text: state.reply }); await openCase(state.cs.case.id); }
            catch (e) { state.error = msg(e, 'Could not send your message.'); }
            finally { state.working = false; }
        };
        const submitCase = async () => {
            state.working = true; state.error = '';
            try {
                const r = await api('post', 'OpenCase', state.form);
                state.form = { title: '', description: '', priority: 'Normal' };
                await load(); state.tab = 'cases';
                Swal.fire({ icon: 'success', title: 'Case ' + r.number + ' opened', timer: 1500, showConfirmButton: false });
            } catch (e) { state.error = msg(e, 'Could not open the case.'); }
            finally { state.working = false; }
        };
        const logout = async () => { try { await api('post', 'Logout', {}); } catch (e) { /* ignore */ } setToken(''); state.data = null; state.mode = 'login'; };

        Vue.onMounted(async () => {
            const login = new URLSearchParams(location.search).get('login');
            if (login) {
                try {
                    const r = (await axios.post('/api/Portal/Exchange', { token: login })).data.content;
                    setToken(r.sessionToken);
                } catch (e) { state.error = msg(e, 'This link is not valid or has expired.'); }
                history.replaceState(null, '', location.pathname);
            }
            if (token()) await load(); else state.mode = 'login';
        });
        return { state, tabs, day, money, requestLink, openCase, back, sendReply, submitCase, logout };
    }
};
Vue.createApp(App).mount('#app');
