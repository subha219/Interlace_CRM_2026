const App = {
    setup() {
        const tabs = [{ key: 'compose', label: 'Compose' }, { key: 'sent', label: 'Sent' }, { key: 'templates', label: 'Templates' }];
        const pad = (n) => String(n).padStart(2, '0');
        const fmt = (s) => { if (!s) return ''; const d = new Date(s.endsWith('Z') ? s : s + 'Z'); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`; };
        const blankCompose = () => ({ relatedType: '', relatedId: '', relatedName: '', to: '', templateId: '', subject: '', body: '', isMarketing: false });
        const blankTemplate = () => ({ editing: false, id: '', name: '', subject: '', body: '', isMarketing: false });
        const state = Vue.reactive({ tab: 'compose', busy: false, options: [], templates: [], logs: [], c: blankCompose(), t: blankTemplate() });

        const loadTemplates = async () => { const r = await AxiosManager.get('/Email/GetEmailTemplateList', {}); state.templates = r.data.content.data || []; };
        const loadLogs = async () => { const r = await AxiosManager.get('/Email/GetEmailLogList', {}); state.logs = r.data.content.data || []; };

        const onType = async () => {
            state.c.relatedId = ''; state.c.relatedName = ''; state.options = [];
            try {
                if (state.c.relatedType === 'Customer') {
                    const r = await AxiosManager.get('/Customer/GetCustomerList', {});
                    state.options = (r.data.content.data || []).map(x => ({ id: x.id, name: `${x.number} ${x.name || ''}`.trim(), email: x.emailAddress }));
                } else if (state.c.relatedType === 'Lead') {
                    const r = await AxiosManager.get('/Lead/GetLeadList', {});
                    state.options = (r.data.content.data || []).map(x => ({ id: x.id, name: `${x.number} ${x.companyName || x.title || ''}`.trim(), email: x.companyEmail }));
                }
            } catch (e) { /* empty list */ }
        };
        const onPick = () => {
            const o = state.options.find(x => x.id === state.c.relatedId);
            state.c.relatedName = o ? o.name : '';
            if (o && o.email) state.c.to = o.email;
        };
        const applyTemplate = () => {
            const t = state.templates.find(x => x.id === state.c.templateId);
            if (!t) return;
            state.c.subject = t.subject; state.c.body = t.body; state.c.isMarketing = !!t.isMarketing;
        };

        const send = async () => {
            const c = state.c;
            if (!c.to || !c.subject || !c.body) { Swal.fire({ icon: 'warning', title: 'To, subject and message are required' }); return; }
            state.busy = true;
            try {
                const r = await AxiosManager.post('/Email/SendEmail', {
                    to: c.to, subject: c.subject, body: c.body, isMarketing: c.isMarketing,
                    relatedType: c.relatedType || null, relatedId: c.relatedId || null, relatedName: c.relatedName || null
                });
                const d = r.data.content;
                if (d.success) {
                    Swal.fire({ icon: 'success', title: 'Sent', timer: 1500, showConfirmButton: false });
                    state.c = blankCompose();
                } else {
                    Swal.fire({ icon: 'error', title: 'Not sent', text: d.error || 'The e-mail could not be sent.' });
                }
                await loadLogs();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Not sent', text: e.response?.data?.message || 'Please check the form.' });
            } finally {
                state.busy = false;
            }
        };

        const newTemplate = () => { state.t = { ...blankTemplate(), editing: true }; };
        const editTemplate = (t) => { state.t = { editing: true, id: t.id, name: t.name || '', subject: t.subject || '', body: t.body || '', isMarketing: !!t.isMarketing }; };
        const saveTemplate = async () => {
            state.busy = true;
            try {
                await AxiosManager.post('/Email/SaveEmailTemplate', { id: state.t.id || null, name: state.t.name, subject: state.t.subject, body: state.t.body, isMarketing: state.t.isMarketing });
                state.t = blankTemplate();
                await loadTemplates();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Save failed', text: e.response?.data?.message || 'Name, subject and body are required.' });
            } finally { state.busy = false; }
        };
        const deleteTemplate = async () => {
            const ask = await Swal.fire({ icon: 'warning', title: 'Delete template?', text: state.t.name, showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            await AxiosManager.post('/Email/DeleteEmailTemplate', { id: state.t.id });
            state.t = blankTemplate();
            await loadTemplates();
        };

        Vue.watch(() => state.tab, (t) => { if (t === 'sent') loadLogs(); });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Emails']);
                await SecurityManager.validateToken();
                await loadTemplates();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, tabs, fmt, onType, onPick, applyTemplate, send, newTemplate, editTemplate, saveTemplate, deleteTemplate };
    }
};

Vue.createApp(App).mount('#app');
