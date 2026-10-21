const App = {
    setup() {
        const state = Vue.reactive({ list: [], unread: 0, closed: false, chat: null, messages: [], text: '', busy: false });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const when = (v) => v ? new Date(String(v).endsWith('Z') ? v : v + 'Z').toLocaleString() : '';
        const origin = window.location.origin;
        const copy = async () => {
            try { await navigator.clipboard.writeText('<script src="' + origin + '/js/chat-embed.js" async></script>'); Swal.fire({ icon: 'success', title: 'Copied', timer: 1000, showConfirmButton: false }); }
            catch (e) { Swal.fire({ icon: 'info', title: 'Select the line and copy it' }); }
        };
        const loadList = async () => {
            try { const r = (await AxiosManager.get('/Chat/GetChats?includeClosed=' + state.closed, {})).data.content; state.list = r.data; state.unread = r.unreadCount; }
            catch (e) { /* the next refresh tries again */ }
        };
        const open = async (id) => {
            try {
                const r = (await AxiosManager.get('/Chat/GetThread?id=' + encodeURIComponent(id), {})).data.content;
                const grew = !state.chat || state.chat.id !== id || state.messages.length !== r.messages.length;
                state.chat = r.chat; state.messages = r.messages;
                if (grew) Vue.nextTick(() => { const el = document.querySelector('.card-body[style*="max-height"]'); if (el) el.scrollTop = el.scrollHeight; });
            } catch (e) { fail(e, 'Could not open the chat'); }
        };
        const reply = async () => {
            if (!state.text.trim()) return;
            state.busy = true;
            try { await AxiosManager.post('/Chat/Reply', { id: state.chat.id, text: state.text }); state.text = ''; await open(state.chat.id); await loadList(); }
            catch (e) { fail(e, 'Could not send'); }
            finally { state.busy = false; }
        };
        const makeCase = async () => {
            state.busy = true;
            try {
                const r = (await AxiosManager.post('/Chat/CreateCase', { id: state.chat.id, priority: 1 })).data.content;
                await open(state.chat.id); await loadList();
                Swal.fire({ icon: 'success', title: r.alreadyExisted ? 'Case already exists' : 'Case created', text: r.caseNumber, timer: 2000, showConfirmButton: false });
            } catch (e) {
                fail(e, (e && e.response && e.response.status === 403) ? 'Your role does not include Support cases' : 'Could not create the case');
            } finally { state.busy = false; }
        };
        const close = async () => {
            const ask = await Swal.fire({ icon: 'question', title: 'Close this chat?', showCancelButton: true, confirmButtonText: 'Close' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Chat/Close', { id: state.chat.id }); await open(state.chat.id); await loadList(); } catch (e) { fail(e, 'Could not close'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Chats']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await loadList();
            setInterval(async () => {
                if (document.hidden) return;
                await loadList();
                if (state.chat && !state.busy && !state.text) { const was = state.chat.status; await open(state.chat.id); }
            }, 4000);
        });
        return { state, origin, when, copy, loadList, open, reply, close, makeCase };
    }
};
Vue.createApp(App).mount('#app');
