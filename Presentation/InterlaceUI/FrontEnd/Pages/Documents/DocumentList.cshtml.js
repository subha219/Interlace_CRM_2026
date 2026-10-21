const App = {
    setup() {
        const types = [
            { key: 'Customer', label: 'Customer', url: '/Customer/GetCustomerList', name: x => `${x.number} ${x.name || ''}` },
            { key: 'Lead', label: 'Lead', url: '/Lead/GetLeadList', name: x => `${x.number} ${x.companyName || x.title || ''}` },
            { key: 'SupportCase', label: 'Support case', url: '/SupportCase/GetSupportCaseList', name: x => `${x.number} ${x.title || ''}` },
            { key: 'Quotation', label: 'Quotation', url: '/Quotation/GetQuotationList', name: x => `${x.number} ${x.title || x.customerName || ''}` },
            { key: 'Invoice', label: 'Invoice', url: '/Invoice/GetInvoiceList', name: x => `${x.number} ${x.customerName || ''}` }
        ];
        const fileRef = Vue.ref(null);
        const state = Vue.reactive({
            busy: false, docs: [], uOptions: [], fOptions: [],
            u: { relatedType: 'Customer', relatedId: '', description: '' },
            f: { relatedType: '', relatedId: '' }
        });

        const options = async (typeKey) => {
            const t = types.find(x => x.key === typeKey);
            if (!t) return [];
            try {
                const r = await AxiosManager.get(t.url, {});
                return (r.data.content.data || []).map(x => ({ id: x.id, name: t.name(x).trim() }));
            } catch (e) { return []; }
        };
        const onType = async (which) => {
            if (which === 'u') { state.u.relatedId = ''; state.uOptions = await options(state.u.relatedType); }
            else { state.f.relatedId = ''; state.fOptions = await options(state.f.relatedType); await load(); }
        };

        const load = async () => {
            const q = new URLSearchParams();
            if (state.f.relatedType) q.set('relatedType', state.f.relatedType);
            if (state.f.relatedId) q.set('relatedId', state.f.relatedId);
            const r = await AxiosManager.get('/Document/GetDocumentList?' + q.toString(), {});
            state.docs = r.data.content.data || [];
        };

        const upload = async () => {
            const file = fileRef.value?.files?.[0];
            if (!file) { Swal.fire({ icon: 'warning', title: 'Choose a file first' }); return; }
            if (!state.u.relatedId) { Swal.fire({ icon: 'warning', title: 'Choose the record it belongs to' }); return; }
            const fd = new FormData();
            fd.append('file', file);
            fd.append('relatedType', state.u.relatedType);
            fd.append('relatedId', state.u.relatedId);
            fd.append('relatedName', (state.uOptions.find(o => o.id === state.u.relatedId) || {}).name || '');
            fd.append('description', state.u.description || '');
            state.busy = true;
            try {
                await AxiosManager.post('/Document/Upload', fd, { headers: { 'Content-Type': 'multipart/form-data' } });
                fileRef.value.value = '';
                state.u.description = '';
                Swal.fire({ icon: 'success', title: 'Uploaded', timer: 1200, showConfirmButton: false });
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Upload failed', text: e.response?.data?.message || e.response?.data || 'Please try another file.' });
            } finally { state.busy = false; }
        };

        const download = async (d) => {
            try {
                const r = await AxiosManager.get('/Document/Download?id=' + encodeURIComponent(d.id), { responseType: 'blob' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(r.data);
                a.download = d.originalName || 'document';
                a.click();
                URL.revokeObjectURL(a.href);
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Download failed', text: 'The file could not be found.' });
            }
        };

        const remove = async (d) => {
            const ask = await Swal.fire({ icon: 'warning', title: 'Remove document?', text: d.originalName, showCancelButton: true, confirmButtonText: 'Remove' });
            if (!ask.isConfirmed) return;
            await AxiosManager.post('/Document/DeleteDocument', { id: d.id });
            await load();
        };

        const size = (n) => !n ? '' : n > 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(n / 1024)) + ' KB';
        const day = (s) => s ? String(s).substring(0, 10) : '';

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Documents']);
                await SecurityManager.validateToken();
                state.uOptions = await options(state.u.relatedType);
                await load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, types, fileRef, onType, load, upload, download, remove, size, day };
    }
};

Vue.createApp(App).mount('#app');
