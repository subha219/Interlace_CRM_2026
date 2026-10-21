const App = {
    setup() {
        let saved = '';
        try { saved = localStorage.getItem('crm.agentNumber') || ''; } catch (e) { /* storage may be blocked */ }
        const state = Vue.reactive({ rows: [], configured: false, loaded: false, to: '', agent: saved, busy: false });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });
        const when = (v) => v ? new Date(String(v).endsWith('Z') ? v : v + 'Z').toLocaleString() : '';
        const badge = (s) => s === 'Completed' ? 'bg-success' : s === 'Failed' ? 'bg-danger' : (s === 'Busy' || s === 'NoAnswer') ? 'bg-warning text-dark' : 'bg-secondary';
        const load = async () => {
            try { const r = (await AxiosManager.get('/Call/GetCalls', {})).data.content; state.rows = r.data; state.configured = r.configured; state.loaded = true; }
            catch (e) { fail(e, 'Could not load the calls'); }
        };
        const call = async () => {
            state.busy = true;
            try {
                const r = (await AxiosManager.post('/Call/Start', { to: state.to, agentNumber: state.agent })).data.content;
                try { localStorage.setItem('crm.agentNumber', state.agent); } catch (e) { /* ignore */ }
                if (r.status === 'Failed') Swal.fire({ icon: 'error', title: 'Call not started', text: r.error || 'The provider refused the call.' });
                else Swal.fire({ icon: 'success', title: 'Calling you now', text: 'Pick up your phone; the customer is dialled next.', timer: 2500, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not start the call'); }
            finally { state.busy = false; }
        };
        const note = async (c) => {
            const ask = await Swal.fire({ title: 'Call note', input: 'textarea', inputValue: c.notes || '', inputAttributes: { maxlength: 2000 }, showCancelButton: true, confirmButtonText: 'Save' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/Call/SaveNote', { id: c.id, notes: ask.value }); await load(); } catch (e) { fail(e, 'Could not save the note'); }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Calls']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, when, badge, call, note };
    }
};
Vue.createApp(App).mount('#app');
