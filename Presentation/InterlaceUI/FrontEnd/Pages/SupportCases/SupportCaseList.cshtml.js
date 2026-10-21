const App = {
    setup() {
        const state = Vue.reactive({
            mainData: [],
            deleteMode: false,
            statusLookupData: [],
            priorityLookupData: [],
            channelLookupData: [],
            customerIdLookupData: [],
            salesRepresentativeIdLookupData: [],
            mainTitle: null,
            id: '',
            number: '',
            title: '',
            status: null,
            priority: null,
            channel: null,
            customerId: null,
            salesRepresentativeId: null,
            dueDate: '',
            description: '',
            resolution: '',
            errors: {
                title: '',
                status: '',
                priority: '',
                channel: '',
                customerId: '',
                salesRepresentativeId: '',
                dueDate: '',
                description: '',
                resolution: ''
            },
            isSubmitting: false
        });

        const mainGridRef = Vue.ref(null);
        const mainModalRef = Vue.ref(null);
        const numberRef = Vue.ref(null);
        const titleRef = Vue.ref(null);
        const statusRef = Vue.ref(null);
        const priorityRef = Vue.ref(null);
        const channelRef = Vue.ref(null);
        const customerIdRef = Vue.ref(null);
        const salesRepresentativeIdRef = Vue.ref(null);
        const dueDateRef = Vue.ref(null);

        const validateForm = function () {
            state.errors.title = '';
            state.errors.status = '';
            state.errors.priority = '';
            state.errors.channel = '';
            state.errors.customerId = '';
            state.errors.salesRepresentativeId = '';
            state.errors.dueDate = '';
            state.errors.description = '';
            state.errors.resolution = '';

            let isValid = true;

            if (!state.title) {
                state.errors.title = 'Title is required.';
                isValid = false;
            }
            if (!state.status) {
                state.errors.status = 'Status is required.';
                isValid = false;
            }
            if (!state.priority) {
                state.errors.priority = 'Priority is required.';
                isValid = false;
            }
            if (!state.customerId) {
                state.errors.customerId = 'Customer is required.';
                isValid = false;
            }

            return isValid;
        };

        const resetFormState = () => {
            state.id = '';
            state.number = '';
            state.title = '';
            state.status = null;
            state.priority = null;
            state.channel = null;
            state.customerId = null;
            state.salesRepresentativeId = null;
            state.dueDate = '';
            state.description = '';
            state.resolution = '';
            state.errors = {
                title: '',
                status: '',
                priority: '',
                channel: '',
                customerId: '',
                salesRepresentativeId: '',
                dueDate: '',
                description: '',
                resolution: ''
            };
        };

        const numberText = {
            obj: null,
            create: () => {
                numberText.obj = new ej.inputs.TextBox({
                    placeholder: '[auto]',
                });
                numberText.obj.appendTo(numberRef.value);
            }
        };

        const titleText = {
            obj: null,
            create: () => {
                titleText.obj = new ej.inputs.TextBox({
                    placeholder: 'Enter Title',
                });
                titleText.obj.appendTo(titleRef.value);
            },
            refresh: () => {
                if (titleText.obj) {
                    titleText.obj.value = state.title;
                }
            }
        };

        Vue.watch(
            () => state.title,
            (newVal, oldVal) => {
                titleText.refresh();
                state.errors.title = '';
            }
        );

        const statusLookup = {
            obj: null,
            create: () => {
                if (state.statusLookupData && Array.isArray(state.statusLookupData)) {
                    statusLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.statusLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Status',
                        allowFiltering: false,
                        change: (e) => {
                            state.status = e.value;
                        }
                    });
                    statusLookup.obj.appendTo(statusRef.value);
                }
            },

            refresh: () => {
                if (statusLookup.obj) {
                    statusLookup.obj.value = state.status
                }
            },
        };

        Vue.watch(
            () => state.status,
            (newVal, oldVal) => {
                statusLookup.refresh();
                state.errors.status = '';
            }
        );

        const priorityLookup = {
            obj: null,
            create: () => {
                if (state.priorityLookupData && Array.isArray(state.priorityLookupData)) {
                    priorityLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.priorityLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Priority',
                        allowFiltering: false,
                        change: (e) => {
                            state.priority = e.value;
                        }
                    });
                    priorityLookup.obj.appendTo(priorityRef.value);
                }
            },

            refresh: () => {
                if (priorityLookup.obj) {
                    priorityLookup.obj.value = state.priority
                }
            },
        };

        Vue.watch(
            () => state.priority,
            (newVal, oldVal) => {
                priorityLookup.refresh();
                state.errors.priority = '';
            }
        );

        const channelLookup = {
            obj: null,
            create: () => {
                if (state.channelLookupData && Array.isArray(state.channelLookupData)) {
                    channelLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.channelLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Channel',
                        showClearButton: true,
                        allowFiltering: false,
                        change: (e) => {
                            state.channel = e.value;
                        }
                    });
                    channelLookup.obj.appendTo(channelRef.value);
                }
            },

            refresh: () => {
                if (channelLookup.obj) {
                    channelLookup.obj.value = state.channel
                }
            },
        };

        Vue.watch(
            () => state.channel,
            (newVal, oldVal) => {
                channelLookup.refresh();
                state.errors.channel = '';
            }
        );

        const customerIdLookup = {
            obj: null,
            create: () => {
                if (state.customerIdLookupData && Array.isArray(state.customerIdLookupData)) {
                    customerIdLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.customerIdLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Customer',
                        allowFiltering: true,
                        filterBarPlaceholder: 'Search',
                        sortOrder: 'Ascending',
                        filtering: (e) => {
                            e.preventDefaultAction = true;
                            let query = new ej.data.Query();
                            if (e.text !== '') {
                                query = query.where('name', 'startsWith', e.text, true);
                            }
                            e.updateData(state.customerIdLookupData, query);
                        },
                        change: (e) => {
                            state.customerId = e.value;
                        }
                    });
                    customerIdLookup.obj.appendTo(customerIdRef.value);
                }
            },

            refresh: () => {
                if (customerIdLookup.obj) {
                    customerIdLookup.obj.value = state.customerId
                }
            },
        };

        Vue.watch(
            () => state.customerId,
            (newVal, oldVal) => {
                customerIdLookup.refresh();
                state.errors.customerId = '';
            }
        );

        const salesRepresentativeIdLookup = {
            obj: null,
            create: () => {
                if (state.salesRepresentativeIdLookupData && Array.isArray(state.salesRepresentativeIdLookupData)) {
                    salesRepresentativeIdLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.salesRepresentativeIdLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Assigned To',
                        showClearButton: true,
                        allowFiltering: true,
                        filterBarPlaceholder: 'Search',
                        sortOrder: 'Ascending',
                        filtering: (e) => {
                            e.preventDefaultAction = true;
                            let query = new ej.data.Query();
                            if (e.text !== '') {
                                query = query.where('name', 'startsWith', e.text, true);
                            }
                            e.updateData(state.salesRepresentativeIdLookupData, query);
                        },
                        change: (e) => {
                            state.salesRepresentativeId = e.value;
                        }
                    });
                    salesRepresentativeIdLookup.obj.appendTo(salesRepresentativeIdRef.value);
                }
            },

            refresh: () => {
                if (salesRepresentativeIdLookup.obj) {
                    salesRepresentativeIdLookup.obj.value = state.salesRepresentativeId
                }
            },
        };

        Vue.watch(
            () => state.salesRepresentativeId,
            (newVal, oldVal) => {
                salesRepresentativeIdLookup.refresh();
                state.errors.salesRepresentativeId = '';
            }
        );

        const dueDatePicker = {
            obj: null,
            create: () => {
                dueDatePicker.obj = new ej.calendars.DatePicker({
                    placeholder: 'Select Date',
                    format: 'yyyy-MM-dd',
                    value: state.dueDate ? new Date(state.dueDate) : null,
                    change: (e) => {
                        state.dueDate = DateFormatManager.preserveClientDate(e.value);
                    }
                });
                dueDatePicker.obj.appendTo(dueDateRef.value);
            },
            refresh: () => {
                if (dueDatePicker.obj) {
                    dueDatePicker.obj.value = state.dueDate ? new Date(state.dueDate) : null;
                }
            }
        };

        Vue.watch(
            () => state.dueDate,
            (newVal, oldVal) => {
                dueDatePicker.refresh();
                state.errors.dueDate = '';
            }
        );

        const services = {
            getMainData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCase/GetSupportCaseList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            createMainData: async (title, status, priority, channel, customerId, salesRepresentativeId, dueDate, description, resolution, createdById) => {
                try {
                    const response = await AxiosManager.post('/SupportCase/CreateSupportCase', {
                        title, status, priority, channel, customerId, salesRepresentativeId, dueDate, description, resolution, createdById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            updateMainData: async (id, title, status, priority, channel, customerId, salesRepresentativeId, dueDate, description, resolution, updatedById) => {
                try {
                    const response = await AxiosManager.post('/SupportCase/UpdateSupportCase', {
                        id, title, status, priority, channel, customerId, salesRepresentativeId, dueDate, description, resolution, updatedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            deleteMainData: async (id, deletedById) => {
                try {
                    const response = await AxiosManager.post('/SupportCase/DeleteSupportCase', {
                        id, deletedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getStatusLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCase/GetSupportCaseStatusList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getPriorityLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCase/GetSupportCasePriorityList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getChannelLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCase/GetSupportCaseChannelList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getCustomerIdLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/Customer/GetCustomerList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getSalesRepresentativeIdLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/SalesRepresentative/GetSalesRepresentativeList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            }
        };

        const methods = {
            populateMainData: async () => {
                const response = await services.getMainData();
                state.mainData = response?.data?.content?.data.map(item => ({
                    ...item,
                    dueDate: item.dueDate ? new Date(item.dueDate) : null,
                    resolvedAtUtc: item.resolvedAtUtc ? new Date(item.resolvedAtUtc) : null,
                    createdAtUtc: new Date(item.createdAtUtc)
                }));
            },
            populateStatusLookupData: async () => {
                const response = await services.getStatusLookupData();
                state.statusLookupData = response?.data?.content?.data;
            },
            populatePriorityLookupData: async () => {
                const response = await services.getPriorityLookupData();
                state.priorityLookupData = response?.data?.content?.data;
            },
            populateChannelLookupData: async () => {
                const response = await services.getChannelLookupData();
                state.channelLookupData = response?.data?.content?.data;
            },
            populateCustomerIdLookupData: async () => {
                const response = await services.getCustomerIdLookupData();
                state.customerIdLookupData = response?.data?.content?.data;
            },
            populateSalesRepresentativeIdLookupData: async () => {
                const response = await services.getSalesRepresentativeIdLookupData();
                state.salesRepresentativeIdLookupData = response?.data?.content?.data;
            },
            onMainModalHidden: () => {
                state.errors.title = '';
                state.errors.status = '';
                state.errors.priority = '';
                state.errors.channel = '';
                state.errors.customerId = '';
                state.errors.salesRepresentativeId = '';
                state.errors.dueDate = '';
                state.errors.description = '';
                state.errors.resolution = '';
            }
        };

        const handler = {
            handleSubmit: async function () {
                try {

                    state.isSubmitting = true;
                    await new Promise(resolve => setTimeout(resolve, 300));

                    if (!validateForm()) {
                        return;
                    }

                    const response = state.id === ''
                        ? await services.createMainData(state.title, state.status, state.priority, state.channel, state.customerId, state.salesRepresentativeId, state.dueDate, state.description, state.resolution, StorageManager.getUserId())
                        : state.deleteMode
                            ? await services.deleteMainData(state.id, StorageManager.getUserId())
                            : await services.updateMainData(state.id, state.title, state.status, state.priority, state.channel, state.customerId, state.salesRepresentativeId, state.dueDate, state.description, state.resolution, StorageManager.getUserId());

                    if (response.data.code === 200) {
                        await methods.populateMainData();
                        mainGrid.refresh();
                        Swal.fire({
                            icon: 'success',
                            title: state.deleteMode ? 'Delete Successful' : 'Save Successful',
                            text: 'Form will be closed...',
                            timer: 2000,
                            showConfirmButton: false
                        });
                        setTimeout(() => {
                            mainModal.obj.hide();
                        }, 2000);
                    } else {
                        Swal.fire({
                            icon: 'error',
                            title: state.deleteMode ? 'Delete Failed' : 'Save Failed',
                            text: response.data.message ?? 'Please check your data.',
                            confirmButtonText: 'Try Again'
                        });
                    }

                } catch (error) {
                    Swal.fire({
                        icon: 'error',
                        title: 'An Error Occurred',
                        text: error.response?.data?.message ?? 'Please try again.',
                        confirmButtonText: 'OK'
                    });
                } finally {
                    state.isSubmitting = false;
                }
            },
        };


        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['SupportCases']);
                await SecurityManager.validateToken();

                await methods.populateMainData();
                await mainGrid.create(state.mainData);

                mainModal.create();
                mainModalRef.value?.addEventListener('hidden.bs.modal', methods.onMainModalHidden);
                await methods.populateStatusLookupData();
                await methods.populatePriorityLookupData();
                await methods.populateChannelLookupData();
                await methods.populateCustomerIdLookupData();
                await methods.populateSalesRepresentativeIdLookupData();
                numberText.create();
                titleText.create();
                statusLookup.create();
                priorityLookup.create();
                channelLookup.create();
                customerIdLookup.create();
                salesRepresentativeIdLookup.create();
                dueDatePicker.create();

            } catch (e) {
                console.error('page init error:', e);
            } finally {
                hideSpinnerAndShowContent();
            }
        });

        Vue.onUnmounted(() => {
            mainModalRef.value?.removeEventListener('hidden.bs.modal', methods.onMainModalHidden);
        });

        const mainGrid = {
            obj: null,
            create: async (dataSource) => {
                mainGrid.obj = new ej.grids.Grid({
                    height: '240px',
                    dataSource: dataSource,
                    allowFiltering: true,
                    allowSorting: true,
                    allowSelection: true,
                    allowGrouping: true,
                    allowTextWrap: true,
                    allowResizing: true,
                    allowPaging: true,
                    allowExcelExport: true,
                    filterSettings: { type: 'CheckBox' },
                    sortSettings: { columns: [{ field: 'createdAtUtc', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 50, pageSizes: ["10", "20", "50", "100", "200", "All"] },
                    selectionSettings: { persistSelection: true, type: 'Single' },
                    autoFit: true,
                    showColumnMenu: true,
                    gridLines: 'Horizontal',
                    columns: [
                        { type: 'checkbox', width: 60 },
                        {
                            field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false
                        },
                        { field: 'number', headerText: 'Number', width: 150, minWidth: 150 },
                        { field: 'title', headerText: 'Title', width: 200, minWidth: 200 },
                        { field: 'statusName', headerText: 'Status', width: 150, minWidth: 150 },
                        { field: 'priorityName', headerText: 'Priority', width: 150, minWidth: 150 },
                        { field: 'channelName', headerText: 'Channel', width: 150, minWidth: 150 },
                        { field: 'customerName', headerText: 'Customer', width: 180, minWidth: 180 },
                        { field: 'salesRepresentativeName', headerText: 'Assigned To', width: 180, minWidth: 180 },
                        { field: 'dueDate', headerText: 'SLA Due Date', width: 150, format: 'yyyy-MM-dd' },
                        { field: 'isOverdue', headerText: 'Overdue', width: 100, minWidth: 100 },
                        { field: 'resolvedAtUtc', headerText: 'Resolved At', width: 150, format: 'yyyy-MM-dd HH:mm' },
                        { field: 'createdAtUtc', headerText: 'Created At UTC', width: 150, format: 'yyyy-MM-dd HH:mm' }
                    ],
                    toolbar: [
                        'ExcelExport', 'Search',
                        { type: 'Separator' },
                        { text: 'Add', tooltipText: 'Add', prefixIcon: 'e-add', id: 'AddCustom' },
                        { text: 'Edit', tooltipText: 'Edit', prefixIcon: 'e-edit', id: 'EditCustom' },
                        { text: 'Delete', tooltipText: 'Delete', prefixIcon: 'e-delete', id: 'DeleteCustom' },
                        { type: 'Separator' },
                    ],
                    beforeDataBound: () => { },
                    dataBound: function () {
                        mainGrid.obj.toolbarModule.enableItems(['EditCustom', 'DeleteCustom'], false);
                        mainGrid.obj.autoFitColumns(['number', 'title', 'statusName', 'priorityName', 'channelName', 'customerName', 'salesRepresentativeName', 'dueDate', 'isOverdue', 'resolvedAtUtc', 'createdAtUtc']);
                    },
                    excelExportComplete: () => { },
                    rowSelected: () => {
                        if (mainGrid.obj.getSelectedRecords().length == 1) {
                            mainGrid.obj.toolbarModule.enableItems(['EditCustom', 'DeleteCustom'], true);
                        } else {
                            mainGrid.obj.toolbarModule.enableItems(['EditCustom', 'DeleteCustom'], false);
                        }
                    },
                    rowDeselected: () => {
                        if (mainGrid.obj.getSelectedRecords().length == 1) {
                            mainGrid.obj.toolbarModule.enableItems(['EditCustom', 'DeleteCustom'], true);
                        } else {
                            mainGrid.obj.toolbarModule.enableItems(['EditCustom', 'DeleteCustom'], false);
                        }
                    },
                    rowSelecting: () => {
                        if (mainGrid.obj.getSelectedRecords().length) {
                            mainGrid.obj.clearSelection();
                        }
                    },
                    toolbarClick: async (args) => {
                        if (args.item.id === 'MainGrid_excelexport') {
                            mainGrid.obj.excelExport();
                        }

                        if (args.item.id === 'AddCustom') {
                            state.deleteMode = false;
                            state.mainTitle = 'Add Support Case';
                            resetFormState();
                            mainModal.obj.show();
                        }

                        if (args.item.id === 'EditCustom') {
                            state.deleteMode = false;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Edit Support Case';
                                state.id = selectedRecord.id ?? '';
                                state.number = selectedRecord.number ?? '';
                                state.title = selectedRecord.title ?? '';
                                state.status = String(selectedRecord.status ?? '');
                                state.priority = String(selectedRecord.priority ?? '');
                                state.channel = String(selectedRecord.channel ?? '');
                                state.customerId = selectedRecord.customerId ?? '';
                                state.salesRepresentativeId = selectedRecord.salesRepresentativeId ?? '';
                                state.dueDate = selectedRecord.dueDate ? new Date(selectedRecord.dueDate) : null;
                                state.description = selectedRecord.description ?? '';
                                state.resolution = selectedRecord.resolution ?? '';
                                mainModal.obj.show();
                            }
                        }

                        if (args.item.id === 'DeleteCustom') {
                            state.deleteMode = true;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Delete Support Case?';
                                state.id = selectedRecord.id ?? '';
                                state.number = selectedRecord.number ?? '';
                                state.title = selectedRecord.title ?? '';
                                state.status = String(selectedRecord.status ?? '');
                                state.priority = String(selectedRecord.priority ?? '');
                                state.channel = String(selectedRecord.channel ?? '');
                                state.customerId = selectedRecord.customerId ?? '';
                                state.salesRepresentativeId = selectedRecord.salesRepresentativeId ?? '';
                                state.dueDate = selectedRecord.dueDate ? new Date(selectedRecord.dueDate) : null;
                                state.description = selectedRecord.description ?? '';
                                state.resolution = selectedRecord.resolution ?? '';
                                mainModal.obj.show();
                            }
                        }
                    }
                });

                mainGrid.obj.appendTo(mainGridRef.value);
            },
            refresh: () => {
                mainGrid.obj.setProperties({ dataSource: state.mainData });
            }
        };


        const mainModal = {
            obj: null,
            create: () => {
                mainModal.obj = new bootstrap.Modal(mainModalRef.value, {
                    backdrop: 'static',
                    keyboard: false
                });
            }
        };

        return {
            mainGridRef,
            mainModalRef,
            numberRef,
            titleRef,
            statusRef,
            priorityRef,
            channelRef,
            customerIdRef,
            salesRepresentativeIdRef,
            dueDateRef,
            state,
            handler,
        };
    }
};

Vue.createApp(App).mount('#app');
