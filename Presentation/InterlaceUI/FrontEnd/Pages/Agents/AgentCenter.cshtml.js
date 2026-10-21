const App = {
    setup() {
        const statuses = ['Pending', 'Approved', 'Rejected', 'Failed'];
        const state = Vue.reactive({ agents: [], modelConfigured: false, status: 'Pending', agentKey: '', proposals: [], picked: {}, busy: false });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const when = (v) => v ? new Date(String(v).endsWith('Z') ? v : v + 'Z').toLocaleString() : '';
        const urls = { Lead: '/Pipelines/PipelineBoard', Customer: '/Customers/CustomerList', Contract: '/Contracts/ContractList', SupportCase: '/SupportCases/SupportCaseList' };
        const link = (p) => urls[p.relatedType] || '#';
        const agentName = (k) => (state.agents.find(a => a.key === k) || {}).name || k;
        const selected = Vue.computed(() => state.proposals.filter(p => state.picked[p.id]));
        const allChecked = Vue.computed(() => state.proposals.length > 0 && selected.value.length === state.proposals.length);
        const toggleAll = (on) => { state.proposals.forEach(p => { state.picked[p.id] = on; }); };

        const loadAgents = async () => {
            try { const r = (await AxiosManager.get('/Agent/GetAgents', {})).data.content; state.agents = r.data; state.modelConfigured = r.modelConfigured; }
            catch (e) { fail(e, 'Could not load the agents'); }
        };
        const loadProposals = async () => {
            try {
                const q = '/Agent/GetProposals?status=' + state.status + (state.agentKey ? '&agentKey=' + state.agentKey : '');
                state.proposals = (await AxiosManager.get(q, {})).data.content.data; state.picked = {};
            } catch (e) { fail(e, 'Could not load the suggestions'); }
        };
        const setStatus = async (s) => { state.status = s; await loadProposals(); };
        const filterAgent = async (k) => { state.agentKey = k; state.status = 'Pending'; await loadProposals(); };

        const saveSetting = async (a) => {
            try { await AxiosManager.post('/Agent/SaveSetting', { agentKey: a.key, enabled: a.enabled, autoRun: !!a.autoRun && a.enabled, maxPerRun: a.maxPerRun || 20 }); }
            catch (e) { fail(e, 'Could not save'); await loadAgents(); }
        };
        const run = async (a) => {
            state.busy = true;
            try {
                const r = (await AxiosManager.post('/Agent/Run', { agentKey: a.key })).data.content;
                Swal.fire({ icon: 'success', title: a.name, text: 'Looked at ' + r.scanned + ' record(s), made ' + r.proposed + ' suggestion(s).' + (r.notice ? ' ' + r.notice : ''), timer: 2800, showConfirmButton: false });
                await loadAgents(); await loadProposals();
            } catch (e) { fail(e, 'The agent could not run'); } finally { state.busy = false; }
        };
        const send = async (p, approve) => {
            const body = { id: p.id, approve };
            if (approve && p.kind === 'Task') { body.title = p.title; body.dueInDays = p.dueInDays; body.priority = p.priority; if (p.draft != null) body.draft = p.draft; }
            return (await AxiosManager.post('/Agent/Decide', body)).data.content;
        };
        const decide = async (p, approve) => {
            state.busy = true;
            try {
                const r = await send(p, approve);
                Swal.fire({ icon: 'success', title: r.status, text: r.message, timer: 1600, showConfirmButton: false });
            } catch (e) { fail(e, approve ? 'Could not approve' : 'Could not reject'); }
            finally { state.busy = false; await loadAgents(); await loadProposals(); }
        };
        const bulk = async (approve) => {
            const items = selected.value.slice();
            const ask = await Swal.fire({ icon: 'question', title: (approve ? 'Approve ' : 'Reject ') + items.length + ' suggestion(s)?', showCancelButton: true, confirmButtonText: approve ? 'Approve' : 'Reject' });
            if (!ask.isConfirmed) return;
            state.busy = true;
            let ok = 0; const errors = [];
            for (const p of items) {
                try { await send(p, approve); ok++; } catch (e) { errors.push((p.relatedName || '') + ': ' + (e.response?.data?.message || 'failed')); }
            }
            state.busy = false;
            Swal.fire({ icon: errors.length ? 'warning' : 'success', title: ok + ' done' + (errors.length ? ', ' + errors.length + ' failed' : ''), text: errors.slice(0, 3).join(' | ') });
            await loadAgents(); await loadProposals();
        };

        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Agents']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await loadAgents(); await loadProposals();
        });

        return { state, statuses, when, link, agentName, selected, allChecked, toggleAll, setStatus, filterAgent, saveSetting, run, decide, bulk };
    }
};

Vue.createApp(App).mount('#app');
