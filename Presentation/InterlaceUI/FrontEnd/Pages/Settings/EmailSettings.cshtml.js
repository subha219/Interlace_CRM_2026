const App = {
    setup() {
        const state = Vue.reactive({
            busy: false, source: '', hasPassword: false, testTo: '', testMessage: '', testOk: false,
            f: { host: '', port: 587, security: 'Auto', userName: '', password: '', fromAddress: '', fromName: '' }
        });

        const load = async () => {
            const r = await AxiosManager.get('/Setting/GetSmtpSettings', {});
            const d = r.data.content.data;
            state.source = d.source;
            state.hasPassword = d.hasPassword;
            state.f = { host: d.host || '', port: d.port || 587, security: d.security || 'Auto', userName: d.userName || '', password: '', fromAddress: d.fromAddress || '', fromName: d.fromName || '' };
        };

        const save = async () => {
            if (!state.f.host) { Swal.fire({ icon: 'warning', title: 'Host is required' }); return; }
            state.busy = true;
            try {
                await AxiosManager.post('/Setting/SaveSmtpSettings', { ...state.f, port: Number(state.f.port) });
                Swal.fire({ icon: 'success', title: 'Saved', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Save failed', text: e.response?.data?.message || 'Please check the values.' });
            } finally { state.busy = false; }
        };

        const test = async () => {
            if (!state.testTo) { Swal.fire({ icon: 'warning', title: 'Enter an address to send the test to' }); return; }
            state.busy = true;
            state.testMessage = '';
            try {
                const r = await AxiosManager.post('/Setting/SendTestEmail', { to: state.testTo });
                const d = r.data.content;
                state.testOk = !!d.success;
                state.testMessage = d.success ? 'Test e-mail sent. Check the inbox.' : (d.error || 'The e-mail could not be sent.');
            } catch (e) {
                state.testOk = false;
                state.testMessage = e.response?.data?.message || 'The request failed.';
            } finally { state.busy = false; }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Settings']);
                await SecurityManager.validateToken();
                await load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, save, test };
    }
};

Vue.createApp(App).mount('#app');
