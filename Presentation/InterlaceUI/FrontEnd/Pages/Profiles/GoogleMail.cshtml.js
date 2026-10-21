const App = {
    setup() {
        const q = new URLSearchParams(location.search);
        const state = Vue.reactive({ s: null, busy: false, flash: q.get('connected') ? 'connected' : (q.get('error') || '') });
        const texts = { expired: 'The connection took too long. Please try again.', denied: 'Google access was not granted.', failed: 'Could not finish connecting to Google. Please try again.' };
        const flashText = Vue.computed(() => texts[state.flash] || 'Something went wrong.');
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const load = async () => {
            try { state.s = (await AxiosManager.get('/MailSync/GetStatus', {})).data.content; }
            catch (e) { fail(e, 'Could not load the Gmail status'); }
        };
        const connect = async () => {
            state.busy = true;
            try { const url = (await AxiosManager.get('/MailSync/GetConnectUrl', {})).data.content; window.location.href = url; }
            catch (e) { state.busy = false; fail(e, 'Could not start the connection'); }
        };
        const syncNow = async () => {
            state.busy = true;
            try { await AxiosManager.post('/MailSync/SyncNow', {}); await load(); }
            catch (e) { fail(e, 'Sync failed'); }
            finally { state.busy = false; }
        };
        const disconnect = async () => {
            const ask = await Swal.fire({
                icon: 'question', title: 'Disconnect Gmail?', text: 'Nothing new will be read. The e-mails already saved stay on the records unless you tick the box.',
                input: 'checkbox', inputPlaceholder: 'Also remove the e-mails saved from my mailbox', showCancelButton: true, confirmButtonText: 'Disconnect'
            });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/MailSync/Disconnect?removeSaved=' + (ask.value ? 'true' : 'false'), {}); await load(); } catch (e) { fail(e, 'Could not disconnect'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Profiles']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, flashText, connect, syncNow, disconnect };
    }
};
Vue.createApp(App).mount('#app');
