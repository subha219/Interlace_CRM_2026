const App = {
    setup() {
        const types = ['Task', 'Call', 'Meeting', 'Email', 'Note'];
        const modalRef = Vue.ref(null);
        let modal = null;

        const pad = (n) => String(n).padStart(2, '0');
        const toInput = (d) => d ? `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}` : '';
        const parse = (s) => s ? new Date(s) : null;
        const fmt = (s) => { const d = parse(s); return d ? `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}` : ''; };
        const emptyForm = () => ({ id: '', subject: '', type: 'Task', startAt: '', endAt: '', priority: 'Normal', assignedToUserId: StorageManager.getUserId(), relatedType: '', relatedId: '', relatedName: '', description: '', outcome: '' });

        const now = new Date();
        const state = Vue.reactive({
            view: 'list', scope: 'mine', status: '', type: '',
            items: [], assignees: [], relatedOptions: [], busy: false,
            month: new Date(now.getFullYear(), now.getMonth(), 1),
            form: emptyForm()
        });

        const load = async () => {
            try {
                const r = await AxiosManager.get('/Activity/GetActivityList?mine=' + (state.scope === 'mine'), {});
                state.items = r.data.content.data || [];
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Could not load activities', text: e.response?.data?.message || 'Please try again.' });
            }
        };

        const filtered = Vue.computed(() => state.items.filter(a =>
            (!state.status || a.statusName === state.status) && (!state.type || a.typeName === state.type)));

        const assigneeName = (id) => (state.assignees.find(u => u.id === id) || {}).name || '';
        const linkFor = (a) => a.relatedType === 'Lead' ? '/Leads/LeadList' : a.relatedType === 'Customer' ? '/Customers/Customer360' : '/SupportCases/SupportCaseList';

        const loadRelated = async (type) => {
            state.relatedOptions = [];
            try {
                if (type === 'Lead') {
                    const r = await AxiosManager.get('/Lead/GetLeadList', {});
                    state.relatedOptions = (r.data.content.data || []).map(x => ({ id: x.id, name: `${x.number} ${x.companyName || x.title || ''}`.trim() }));
                } else if (type === 'Customer') {
                    const r = await AxiosManager.get('/Customer/GetCustomerList', {});
                    state.relatedOptions = (r.data.content.data || []).map(x => ({ id: x.id, name: `${x.number} ${x.name || ''}`.trim() }));
                } else if (type === 'SupportCase') {
                    const r = await AxiosManager.get('/SupportCase/GetSupportCaseList', {});
                    state.relatedOptions = (r.data.content.data || []).map(x => ({ id: x.id, name: `${x.number} ${x.title || ''}`.trim() }));
                }
            } catch (e) { /* list stays empty */ }
        };

        const onRelatedType = async () => { state.form.relatedId = ''; state.form.relatedName = ''; await loadRelated(state.form.relatedType); };
        const onRelatedPick = () => { state.form.relatedName = (state.relatedOptions.find(r => r.id === state.form.relatedId) || {}).name || ''; };

        const openNew = (date) => {
            state.form = emptyForm();
            if (date) { const d = new Date(date); d.setHours(9, 0, 0, 0); state.form.startAt = toInput(d); }
            state.relatedOptions = [];
            modal.show();
        };

        const openEdit = async (a) => {
            state.form = {
                id: a.id, subject: a.subject || '', type: a.typeName || 'Task', startAt: toInput(parse(a.startAt)), endAt: toInput(parse(a.endAt)),
                priority: a.priority || 'Normal', assignedToUserId: a.assignedToUserId || StorageManager.getUserId(),
                relatedType: a.relatedType || '', relatedId: a.relatedId || '', relatedName: a.relatedName || '', description: a.description || '', outcome: a.outcome || ''
            };
            await loadRelated(state.form.relatedType);
            modal.show();
        };

        const save = async () => {
            const f = state.form;
            if (!f.subject || f.subject.trim().length < 2) { Swal.fire({ icon: 'warning', title: 'Subject is required' }); return; }
            state.busy = true;
            try {
                await AxiosManager.post('/Activity/SaveActivity', {
                    id: f.id || null, subject: f.subject, description: f.description, type: types.indexOf(f.type), priority: f.priority,
                    startAt: f.startAt || null, endAt: f.endAt || null, relatedType: f.relatedType || null, relatedId: f.relatedId || null,
                    relatedName: f.relatedName || null, assignedToUserId: f.assignedToUserId
                });
                if (f.id && f.outcome) {
                    await AxiosManager.post('/Activity/SetActivityStatus', { id: f.id, status: (state.items.find(i => i.id === f.id)?.status ?? 0), outcome: f.outcome });
                }
                modal.hide();
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Save failed', text: e.response?.data?.message || 'Please check the form.' });
            } finally {
                state.busy = false;
            }
        };

        const toggleDone = async (a) => {
            try {
                await AxiosManager.post('/Activity/SetActivityStatus', { id: a.id, status: a.statusName === 'Done' ? 0 : 1 });
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Update failed', text: e.response?.data?.message || 'Please try again.' });
            }
        };

        const remove = async (a) => {
            const ask = await Swal.fire({ icon: 'warning', title: 'Delete activity?', text: a.subject, showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try {
                await AxiosManager.post('/Activity/DeleteActivity', { id: a.id });
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Delete failed', text: e.response?.data?.message || 'Please try again.' });
            }
        };

        const shift = (n) => { state.month = new Date(state.month.getFullYear(), state.month.getMonth() + n, 1); };
        const today = () => { const t = new Date(); state.month = new Date(t.getFullYear(), t.getMonth(), 1); };
        const monthLabel = Vue.computed(() => state.month.toLocaleString(undefined, { month: 'long', year: 'numeric' }));
        const dayKey = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
        const timeOf = (a) => { const d = parse(a.startAt); return d && (d.getHours() || d.getMinutes()) ? pad(d.getHours()) + ':' + pad(d.getMinutes()) + ' ' : ''; };

        const cells = Vue.computed(() => {
            const first = new Date(state.month);
            const offset = (first.getDay() + 6) % 7;
            const start = new Date(first.getFullYear(), first.getMonth(), 1 - offset);
            const byDay = {};
            filtered.value.forEach(a => {
                const d = parse(a.startAt || a.endAt);
                if (d) { (byDay[dayKey(d)] = byDay[dayKey(d)] || []).push(a); }
            });
            const todayKey = dayKey(new Date());
            return Array.from({ length: 42 }, (_, i) => {
                const date = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
                const key = dayKey(date);
                return { key, date, inMonth: date.getMonth() === state.month.getMonth(), isToday: key === todayKey, items: byDay[key] || [] };
            });
        });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Activities']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            modal = new bootstrap.Modal(modalRef.value, { backdrop: 'static', keyboard: false });
            try {
                const r = await AxiosManager.get('/Activity/GetAssignees', {});
                state.assignees = r.data.content || [];
            } catch (e) { /* assignee names stay blank */ }
            await load();
        });

        return { state, types, modalRef, filtered, cells, monthLabel, load, fmt, timeOf, assigneeName, linkFor, onRelatedType, onRelatedPick, openNew, openEdit, save, toggleDone, remove, shift, today };
    }
};

Vue.createApp(App).mount('#app');
