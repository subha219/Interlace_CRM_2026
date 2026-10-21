const App = {
    setup() {
        const state = Vue.reactive({
            mainData: [],
            deleteMode: false,
            supportCaseIdLookupData: [],
            mainTitle: null,
            id: '',
            supportCaseId: null,
            comment: '',
            errors: {
                supportCaseId: '',
                comment: ''
            },
            isSubmitting: false
        });

        const mainGridRef = Vue.ref(null);
        const mainModalRef = Vue.ref(null);
        const supportCaseIdRef = Vue.ref(null);

        const validateForm = function () {
            state.errors.supportCaseId = '';
            state.errors.comment = '';

            let isValid = true;

            if (!state.supportCaseId) {
                state.errors.supportCaseId = 'Support Case is required.';
                isValid = false;
            }
            if (!state.comment) {
                state.errors.comment = 'Comment is required.';
                isValid = false;
            }

            return isValid;
        };

        const resetFormState = () => {
            state.id = '';
            state.supportCaseId = null;
            state.comment = '';
            state.errors = {
                supportCaseId: '',
                comment: ''
            };
        };

        const supportCaseIdLookup = {
            obj: null,
            create: () => {
                if (state.supportCaseIdLookupData && Array.isArray(state.supportCaseIdLookupData)) {
                    supportCaseIdLookup.obj = new ej.dropdowns.DropDownList({
                        dataSource: state.supportCaseIdLookupData,
                        fields: { value: 'id', text: 'number' },
                        placeholder: 'Select Support Case',
                        allowFiltering: true,
                        filterBarPlaceholder: 'Search',
                        sortOrder: 'Ascending',
                        filtering: (e) => {
                            e.preventDefaultAction = true;
                            let query = new ej.data.Query();
                            if (e.text !== '') {
                                query = query.where('number', 'startsWith', e.text, true);
                            }
                            e.updateData(state.supportCaseIdLookupData, query);
                        },
                        change: (e) => {
                            state.supportCaseId = e.value;
                        }
                    });
                    supportCaseIdLookup.obj.appendTo(supportCaseIdRef.value);
                }
            },

            refresh: () => {
                if (supportCaseIdLookup.obj) {
                    supportCaseIdLookup.obj.value = state.supportCaseId
                }
            },
        };

        Vue.watch(
            () => state.supportCaseId,
            (newVal, oldVal) => {
                supportCaseIdLookup.refresh();
                state.errors.supportCaseId = '';
            }
        );

        const services = {
            getMainData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCaseComment/GetSupportCaseCommentList', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            createMainData: async (supportCaseId, comment, createdById) => {
                try {
                    const response = await AxiosManager.post('/SupportCaseComment/CreateSupportCaseComment', {
                        supportCaseId, comment, createdById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            updateMainData: async (id, supportCaseId, comment, updatedById) => {
                try {
                    const response = await AxiosManager.post('/SupportCaseComment/UpdateSupportCaseComment', {
                        id, supportCaseId, comment, updatedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            deleteMainData: async (id, deletedById) => {
                try {
                    const response = await AxiosManager.post('/SupportCaseComment/DeleteSupportCaseComment', {
                        id, deletedById
                    });
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getSupportCaseIdLookupData: async () => {
                try {
                    const response = await AxiosManager.get('/SupportCase/GetSupportCaseList', {});
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

                    createdAtUtc: new Date(item.createdAtUtc)
                }));
            },
            populateSupportCaseIdLookupData: async () => {
                const response = await services.getSupportCaseIdLookupData();
                state.supportCaseIdLookupData = response?.data?.content?.data;
            },
            onMainModalHidden: () => {
                state.errors.supportCaseId = '';
                state.errors.comment = '';
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
                        ? await services.createMainData(state.supportCaseId, state.comment, StorageManager.getUserId())
                        : state.deleteMode
                            ? await services.deleteMainData(state.id, StorageManager.getUserId())
                            : await services.updateMainData(state.id, state.supportCaseId, state.comment, StorageManager.getUserId());

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
                await SecurityManager.authorizePage(['SupportCaseComments']);
                await SecurityManager.validateToken();

                await methods.populateMainData();
                await mainGrid.create(state.mainData);

                mainModal.create();
                mainModalRef.value?.addEventListener('hidden.bs.modal', methods.onMainModalHidden);
                await methods.populateSupportCaseIdLookupData();
                supportCaseIdLookup.create();

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
                        { field: 'supportCaseNumber', headerText: 'Support Case', width: 180, minWidth: 180 },
                        { field: 'comment', headerText: 'Comment', width: 400, minWidth: 300 },
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
                        mainGrid.obj.autoFitColumns(['supportCaseNumber', 'comment', 'createdAtUtc']);
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
                            state.mainTitle = 'Add Case Comment';
                            resetFormState();
                            mainModal.obj.show();
                        }

                        if (args.item.id === 'EditCustom') {
                            state.deleteMode = false;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Edit Case Comment';
                                state.id = selectedRecord.id ?? '';
                                state.supportCaseId = selectedRecord.supportCaseId ?? '';
                                state.comment = selectedRecord.comment ?? '';
                                mainModal.obj.show();
                            }
                        }

                        if (args.item.id === 'DeleteCustom') {
                            state.deleteMode = true;
                            if (mainGrid.obj.getSelectedRecords().length) {
                                const selectedRecord = mainGrid.obj.getSelectedRecords()[0];
                                state.mainTitle = 'Delete Case Comment?';
                                state.id = selectedRecord.id ?? '';
                                state.supportCaseId = selectedRecord.supportCaseId ?? '';
                                state.comment = selectedRecord.comment ?? '';
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
            supportCaseIdRef,
            state,
            handler,
        };
    }
};

Vue.createApp(App).mount('#app');
