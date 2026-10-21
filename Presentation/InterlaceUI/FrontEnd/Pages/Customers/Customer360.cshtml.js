const App = {
    setup() {
        const state = Vue.reactive({ customers: [], data: null, loading: false, error: '', tab: 'invoices' });
        const selectRef = Vue.ref(null);
        let dropdown = null;

        const tabs = [
            { key: 'invoices', label: 'Invoices' },
            { key: 'quotations', label: 'Quotations' },
            { key: 'orders', label: 'Orders' },
            { key: 'cases', label: 'Cases' }
        ];

        const methods = {
            money: (v) => NumberFormatManager.formatToLocale(v ?? 0),
            day: (d) => d ? new Date(d).toISOString().substring(0, 10) : '',
            healthPill: (l) => l === 'Healthy' ? 'pill-good' : l === 'Needs attention' ? 'pill-medium' : 'pill-high',
            tIcon: (t) => ({ Quotation: 'fa-file-signature', Order: 'fa-box', Invoice: 'fa-file-invoice-dollar', Payment: 'fa-money-bill-wave', Case: 'fa-headset' }[t] ?? 'fa-circle'),
            count: (key) => state.data ? ({ invoices: state.data.invoices, quotations: state.data.quotations, orders: state.data.orders, cases: state.data.cases }[key] ?? []).length : 0,
            draft: (number) => window.CrmShell?.openAssistant('Draft a payment reminder for ' + number),
            loadCustomer: async (id) => {
                if (!id) return;
                state.loading = true;
                state.error = '';
                try {
                    const response = await AxiosManager.get('/Customer360/GetCustomer360?customerId=' + encodeURIComponent(id), {});
                    state.data = response?.data?.content ?? null;
                    const url = new URL(window.location.href);
                    url.searchParams.set('id', id);
                    window.history.replaceState({}, '', url);
                } catch (e) {
                    state.error = 'Could not load this customer.';
                    state.data = null;
                } finally {
                    state.loading = false;
                }
            }
        };

        const docs = Vue.computed(() => {
            if (!state.data) return [];
            return { invoices: state.data.invoices, quotations: state.data.quotations, orders: state.data.orders }[state.tab] ?? [];
        });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Customers']);
                await SecurityManager.validateToken();
                const response = await AxiosManager.get('/Customer/GetCustomerList', {});
                state.customers = (response?.data?.content?.data ?? []).map(x => ({ id: x.id, name: `${x.name ?? ''} (${x.number ?? ''})` }));
                dropdown = new ej.dropdowns.DropDownList({
                    dataSource: state.customers,
                    fields: { value: 'id', text: 'name' },
                    placeholder: 'Select a customer',
                    allowFiltering: true,
                    filterType: 'Contains',
                    popupHeight: '320px',
                    change: (e) => methods.loadCustomer(e.value)
                });
                dropdown.appendTo(selectRef.value);
                const id = new URLSearchParams(window.location.search).get('id');
                if (id) {
                    dropdown.value = id;
                    await methods.loadCustomer(id);
                }
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, selectRef, tabs, docs, ...methods };
    }
};

Vue.createApp(App).mount('#app');
