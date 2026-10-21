const App = {
    setup() {
        const state = Vue.reactive({
            months: 6,
            data: { openAmount: 0, weightedAmount: 0, wonAmount: 0, wonCount: 0, lostCount: 0, openCount: 0, winRate: 0, averageDealSize: 0, byStage: [], byMonth: [], byTeam: [] }
        });

        const stageChartRef = Vue.ref(null);
        const monthChartRef = Vue.ref(null);
        const teamGridRef = Vue.ref(null);
        let stageChart = null, monthChart = null, teamGrid = null;

        const format = (v) => NumberFormatManager.formatToLocale(v ?? 0);

        const buildChart = (el, title, rows, old) => {
            if (old) { old.destroy(); }
            const chart = new ej.charts.Chart({
                primaryXAxis: { valueType: 'Category', majorGridLines: { width: 0 }, labelIntersectAction: 'Rotate45' },
                chartArea: { border: { width: 0 } },
                primaryYAxis: { title: 'Amount', labelFormat: '{value}', lineStyle: { width: 0 }, majorTickLines: { width: 0 } },
                series: [
                    { type: 'Column', dataSource: rows, xName: 'name', yName: 'amount', name: 'Pipeline', columnSpacing: 0.1 },
                    { type: 'Column', dataSource: rows, xName: 'name', yName: 'weighted', name: 'Weighted', columnSpacing: 0.1 }
                ],
                title: title,
                tooltip: { enable: true, shared: true },
                legendSettings: { enableHighlight: true },
                palettes: ['#009CFF', '#28a745']
            }, el);
            return chart;
        };

        const methods = {
            load: async () => {
                const response = await AxiosManager.get('/Lead/GetSalesForecast?monthsAhead=' + state.months, {});
                state.data = response?.data?.content ?? state.data;
                stageChart = buildChart(stageChartRef.value, 'Pipeline by stage', state.data.byStage, stageChart);
                monthChart = buildChart(monthChartRef.value, 'Expected closing by month', state.data.byMonth, monthChart);
                if (teamGrid) { teamGrid.destroy(); }
                teamGrid = new ej.grids.Grid({
                    dataSource: state.data.byTeam,
                    allowSorting: true,
                    allowExcelExport: true,
                    gridLines: 'Horizontal',
                    columns: [
                        { field: 'name', headerText: 'Sales Team', width: 180 },
                        { field: 'openCount', headerText: 'Open Leads', width: 110, textAlign: 'Right' },
                        { field: 'openAmount', headerText: 'Open Amount', width: 140, format: 'N2', textAlign: 'Right' },
                        { field: 'weighted', headerText: 'Weighted', width: 140, format: 'N2', textAlign: 'Right' },
                        { field: 'wonCount', headerText: 'Won', width: 80, textAlign: 'Right' },
                        { field: 'wonAmount', headerText: 'Won Amount', width: 140, format: 'N2', textAlign: 'Right' },
                        { field: 'lostCount', headerText: 'Lost', width: 80, textAlign: 'Right' },
                        { field: 'winRate', headerText: 'Win Rate %', width: 110, format: 'N1', textAlign: 'Right' }
                    ]
                }, teamGridRef.value);
            },
            exportExcel: () => {
                if (teamGrid) {
                    teamGrid.excelExport({ fileName: 'sales-forecast-by-team.xlsx' });
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Pipelines']);
                await SecurityManager.validateToken();
                await methods.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, format, stageChartRef, monthChartRef, teamGridRef };
    }
};

Vue.createApp(App).mount('#app');
