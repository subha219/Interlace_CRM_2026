const App = {
    setup() {
        const q = new URLSearchParams(location.search);
        const state = Vue.reactive({ s: null, busy: false, flash: q.get('connected') ? 'connected' : (q.get('error') || '') });
        const texts = { expired: 'The connection took too long. Please try again.', denied: 'Access was not granted.', failed: 'Could not finish connecting. Please try again.' };
        const flashText = Vue.computed(() => texts[state.flash] || 'Something went wrong.');
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const load = async () => {
            try { state.s = (await AxiosManager.get('/Calendar/GetStatus', {})).data.content; }
            catch (e) { fail(e, 'Could not load the calendar status'); }
        };
        const connect = async (provider) => {
            state.busy = true;
            try { const url = (await AxiosManager.get('/Calendar/GetConnectUrl', { provider })).data.content; window.location.href = url; }
            catch (e) { state.busy = false; fail(e, 'Could not start the connection'); }
        };
        const syncNow = async () => {
            state.busy = true;
            try { await AxiosManager.post('/Calendar/SyncNow', {}); await load(); }
            catch (e) { fail(e, 'Sync failed'); }
            finally { state.busy = false; }
        };
        const disconnect = async () => {
            const ask = await Swal.fire({ icon: 'question', title: 'Disconnect the calendar?', text: 'Events already created stay in the calendar; nothing new is sent.', showCancelButton: true, confirmButtonText: 'Disconnect' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Calendar/Disconnect', {}); await load(); } catch (e) { fail(e, 'Could not disconnect'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Profiles']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, flashText, connect, syncNow, disconnect };
    }
};
Vue.createApp(App).mount('#app');
