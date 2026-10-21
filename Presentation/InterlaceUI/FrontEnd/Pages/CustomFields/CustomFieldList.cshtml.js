const App = {
    setup() {
        const state = Vue.reactive({ items: [], filter: '', form: null });
        const shown = Vue.computed(() => state.items.filter(x => !state.filter || x.entity === state.filter));
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || 'Please try again.' });

        const load = async () => {
            try { state.items = (await AxiosManager.get('/CustomField/GetDefinitions', {})).data.content.data || []; }
            catch (e) { fail(e, 'Could not load custom fields'); }
        };
        const blank = () => ({ id: null, entity: 'Lead', label: '', fieldType: 'Text', options: '', isRequired: false, isActive: true, sortOrder: 0 });
        const add = () => { state.form = blank(); };
        const edit = (f) => { state.form = { ...f, options: (f.options || []).join('\n') }; };
        const save = async () => {
            try {
                await AxiosManager.post('/CustomField/SaveDefinition', state.form);
                state.form = null;
                await load();
            } catch (e) { fail(e, 'Could not save the field'); }
        };
        const remove = async (f) => {
            const ask = await Swal.fire({ icon: 'warning', title: `Delete "${f.label}"?`, text: 'Values already entered stay hidden. Consider deactivating instead.', showCancelButton: true, confirmButtonText: 'Delete' });
            if (!ask.isConfirmed) return;
            try { await AxiosManager.post('/CustomField/DeleteDefinition', { id: f.id }); await load(); }
            catch (e) { fail(e, 'Could not delete'); }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['CustomFields']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
            await load();
        });

        return { state, shown, add, edit, save, remove };
    }
};

Vue.createApp(App).mount('#app');
