const App = {
    setup() {
        const state = Vue.reactive({ stages: [] });
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const load = async () => {
            try { state.stages = (await AxiosManager.get('/PipelineSetting/GetStages', {})).data.content.data || []; }
            catch (e) { fail(e, 'Could not load the stages'); }
        };
        const save = async () => {
            try {
                await AxiosManager.post('/PipelineSetting/SaveStages', { stages: state.stages.map(s => ({ stage: s.stage, label: s.label === s.defaultLabel ? '' : s.label, probability: Number(s.probability) || 0 })) });
                Swal.fire({ icon: 'success', title: 'Saved', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not save'); }
        };
        const reset = async () => {
            const ask = await Swal.fire({ icon: 'question', title: 'Restore the standard stages?', showCancelButton: true, confirmButtonText: 'Restore' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/PipelineSetting/ResetStages', {}); await load(); } catch (e) { fail(e, 'Could not restore'); }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['PipelineSettings']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
        });

        return { state, save, reset };
    }
};

Vue.createApp(App).mount('#app');
