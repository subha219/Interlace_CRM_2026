const App = {
    setup() {
        const state = Vue.reactive({ mainData: [] });
        const mainGridRef = Vue.ref(null);

        const methods = {
            populateMainData: async () => {
                const response = await AxiosManager.get('/Notification/GetNotificationList?top=500', {});
                state.mainData = (response?.data?.content?.data ?? []).map(item => ({
                    ...item,
                    createdAtUtc: new Date(item.createdAtUtc)
                }));
            }
        };

        const mainGrid = {
            obj: null,
            create: (dataSource) => {
                mainGrid.obj = new ej.grids.Grid({
                    height: '400px',
                    dataSource: dataSource,
                    allowFiltering: true,
                    allowSorting: true,
                    allowSelection: true,
                    allowTextWrap: true,
                    allowPaging: true,
                    filterSettings: { type: 'CheckBox' },
                    sortSettings: { columns: [{ field: 'createdAtUtc', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 20, pageSizes: ['10', '20', '50', '100'] },
                    selectionSettings: { type: 'Single' },
                    gridLines: 'Horizontal',
                    columns: [
                        { field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false },
                        { field: 'createdAtUtc', headerText: 'When', width: 150, format: 'yyyy-MM-dd HH:mm' },
                        { field: 'type', headerText: 'Type', width: 100 },
                        { field: 'title', headerText: 'Title', width: 200 },
                        { field: 'message', headerText: 'Message', width: 380 },
                        { field: 'isRead', headerText: 'Read', width: 90, type: 'boolean', displayAsCheckBox: true }
                    ],
                    toolbar: [
                        { text: 'Open', tooltipText: 'Open the related page', id: 'OpenCustom' },
                        { text: 'Mark all as read', tooltipText: 'Mark all as read', id: 'MarkAllCustom' },
                        'Search'
                    ],
                    toolbarClick: async (args) => {
                        if (args.item.id === 'MarkAllCustom') {
                            await AxiosManager.post('/Notification/MarkNotificationRead', { markAll: true });
                            await methods.populateMainData();
                            mainGrid.obj.setProperties({ dataSource: state.mainData });
                        }
                        if (args.item.id === 'OpenCustom') {
                            const rows = mainGrid.obj.getSelectedRecords();
                            if (rows.length) {
                                await AxiosManager.post('/Notification/MarkNotificationRead', { id: rows[0].id });
                                if (rows[0].url) { window.location.href = rows[0].url; }
                            }
                        }
                    }
                });
                mainGrid.obj.appendTo(mainGridRef.value);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Notifications']);
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
