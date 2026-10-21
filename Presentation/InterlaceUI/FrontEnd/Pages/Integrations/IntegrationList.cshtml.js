const App = {
    setup() {
        const state = Vue.reactive({
            tab: 'keys', keys: [], hooks: [], events: [], log: [], form: null,
            keyName: '', keyDays: null, newKey: ''
        });
        const tabs = [{ key: 'keys', label: 'API keys' }, { key: 'hooks', label: 'Webhooks' }, { key: 'log', label: 'Deliveries' }];
        const origin = window.location.origin;
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const when = (d) => d ? new Date(d).toLocaleString() : '';
        const copy = async (t) => { try { await navigator.clipboard.writeText(t); Swal.fire({ icon: 'success', title: 'Copied', timer: 900, showConfirmButton: false }); } catch (e) { /* ignore */ } };

        const loadKeys = async () => { try { state.keys = (await AxiosManager.get('/Integration/ListApiKeys', {})).data.content.data || []; } catch (e) { fail(e, 'Could not load keys'); } };
        const loadHooks = async () => {
            try { const c = (await AxiosManager.get('/Integration/ListWebhooks', {})).data.content; state.hooks = c.data || []; state.events = c.availableEvents || []; }
            catch (e) { fail(e, 'Could not load webhooks'); }
        };
        const loadLog = async () => { try { state.log = (await AxiosManager.get('/Integration/ListDeliveries', {})).data.content.data || []; } catch (e) { fail(e, 'Could not load deliveries'); } };

        const createKey = async () => {
            try {
                const r = await AxiosManager.post('/Integration/CreateApiKey', { name: state.keyName, expiresInDays: state.keyDays || null });
                state.newKey = r.data.content.key; state.keyName = ''; state.keyDays = null;
                await loadKeys();
            } catch (e) { fail(e, 'Could not create the key'); }
        };
        const revoke = async (k) => {
            const ask = await Swal.fire({ icon: 'warning', title: `Revoke "${k.name}"?`, text: 'Systems using this key will stop working.', showCancelButton: true, confirmButtonText: 'Revoke' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Integration/RevokeApiKey', { id: k.id }); await loadKeys(); } catch (e) { fail(e, 'Could not revoke'); }
        };

        const newHook = () => { state.form = { id: null, name: '', url: '', chosen: ['lead.created'], isActive: true, rotateSecret: false, secret: '' }; };
        const editHook = (h) => { state.form = { id: h.id, name: h.name, url: h.url, chosen: (h.events || '').split(',').filter(Boolean), isActive: h.isActive, rotateSecret: false, secret: h.secret }; };
        const saveHook = async () => {
            const f = state.form;
            try {
                await AxiosManager.post('/Integration/SaveWebhook', { id: f.id, name: f.name, url: f.url, events: f.chosen.join(','), isActive: f.isActive, rotateSecret: f.rotateSecret });
                state.form = null; await loadHooks();
            } catch (e) { fail(e, 'Could not save the webhook'); }
        };
        const removeHook = async (h) => {
            const ask = await Swal.fire({ icon: 'warning', title: `Delete "${h.name}"?`, showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Integration/DeleteWebhook', { id: h.id }); await loadHooks(); } catch (e) { fail(e, 'Could not delete'); }
        };
        const test = async (h) => {
            try {
                await AxiosManager.post('/Integration/TestWebhook', { id: h.id });
                Swal.fire({ icon: 'info', title: 'Test queued', text: 'Check the Deliveries tab in a few seconds.', timer: 1800, showConfirmButton: false });
            } catch (e) { fail(e, 'Could not send the test'); }
        };

        Vue.watch(() => state.tab, (t) => { if (t === 'log') loadLog(); });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Integrations']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await Promise.all([loadKeys(), loadHooks()]);
        });

        return { state, tabs, origin, when, copy, createKey, revoke, newHook, editHook, saveHook, removeHook, test, loadLog };
    }
};

Vue.createApp(App).mount('#app');
