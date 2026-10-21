const App = {
    setup() {
        const types = [
            { key: 'Customers', label: 'Customers', endpoint: '/Import/ImportCustomers', file: 'customers-template.csv',
              columns: 'Name (required), Email, Phone, Street, City, State, Zip, Country, Website, WhatsApp, Description, Group, Category. Group and category are matched by name; otherwise the first one is used.',
              template: 'Name,Email,Phone,Street,City,State,Zip,Country,Website,WhatsApp,Description,Group,Category\r\nAcme Trading,hello@acme.example,+1 555 0100,12 Main St,Springfield,IL,62701,USA,https://acme.example,,Key account,,\r\n' },
            { key: 'Leads', label: 'Leads', endpoint: '/Import/ImportLeads', file: 'leads-template.csv',
              columns: 'Title or Company (one required), Email, Phone, Website, Street, City, State, Zip, Country, Source, Tags, Consent (yes/no), Amount, Notes. New leads start in Prospecting.',
              template: 'Company,Title,Email,Phone,Website,City,Country,Source,Tags,Consent,Amount,Notes\r\nGlobex Ltd,Website redesign,info@globex.example,+44 20 7946 0000,https://globex.example,London,UK,Website,"vip, 2026",yes,15000,Met at expo\r\n' },
            { key: 'Contacts', label: 'Contacts', endpoint: '/Import/ImportContacts', file: 'contacts-template.csv',
              columns: 'Name and Customer (both required; customer is matched by its name or number), Email, Phone, Job Title, Consent (yes/no), Notes.',
              template: 'Name,Customer,Email,Phone,Job Title,Consent,Notes\r\nJane Smith,Acme Trading,jane@acme.example,+1 555 0101,Purchasing Manager,yes,\r\n' }
        ];
        const state = Vue.reactive({ type: 'Customers', csv: '', fileName: '', busy: false, checked: false, result: null });
        const current = Vue.computed(() => types.find(t => t.key === state.type));

        const methods = {
            onFile: (event) => {
                const file = event.target.files?.[0];
                state.result = null;
                state.checked = false;
                if (!file) { state.csv = ''; state.fileName = ''; return; }
                state.fileName = file.name;
                const reader = new FileReader();
                reader.onload = () => { state.csv = String(reader.result ?? ''); };
                reader.readAsText(file);
            },
            run: async (preview) => {
                state.busy = true;
                try {
                    const response = await AxiosManager.post(current.value.endpoint, {
                        csv: state.csv,
                        preview: preview,
                        createdById: StorageManager.getUserId()
                    });
                    state.result = response?.data?.content ?? null;
                    state.checked = preview;
                    if (!preview) {
                        Swal.fire({ icon: 'success', title: 'Import finished', text: `${state.result.imported} record(s) imported.`, timer: 2200, showConfirmButton: false });
                    }
                } catch (error) {
                    Swal.fire({ icon: 'error', title: 'Import failed', text: error.response?.data?.message ?? 'Please check the file and try again.', confirmButtonText: 'OK' });
                } finally {
                    state.busy = false;
                }
            },
            setType: (key) => {
                state.type = key;
                state.csv = '';
                state.fileName = '';
                state.result = null;
                state.checked = false;
                const input = document.querySelector('input[type=file]');
                if (input) { input.value = ''; }
            },
            downloadTemplate: () => {
                const csv = current.value.template;
                const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = current.value.file;
                a.click();
                URL.revokeObjectURL(a.href);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Imports']);
                await SecurityManager.validateToken();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, types, current, ...methods };
    }
};

Vue.createApp(App).mount('#app');
