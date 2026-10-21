const App = {
    setup() {
        const state = Vue.reactive({ entities: [], saved: [], result: null, busy: false, saveName: '', saveShared: false, def: null, schedules: [], sched: null });
        const operators = [
            { v: 'equals', t: 'is' }, { v: 'not_equals', t: 'is not' }, { v: 'contains', t: 'contains' },
            { v: 'gt', t: 'is greater / after' }, { v: 'lt', t: 'is less / before' }, { v: 'is_empty', t: 'is empty' }, { v: 'is_not_empty', t: 'is not empty' }
        ];
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const entity = () => state.entities.find(e => e.key === state.def.entity);
        const fields = Vue.computed(() => (state.def && entity()) ? entity().fields : []);
        const numeric = Vue.computed(() => fields.value.filter(f => f.type === 'number'));
        const typeOf = (key) => (fields.value.find(f => f.key === key) || {}).type;
        const entityLabel = (k) => (state.entities.find(e => e.key === k) || {}).label || k;
        const fmt = (v) => (typeof v === 'number') ? v.toLocaleString(undefined, { maximumFractionDigits: 2 }) : v;

        const blank = (key) => ({ entity: key, mode: 'list', columns: [], sortBy: '', sortDescending: false, groupBy: '', metric: 'count', filters: [] });
        const resetDef = () => {
            const k = state.def.entity;
            state.def = blank(k);
            const f = entity().fields;
            state.def.columns = f.slice(0, 6).map(x => x.key);
            state.def.groupBy = f[0].key;
            state.result = null;
        };
        const addFilter = () => { state.def.filters.push({ field: fields.value[0].key, op: 'equals', value: '' }); };

        const request = () => {
            const d = state.def;
            return {
                entity: d.entity,
                columns: d.mode === 'list' ? d.columns : null,
                filters: d.filters.map(f => ({ field: f.field, op: f.op, value: f.value })),
                groupBy: d.mode === 'group' ? d.groupBy : null,
                metric: d.mode === 'group' ? d.metric : null,
                sortBy: d.mode === 'list' && d.sortBy ? d.sortBy : null,
                sortDescending: d.sortDescending
            };
        };
        const run = async () => {
            state.busy = true;
            try { state.result = (await AxiosManager.post('/Report/Run', request())).data.content; }
            catch (e) { fail(e, 'Could not run the report'); }
            finally { state.busy = false; }
        };

        const barWidth = (v) => {
            const max = Math.max(...state.result.rows.map(r => Number(r[1]) || 0), 0);
            return max > 0 ? Math.max(1, Math.round((Number(v) || 0) / max * 100)) : 0;
        };

        const csvCell = (v) => { const s = v == null ? '' : String(v); return /[",\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
        const exportCsv = () => {
            const r = state.result; if (!r) return;
            const lines = [r.columns.map(c => csvCell(c.label)).join(',')].concat(r.rows.map(row => row.map(csvCell).join(',')));
            const blob = new Blob(['\ufeff' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = (state.saveName || 'report') + '.csv';
            a.click();
            setTimeout(() => URL.revokeObjectURL(a.href), 1000);
        };

        const loadSaved = async () => { try { state.saved = (await AxiosManager.get('/Report/ListReports', {})).data.content.data || []; } catch (e) { /* optional */ } };
        const save = async () => {
            try {
                await AxiosManager.post('/Report/SaveReport', { name: state.saveName, entity: state.def.entity, definitionJson: JSON.stringify(state.def), isShared: state.saveShared });
                Swal.fire({ icon: 'success', title: 'Report saved', timer: 1200, showConfirmButton: false });
                await loadSaved();
            } catch (e) { fail(e, 'Could not save the report'); }
        };
        const openSaved = async (r) => {
            try {
                const d = JSON.parse(r.definitionJson);
                state.def = Object.assign(blank(r.entity), d);
                state.saveName = r.name; state.saveShared = r.isShared;
                await run();
            } catch (e) { fail(e, 'This report could not be opened'); }
        };
        const loadSchedules = async () => { try { state.schedules = (await AxiosManager.get('/Report/ListSchedules', {})).data.content.data || []; } catch (e) { /* optional */ } };
        const scheduleOf = (r) => state.schedules.find(s => s.reportId === r.id && s.isActive);
        const openSchedule = (r) => {
            const s = state.schedules.find(x => x.reportId === r.id);
            state.sched = s ? { reportId: r.id, name: r.name, exists: true, frequency: s.frequency, day: s.day, hour: s.hour, recipients: s.recipients, attach: s.attach || 'Csv', isActive: s.isActive, lastStatus: s.lastStatus }
                            : { reportId: r.id, name: r.name, exists: false, frequency: 'Weekly', day: 1, hour: 8, recipients: '', attach: 'Csv', isActive: true, lastStatus: null };
        };
        const saveSchedule = async () => {
            const s = state.sched;
            try {
                await AxiosManager.post('/Report/SaveSchedule', { reportId: s.reportId, frequency: s.frequency, day: s.frequency === 'Daily' ? 0 : Number(s.day) || 0, hour: Number(s.hour) || 0, recipients: s.recipients, attach: s.attach || 'Csv', isActive: s.isActive });
                state.sched = null; await loadSchedules();
                Swal.fire({ icon: 'success', title: 'Schedule saved', timer: 1200, showConfirmButton: false });
            } catch (e) { fail(e, 'Could not save the schedule'); }
        };
        const removeSchedule = async () => {
            try { await AxiosManager.post('/Report/DeleteSchedule', { reportId: state.sched.reportId }); state.sched = null; await loadSchedules(); } catch (e) { fail(e, 'Could not stop the schedule'); }
        };
        const removeSaved = async (r) => {
            const ask = await Swal.fire({ icon: 'warning', title: `Delete "${r.name}"?`, showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Report/DeleteReport', { id: r.id }); await loadSaved(); } catch (e) { fail(e, 'Could not delete'); }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Reports']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            try {
                state.entities = (await AxiosManager.get('/Report/GetCatalog', {})).data.content.entities || [];
                if (state.entities.length) { state.def = blank(state.entities[0].key); resetDef(); }
            } catch (e) { fail(e, 'Could not load the report options'); }
            await loadSaved();
            await loadSchedules();
        });

        return { state, operators, fields, numeric, typeOf, entityLabel, fmt, resetDef, addFilter, run, barWidth, exportCsv, save, openSaved, removeSaved, scheduleOf, openSchedule, saveSchedule, removeSchedule };
    }
};

Vue.createApp(App).mount('#app');
