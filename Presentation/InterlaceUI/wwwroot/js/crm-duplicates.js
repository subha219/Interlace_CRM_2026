const App = {
    setup() {
        const entity = document.getElementById('app').dataset.entity;
        const state = Vue.reactive({ loading: true, busy: false, groups: [] });

        const scan = async () => {
            state.loading = true;
            try {
                const r = await AxiosManager.get('/Duplicate/GetDuplicateGroups?entity=' + entity, {});
                state.groups = (r.data.content.data || []).map(g => ({ ...g, keep: g.items[0] ? g.items[0].id : null }));
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Scan failed', text: e.response?.data?.message || 'Please try again.' });
            } finally {
                state.loading = false;
            }
        };

        const merge = async (g) => {
            const others = g.items.filter(i => i.id !== g.keep);
            const keepItem = g.items.find(i => i.id === g.keep);
            const ask = await Swal.fire({
                icon: 'warning',
                title: 'Merge ' + others.length + ' record(s)?',
                text: 'Everything is moved into ' + (keepItem?.number || '') + ' ' + (keepItem?.name || '') + ' and the others are removed. This cannot be undone from here.',
                showCancelButton: true,
                confirmButtonText: 'Merge'
            });
            if (!ask.isConfirmed) return;
            state.busy = true;
            try {
                await AxiosManager.post('/Duplicate/MergeRecords', {
                    entity, survivorId: g.keep, duplicateIds: others.map(i => i.id), updatedById: StorageManager.getUserId()
                });
                Swal.fire({ icon: 'success', title: 'Merged', timer: 1200, showConfirmButton: false });
                await scan();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Merge failed', text: e.response?.data?.message || 'Please try again.' });
            } finally {
                state.busy = false;
            }
        };

        Vue.onMounted(scan);
        return { state, scan, merge };
    }
};

Vue.createApp(App).mount('#app');
