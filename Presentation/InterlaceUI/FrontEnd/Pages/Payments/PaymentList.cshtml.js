const App = {
    setup() {
        const state = Vue.reactive({
            mainData: [],
            deleteMode: false,
            invoiceIdLookupData: [],
            methodLookupData: [],
            mainTitle: null,
            id: '',
            number: '',
            invoiceId: null,
            paymentDate: '',
            amount: '',
            method: null,
            reference: '',
            description: '',
            errors: {
                invoiceId: '',
                paymentDate: '',
                amount: '',
                method: '',
                reference: '',
                description: ''
            },
            isSubmitting: false
        });

        const mainGridRef = Vue.ref(null);
        const mainModalRef = Vue.ref(null);
        const numberRef = Vue.ref(null);
        const invoiceIdRef = Vue.ref(null);
        const paymentDateRef = Vue.ref(null);
        const amountRef = Vue.ref(null);
        const methodRef = Vue.ref(null);
        const referenceRef = Vue.ref(null);

        const validateForm = function () {
            state.errors.invoiceId = '';
            state.errors.paymentDate = '';
            state.errors.amount = '';
            state.errors.method = '';
            state.errors.reference = '';
            state.errors.description = '';

            let isValid = true;

            if (!state.invoiceId) {
                state.errors.invoiceId = 'Invoice is required.';
                isValid = false;
            }
            if (!state.paymentDate) {
                state.errors.paymentDate = 'Payment Date is required.';
                isValid = false;
            }
            if (state.amount === null || state.amount === '' || isNaN(state.amount)) {
                state.errors.amount = 'Amount is required.';
                isValid = false;
            }
            if (!state.method) {
                state.errors.method = 'Method is required.';
                isValid = false;
            }

            return isValid;
        };

        const resetFormState = () => {
            state.id = '';
            state.number = '';
            state.invoiceId = null;
            state.paymentDate = '';
            state.amount = '';
            state.method = null;
            state.reference = '';
            state.description = '';
            state.errors = {
                invoiceId: '',
                paymentDate: '',
                amount: '',
                method: '',
                reference: '',
                description: ''
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

        const invoiceIdLookup = {
            obj: null,
            create: () => {
                if (state.invoiceIdLookupData && Array.isArray(state.invoiceIdLookupData)) {
                    invoiceIdLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.invoiceIdLookupData,
                        fields: { value: 'id', text: 'number' },
                        placeholder: 'Select Invoice',
                        allowFiltering: true,
                        filterBarPlaceholder: 'Search',
                        sortOrder: 'Ascending',
                        filtering: (e) => {
                            e.preventDefaultAction = true;
                            let query = new ej.data.Query();
                            if (e.text !== '') {
                                query = query.where('number', 'startsWith', e.text, true);
                            }
                            e.updateData(state.invoiceIdLookupData, query);
                        },
                        change: (e) => {
                            state.invoiceId = e.value;
                        }
                    });
                    invoiceIdLookup.obj.appendTo(invoiceIdRef.value);
                }
            },

            refresh: () => {
                if (invoiceIdLookup.obj) {
                    invoiceIdLookup.obj.value = state.invoiceId
                }
            },
        };

        Vue.watch(
            () => state.invoiceId,
            (newVal, oldVal) => {
                invoiceIdLookup.refresh();
                state.errors.invoiceId = '';
            }
        );

        const paymentDatePicker = {
            obj: null,
            create: () => {
                paymentDatePicker.obj = new ej.calendars.DatePicker({
                    placeholder: 'Select Date',
                    format: 'yyyy-MM-dd',
                    value: state.paymentDate ? new Date(state.paymentDate) : null,
                    change: (e) => {
                        state.paymentDate = DateFormatManager.preserveClientDate(e.value);
                    }
                });
                paymentDatePicker.obj.appendTo(paymentDateRef.value);
            },
            refresh: () => {
                if (paymentDatePicker.obj) {
                    paymentDatePicker.obj.value = state.paymentDate ? new Date(state.paymentDate) : null;
                }
            }
        };

        Vue.watch(
            () => state.paymentDate,
            (newVal, oldVal) => {
                paymentDatePicker.refresh();
                state.errors.paymentDate = '';
            }
        );

        const amountNumeric = {
            obj: null,
            create: () => {
                amountNumeric.obj = new ej.inputs.NumericTextBox({
                    placeholder: 'Enter Amount',
                    format: 'n2',
                    min: 0,
                });
                amountNumeric.obj.appendTo(amountRef.value);
            },
            refresh: () => {
                if (amountNumeric.obj) {
                    amountNumeric.obj.value = parseFloat(state.amount) || 0;
                }
            }
        };

        Vue.watch(
            () => state.amount,
            (newVal, oldVal) => {
                amountNumeric.refresh();
                state.errors.amount = '';
            }
        );

        const methodLookup = {
            obj: null,
            create: () => {
                if (state.methodLookupData && Array.isArray(state.methodLookupData)) {
                    methodLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.methodLookupData,
                        fields: { value: 'id', text: 'name' },
                        placeholder: 'Select Method',
                        allowFiltering: false,
                        change: (e) => {
                            state.method = e.value;
                        }
                    });
                    methodLookup.obj.appendTo(methodRef.value);
                }
            },

            refresh: () => {
                if (methodLookup.obj) {
                    methodLookup.obj.value = state.method
                }
            },
        };

        Vue.watch(
            () => state.method,
            (newVal, oldVal) => {
                methodLookup.refresh();
                state.errors.method = '';
            }
        );

        const referenceText = {
            obj: null,
            create: () => {
                referenceText.obj = new ej.inputs.TextBox({
                    placeholder: 'Enter Reference',
                });
                referenceText.obj.appendTo(referenceRef.value);
            },
            refresh: () => {
                if (referenceText.obj) {
                    referenceText.obj.value = state.reference;
                }
            }
        };

        Vue.watch(
            () => state.reference,
            (newVal, oldVal) => {
                referenceText.refresh();
                state.errors.reference = '';
            }
        );

        const services = {
            getMainData: async () => {
                try {
                    const response = await AxiosManager.get('/Payment/GetPaymentList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            createMainData: async (invoiceId, paymentDate, amount, method, reference, description, createdById) => {
                try {
                    const response = await AxiosManager.post('/Payment/CreatePayment', {
                        invoiceId, paymentDate, amount, method, reference, description, createdById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            updateMainData: async (id, invoiceId, paymentDate, amount, method, reference, description, updatedById) => {
                try {
                    const response = await AxiosManager.post('/Payment/UpdatePayment', {
                        id, invoiceId, paymentDate, amount, method, reference, description, updatedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            deleteMainData: async (id, deletedById) => {
                try {
                    const response = await AxiosManager.post('/Payment/DeletePayment', {
                        id, deletedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getInvoiceIdLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/Invoice/GetInvoiceList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getMethodLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/Payment/GetPaymentMethodList', {});
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
                    paymentDate: item.paymentDate ? new Date(item.paymentDate) : null,
                    createdAtUtc: new Date(item.createdAtUtc)
                }));
            },
            populateInvoiceIdLookupData: async () => {
                const response = await services.getInvoiceIdLookupData();
                state.invoiceIdLookupData = response?.data?.content?.data;
            },
            populateMethodLookupData: async () => {
                const response = await services.getMethodLookupData();
                state.methodLookupData = response?.data?.content?.data;
            },
            onMainModalHidden: () => {
                state.errors.invoiceId = '';
                state.errors.paymentDate = '';
                state.errors.amount = '';
                state.errors.method = '';
                state.errors.reference = '';
                state.errors.description = '';
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
                        ? await services.createMainData(state.invoiceId, state.paymentDate, state.amount, state.method, state.reference, state.description, StorageManager.getUserId())
                        : state.deleteMode
                            ? await services.deleteMainData(state.id, StorageManager.getUserId())
                            : await services.updateMainData(state.id, state.invoiceId, state.paymentDate, state.amount, state.method, state.reference, state.description, StorageManager.getUserId());

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
                await SecurityManager.authorizePage(['Payments']);
                await SecurityManager.validateToken();

                await methods.populateMainData();
                await mainGrid.create(state.mainData);

                mainModal.create();
                mainModalRef.value?.addEventListener('hidden.bs.modal', methods.onMainModalHidden);
                await methods.populateInvoiceIdLookupData();
                await methods.populateMethodLookupData();
                numberText.create();
                invoiceIdLookup.create();
                paymentDatePicker.create();
                amountNumeric.create();
                methodLookup.create();
                referenceText.create();

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
                        { field: 'invoiceNumber', headerText: 'Invoice', width: 180, minWidth: 180 },
                        { field: 'paymentDate', headerText: 'Payment Date', width: 150, format: 'yyyy-MM-dd' },
                        { field: 'amount', headerText: 'Amount', width: 120, minWidth: 120, format: 'N2' },
                        { field: 'methodName', headerText: 'Method', width: 150, minWidth: 150 },
                        { field: 'reference', headerText: 'Reference', width: 200, minWidth: 200 },
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
                        mainGrid.obj.autoFitColumns(['number', 'invoiceNumber', 'paymentDate', 'amount', 'methodName', 'reference', 'createdAtUtc']);
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
                            state.mainTitle = 'Add Payment';
                            resetFormState();
                            mainModal.obj.show();
                        }

                        if (args.item.id === 'EditCustom') {
                            state.deleteMode = false;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Edit Payment';
                                state.id = selectedRecord.id ?? '';
                                state.number = selectedRecord.number ?? '';
                                state.invoiceId = selectedRecord.invoiceId ?? '';
                                state.paymentDate = selectedRecord.paymentDate ? new Date(selectedRecord.paymentDate) : null;
                                state.amount = selectedRecord.amount ?? '';
                                state.method = String(selectedRecord.method ?? '');
                                state.reference = selectedRecord.reference ?? '';
                                state.description = selectedRecord.description ?? '';
                                mainModal.obj.show();
                            }
                        }

                        if (args.item.id === 'DeleteCustom') {
                            state.deleteMode = true;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Delete Payment?';
                                state.id = selectedRecord.id ?? '';
                                state.number = selectedRecord.number ?? '';
                                state.invoiceId = selectedRecord.invoiceId ?? '';
                                state.paymentDate = selectedRecord.paymentDate ? new Date(selectedRecord.paymentDate) : null;
                                state.amount = selectedRecord.amount ?? '';
                                state.method = String(selectedRecord.method ?? '');
                                state.reference = selectedRecord.reference ?? '';
                                state.description = selectedRecord.description ?? '';
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
            invoiceIdRef,
            paymentDateRef,
            amountRef,
            methodRef,
            referenceRef,
            state,
            handler,
        };
    }
};

Vue.createApp(App).mount('#app');
