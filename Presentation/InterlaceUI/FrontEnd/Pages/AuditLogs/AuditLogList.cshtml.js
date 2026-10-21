const App = {
    setup() {
        const state = Vue.reactive({ mainData: [] });
        const mainGridRef = Vue.ref(null);

        const describe = (json) => {
            if (!json) return '';
            try {
                return JSON.parse(json).map(c => `${c.field}: ${c.old ?? '(empty)'} -> ${c.new ?? '(empty)'}`).join('; ');
            } catch (e) {
                return json;
            }
        };

        const methods = {
            populateMainData: async () => {
                const response = await AxiosManager.get('/AuditLog/GetAuditLogList?top=1000', {});
                state.mainData = (response?.data?.content?.data ?? []).map(item => ({
                    ...item,
                    changeText: describe(item.changes),
                    createdAtUtc: new Date(item.createdAtUtc)
                }));
            }
        };

        const mainGrid = {
            obj: null,
            create: (dataSource) => {
                mainGrid.obj = new ej.grids.Grid({
                    height: '460px',
                    dataSource: dataSource,
                    allowFiltering: true,
                    allowSorting: true,
                    allowTextWrap: true,
                    allowResizing: true,
                    allowPaging: true,
                    allowExcelExport: true,
                    filterSettings: { type: 'CheckBox' },
                    sortSettings: { columns: [{ field: 'createdAtUtc', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 20, pageSizes: ['10', '20', '50', '100', '200'] },
                    gridLines: 'Horizontal',
                    columns: [
                        { field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false },
                        { field: 'createdAtUtc', headerText: 'When (UTC)', width: 150, format: 'yyyy-MM-dd HH:mm:ss' },
                        { field: 'userName', headerText: 'User', width: 160 },
                        { field: 'action', headerText: 'Action', width: 100 },
                        { field: 'entityName', headerText: 'Record Type', width: 150 },
                        { field: 'entityId', headerText: 'Record Id', width: 120, visible: false },
                        { field: 'changeText', headerText: 'Changes', width: 460 }
                    ],
                    toolbar: ['ExcelExport', 'Search'],
                    toolbarClick: (args) => {
                        if (args.item.id && args.item.id.endsWith('_excelexport')) {
                            mainGrid.obj.excelExport();
                        }
                    }
                });
                mainGrid.obj.appendTo(mainGridRef.value);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['AuditLogs']);
                await SecurityManager.validateToken();
                await methods.populateMainData();
                mainGrid.create(state.mainData);
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, mainGridRef };
    }
};

Vue.createApp(App).mount('#app');
