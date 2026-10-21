const App = {
    setup() {
        const state = Vue.reactive({ tab: 'rules', rules: [], runs: [], events: [], users: [], form: null });

        const FIELDS = {
            lead: ['title', 'company', 'email', 'stage', 'previousStage', 'amount', 'source', 'owner', 'number'],
            customer: ['name', 'email', 'source', 'owner', 'number'],
            case: ['title', 'priority', 'customerId', 'owner', 'number']
        };
        const operators = [
            { v: 'equals', t: 'is' }, { v: 'not_equals', t: 'is not' }, { v: 'contains', t: 'contains' },
            { v: 'gt', t: 'is greater than' }, { v: 'lt', t: 'is less than' }, { v: 'is_empty', t: 'is empty' }, { v: 'is_not_empty', t: 'is not empty' }
        ];
        const EVENT_LABELS = {
            'lead.created': 'A lead is created', 'lead.stage_changed': 'A lead moves to another stage', 'lead.won': 'A lead is won',
            'lead.lost': 'A lead is lost', 'customer.created': 'A customer is created', 'case.created': 'A support case is created'
        };
        const eventLabel = (e) => EVENT_LABELS[e] || e;
        const when = (d) => d ? new Date(d).toLocaleString() : '';
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const kind = () => (state.form?.triggerEvent || 'lead.created').split('.')[0];
        const fields = Vue.computed(() => FIELDS[kind()] || FIELDS.lead);
        const hasTags = Vue.computed(() => kind() !== 'case');

        const blankAction = () => ({ type: 'notify', to: 'owner', title: 'Workflow', message: '', activityType: 'Task', subject: '', dueInDays: 1, assignTo: 'owner', userId: '', tag: '', body: '', marketing: false });
        const safe = (s) => { try { return JSON.parse(s || '[]'); } catch (e) { return []; } };
        const summaryIf = (r) => { const c = safe(r.conditionsJson); return c.length ? c.map(x => `${x.field} ${x.op.replace('_', ' ')} ${x.value || ''}`.trim()).join(' and ') : 'always'; };
        const summaryThen = (r) => safe(r.actionsJson).map(a => a.type.replace('_', ' ')).join(', ');

        const load = async () => {
            try {
                const c = (await AxiosManager.get('/Workflow/ListRules', {})).data.content;
                state.rules = c.data || []; state.events = c.events || [];
            } catch (e) { fail(e, 'Could not load rules'); }
        };
        const loadRuns = async () => { try { state.runs = (await AxiosManager.get('/Workflow/ListRuns', {})).data.content.data || []; } catch (e) { fail(e, 'Could not load the history'); } };
        const openRuns = async () => { state.tab = 'runs'; await loadRuns(); };

        const add = () => { state.form = { id: null, name: '', triggerEvent: 'lead.created', isActive: true, conditions: [], actions: [blankAction()] }; };
        const edit = (r) => {
            state.form = { id: r.id, name: r.name, triggerEvent: r.triggerEvent, isActive: r.isActive, conditions: safe(r.conditionsJson),
                actions: safe(r.actionsJson).map(a => ({ ...blankAction(), ...a })) };
        };
        const clean = (a) => {
            const keep = {
                create_activity: ['type', 'activityType', 'subject', 'dueInDays', 'assignTo'],
                notify: ['type', 'to', 'title', 'message'], set_owner: ['type', 'userId'], add_tag: ['type', 'tag'], send_email: ['type', 'subject', 'body', 'marketing']
            }[a.type] || ['type'];
            const o = {}; keep.forEach(k => { o[k] = a[k]; }); return o;
        };
        const save = async () => {
            const f = state.form;
            try {
                await AxiosManager.post('/Workflow/SaveRule', {
                    id: f.id, name: f.name, triggerEvent: f.triggerEvent, isActive: f.isActive,
                    conditionsJson: JSON.stringify(f.conditions), actionsJson: JSON.stringify(f.actions.map(clean))
                });
                state.form = null; await load();
            } catch (e) { fail(e, 'Could not save the rule'); }
        };
        const remove = async (r) => {
            const ask = await Swal.fire({ icon: 'warning', title: `Delete "${r.name}"?`, showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Workflow/DeleteRule', { id: r.id }); await load(); } catch (e) { fail(e, 'Could not delete'); }
        };

        Vue.watch(() => state.form && state.form.triggerEvent, () => {
            if (!state.form) return;
            const allowed = FIELDS[kind()];
            state.form.conditions.forEach(c => { if (!allowed.includes(c.field)) c.field = allowed[0]; });
            if (kind() === 'case') state.form.actions = state.form.actions.filter(a => a.type !== 'add_tag');
        });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Workflows']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
            try { state.users = (await AxiosManager.get('/Activity/GetAssignees', {})).data.content || []; } catch (e) { /* optional */ }
        });

        return { state, operators, fields, hasTags, eventLabel, when, blankAction, summaryIf, summaryThen, add, edit, save, remove, openRuns, loadRuns };
    }
};

Vue.createApp(App).mount('#app');
