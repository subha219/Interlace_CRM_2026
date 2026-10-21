const App = {
    setup() {
        const state = Vue.reactive({ list: [], configured: null, thread: null, draft: null, templates: [], templateId: '', values: {}, text: '', marketing: false, busy: false });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const when = (v) => v ? new Date(String(v).endsWith('Z') ? v : v + 'Z').toLocaleString() : '';
        const templates = Vue.computed(() => state.thread ? state.templates.filter(t => t.channel === state.thread.channel && t.isActive) : []);
        const tpl = Vue.computed(() => templates.value.find(t => t.id === state.templateId) || null);
        const vars = Vue.computed(() => tpl.value && tpl.value.variables ? tpl.value.variables.split(',').filter(Boolean) : []);
        const isOpen = (c) => state.thread && state.thread.channel === c.channel && state.thread.address === c.address;
        const loadList = async () => {
            try { const r = (await AxiosManager.get('/Messaging/GetConversations', {})).data.content; state.list = r.data; state.configured = r.configured; }
            catch (e) { fail(e, 'Could not load the conversations'); }
        };
        const open = async (channel, address) => {
            try {
                state.thread = (await AxiosManager.get('/Messaging/GetThread?channel=' + encodeURIComponent(channel) + '&address=' + encodeURIComponent(address), {})).data.content;
                state.draft = null; state.templateId = ''; state.values = {}; state.text = ''; state.marketing = false;
            } catch (e) { fail(e, 'Could not open the conversation'); }
        };
        const pickTemplate = () => { state.values = {}; };
        const newChat = () => { state.thread = null; state.draft = { channel: 'WhatsApp', address: '' }; };
        const startChat = async () => {
            const digits = (state.draft.address || '').replace(/\D/g, '');
            if (digits.length < 10) { Swal.fire({ icon: 'warning', title: 'Enter a valid phone number' }); return; }
            await open(state.draft.channel, state.draft.address);
        };
        const send = async () => {
            state.busy = true;
            try {
                const r = (await AxiosManager.post('/Messaging/Send', { channel: state.thread.channel, to: state.thread.address, text: state.text, templateId: state.templateId || null, variables: state.values, isMarketing: state.marketing })).data.content;
                if (r.status === 'Failed') Swal.fire({ icon: 'error', title: 'Not delivered', text: r.error || 'The provider refused the message.' });
                await open(state.thread.channel, state.thread.address);
                await loadList();
            } catch (e) { fail(e, 'Could not send'); }
            finally { state.busy = false; }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Messaging']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            try { state.templates = (await AxiosManager.get('/Messaging/GetTemplates', {})).data.content.data || []; } catch (e) { /* free text still works */ }
            await loadList();
            const q = new URLSearchParams(location.search);
            if (q.get('address')) await open(q.get('channel') === 'SMS' ? 'SMS' : 'WhatsApp', q.get('address'));
        });
        return { state, templates, tpl, vars, when, isOpen, open, pickTemplate, newChat, startChat, send };
    }
};
Vue.createApp(App).mount('#app');
