const App = {
    setup() {
        const loading = Vue.ref(false);
        const crm = Vue.ref({});
        const cards = Vue.ref({});
        const sales = Vue.ref({});
        const purchase = Vue.ref({});
        const funnel = Vue.ref([]);
        const team = Vue.ref([]);
        const campaign = Vue.ref([]);
        const activity = Vue.ref([]);
        const tab = Vue.ref(0);

        const palette = [['#6366f1', '#8b5cf6'], ['#3b82f6', '#6366f1'], ['#06b6d4', '#3b82f6'], ['#10b981', '#06b6d4'], ['#f59e0b', '#10b981'], ['#ef4444', '#f59e0b']];
        const donutColors = ['#6366f1', '#06b6d4', '#10b981', '#f59e0b', '#ec4899', '#ef4444', '#8b5cf6', '#3b82f6'];
        const barColors = ['#6366f1', '#06b6d4', '#f59e0b', '#10b981', '#ec4899', '#ef4444'];
        const gaugeLen = 2 * Math.PI * 56;
        const CIRC = 2 * Math.PI * 70;

        const get = async (url) => {
            const r = await AxiosManager.get(url, {});
            return r?.data?.content?.data;
        };

        const short = (v) => {
            const n = Number(v ?? 0);
            if (Math.abs(n) >= 1e9) return (n / 1e9).toFixed(1) + 'B';
            if (Math.abs(n) >= 1e6) return (n / 1e6).toFixed(1) + 'M';
            if (Math.abs(n) >= 1e3) return (n / 1e3).toFixed(1) + 'k';
            return n.toFixed(0);
        };
        const money = (v) => NumberFormatManager.formatToLocale(v ?? 0);
        const day = (v) => (v ? String(v).substring(0, 10) : '');

        //>>> show (and fetch) only what the user's roles cover; the server enforces the same rules
        const can = (...roles) => SecurityManager.hasAny(roles);
        const show = {
            leads: can('Leads', 'Pipelines'),
            campaigns: can('Campaigns'),
            budgets: can('Budgets'),
            expenses: can('Expenses'),
            activity: can('LeadActivities', 'Leads'),
            sales: can('SalesOrders', 'Invoices', 'Quotations'),
            salesQty: can('SalesOrders', 'Invoices'),
            purchase: can('PurchaseOrders'),
        };
        show.budget = show.budgets || show.expenses;
        const crmNeeded = show.leads || show.campaigns || show.budget;

        const load = async () => {
            loading.value = true;
            const skip = () => Promise.resolve(null);
            const jobs = await Promise.allSettled([
                crmNeeded ? get('/Dashboard/GetCRMDashboard') : skip(), (show.salesQty || show.purchase) ? get('/Dashboard/GetCardsDashboard') : skip(),
                show.sales ? get('/Dashboard/GetSalesDashboard') : skip(), show.purchase ? get('/Dashboard/GetPurchaseDashboard') : skip(),
                show.leads ? get('/Dashboard/GetLeadPipelineFunnel') : skip(), show.leads ? get('/Dashboard/GetSalesTeamLeadClosing') : skip(),
                show.campaigns ? get('/Dashboard/GetCampaignByStatus') : skip(), show.activity ? get('/Dashboard/GetLeadActivityByType') : skip(),
            ]);
            const v = (i, fallback) => (jobs[i].status === 'fulfilled' && jobs[i].value != null ? jobs[i].value : fallback);
            crm.value = v(0, {}).crmDashboard ?? {};
            cards.value = v(1, {}).cardsDashboard ?? {};
            sales.value = v(2, {});
            purchase.value = v(3, {});
            funnel.value = v(4, []);
            team.value = v(5, []);
            campaign.value = v(6, []);
            activity.value = v(7, []);
            loading.value = false;
        };

        const winRate = Vue.computed(() => {
            const lead = crm.value.leadTotalAmount, won = crm.value.closedTotalAmount;
            return lead ? Math.min(100, Math.round(((won || 0) / lead) * 100)) : null;
        });
        const budgetUsed = Vue.computed(() => {
            const b = crm.value.budgetTotalAmount, e = crm.value.expenseTotalAmount;
            return b ? Math.round(((e || 0) / b) * 100) : null;
        });
        const budgetDash = Vue.computed(() => `${Math.min(1, (budgetUsed.value || 0) / 100) * CIRC} ${CIRC}`);

        const tiles = Vue.computed(() => {
            if (!Object.keys(crm.value).length && !Object.keys(cards.value).length) return [];
            return [
                { label: 'Closed won', text: short(crm.value.closedTotalAmount), note: 'Revenue won from leads', icon: 'fa-trophy', color: '#10b981', href: '/Leads/LeadList', ok: show.leads },
                { label: 'Lead pipeline', text: short(crm.value.leadTotalAmount), note: 'Targeted value', icon: 'fa-bullseye', color: '#6366f1', href: '/Pipelines/PipelineBoard', ok: show.leads },
                { label: 'Campaign target', text: short(crm.value.campaignTotalAmount), note: 'Target revenue', icon: 'fa-bullhorn', color: '#ec4899', href: '/Campaigns/CampaignList', ok: show.campaigns },
                { label: 'Budget', text: short(crm.value.budgetTotalAmount), note: 'Allocated', icon: 'fa-wallet', color: '#06b6d4', href: '/Budgets/BudgetList', ok: show.budgets },
                { label: 'Expense', text: short(crm.value.expenseTotalAmount), note: 'Spent so far', icon: 'fa-receipt', color: '#f59e0b', href: '/Expenses/ExpenseList', ok: show.expenses },
                { label: 'Sales qty', text: short(cards.value.salesTotal), note: 'Units sold', icon: 'fa-chart-line', color: '#3b82f6', href: '/SalesOrders/SalesOrderList', ok: show.salesQty },
                { label: 'Purchase qty', text: short(cards.value.purchaseTotal), note: 'Units purchased', icon: 'fa-cart-shopping', color: '#8b5cf6', href: '/PurchaseOrders/PurchaseOrderList', ok: show.purchase },
            ].filter(t => t.ok);
        });

        const funnelTotal = Vue.computed(() => Math.max(1, funnel.value.reduce((s, x) => s + (x.y || 0), 0)));
        const funnelMax = Vue.computed(() => Math.max(1, ...funnel.value.map(x => x.y || 0)));
        const activityMax = Vue.computed(() => Math.max(1, ...activity.value.map(x => x.y || 0)));

        const toSegs = (rows) => {
            const list = (rows || []).filter(r => (r.y || 0) > 0);
            const total = list.reduce((s, r) => s + r.y, 0) || 1;
            let acc = 0;
            return list.map((r, i) => {
                const len = (r.y / total) * CIRC;
                const seg = { x: r.x || 'Unknown', pct: Math.round((r.y / total) * 100), color: donutColors[i % donutColors.length], dash: `${Math.max(0, len - 2)} ${CIRC}`, offset: -acc };
                acc += len;
                return seg;
            });
        };
        const teamSegs = Vue.computed(() => toSegs(team.value));
        const campSegs = Vue.computed(() => toSegs(campaign.value));

        const allTabs = [
            { key: 'salesByCustomerGroupDashboard', src: 'sales', label: 'Sales by customer group' },
            { key: 'salesByCustomerCategoryDashboard', src: 'sales', label: 'Sales by customer category' },
            { key: 'purchaseByVendorGroupDashboard', src: 'purchase', label: 'Purchase by vendor group' },
            { key: 'purchaseByVendorCategoryDashboard', src: 'purchase', label: 'Purchase by vendor category' },
        ];
        const tabs = allTabs.filter(t => (t.src === 'sales' ? show.sales : show.purchase));

        const bars = Vue.computed(() => {
            const t = tabs[tab.value];
            if (!t) return { cats: [], series: [], grid: [], rects: [], labels: [] };
            const series = ((t.src === 'sales' ? sales.value : purchase.value)?.[t.key] ?? []).map(s => ({ name: s.name || 'Series', data: s.dataSource || [] }));
            const cats = [];
            series.forEach(s => s.data.forEach(p => { if (!cats.includes(p.x)) cats.push(p.x); }));
            if (!cats.length) return { cats: [], series, grid: [], rects: [], labels: [] };
            const rawMax = Math.max(1, ...series.flatMap(s => s.data.map(p => p.y || 0)));
            const p10 = Math.pow(10, Math.floor(Math.log10(rawMax)));
            const step = p10 / 2;
            const max = Math.ceil(rawMax / step) * step || rawMax;
            const L = 44, R = 692, T = 14, B = 220;
            const grid = [0, 1, 2, 3, 4].map(i => ({ v: +((max * i) / 4).toFixed(1), y: B - (i / 4) * (B - T) }));
            const gw = (R - L) / cats.length;
            const bw = Math.min(38, (gw * 0.72) / Math.max(1, series.length));
            const rects = [], labels = [];
            cats.forEach((c, ci) => {
                const gx = L + gw * ci + (gw - bw * series.length) / 2;
                labels.push({ name: String(c).length > 14 ? String(c).substring(0, 13) + '...' : c, x: L + gw * ci + gw / 2 });
                series.forEach((s, si) => {
                    const pt = s.data.find(p => p.x === c);
                    const val = pt ? (pt.y || 0) : 0;
                    const h = Math.max(val ? 3 : 0, (val / max) * (B - T));
                    rects.push({ key: ci + '-' + si, x: gx + bw * si + 1, y: B - h, w: Math.max(4, bw - 3), h, color: barColors[si % barColors.length], tip: `${s.name} - ${c}: ${val}` });
                });
            });
            return { cats, series, grid, rects, labels };
        });

        const latestSales = Vue.computed(() => [...(sales.value?.salesOrderDashboard ?? [])].sort((a, b) => String(b.salesOrder?.orderDate).localeCompare(String(a.salesOrder?.orderDate))).slice(0, 7));
        const latestPurchases = Vue.computed(() => [...(purchase.value?.purchaseOrderDashboard ?? [])].sort((a, b) => String(b.purchaseOrder?.orderDate).localeCompare(String(a.purchaseOrder?.orderDate))).slice(0, 7));

        const todayText = new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Dashboards']);
                await SecurityManager.validateToken();
                await load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return {
            loading, load, crm, tiles, funnel, funnelTotal, funnelMax, activity, activityMax, teamSegs, campSegs,
            winRate, budgetUsed, budgetDash, tabs, tab, bars, barColors, palette, latestSales, latestPurchases,
            gaugeLen, todayText, short, money, day, show,
        };
    }
};

Vue.createApp(App).mount('#app');
