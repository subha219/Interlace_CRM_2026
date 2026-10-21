const App = {
    setup() {
        const state = Vue.reactive({ data: null, loading: false, error: '' });
        const hover = Vue.ref(-1);

        const nice = (n) => NumberFormatManager.formatToLocale(n ?? 0);
        const maxOf = (rows, key) => Math.max(1, ...rows.map(r => r[key] ?? 0));
        const palette = [['#6366f1', '#8b5cf6'], ['#3b82f6', '#6366f1'], ['#06b6d4', '#3b82f6'], ['#10b981', '#06b6d4'], ['#f59e0b', '#10b981'], ['#ef4444', '#f59e0b']];
        const kpiColors = ['#6366f1', '#06b6d4', '#10b981', '#f59e0b', '#ec4899', '#8b5cf6', '#3b82f6', '#ef4444'];
        const kpiIcons = [
            [/overdue|invoice|receiv/i, 'fa-file-invoice-dollar'], [/cash|revenue|payment|collect|paid/i, 'fa-sack-dollar'],
            [/win|won|closed/i, 'fa-trophy'], [/pipeline|deal|forecast/i, 'fa-handshake'], [/lead/i, 'fa-bullseye'],
            [/case|support|ticket/i, 'fa-headset'], [/quote|quotation/i, 'fa-file-signature'], [/customer|account|contact/i, 'fa-users'],
        ];
        const gaugeLen = 2 * Math.PI * 56;

        const d = Vue.computed(() => state.data);

        const health = Vue.computed(() => {
            const h = d.value?.healthyDeals ?? 0, a = d.value?.attentionDeals ?? 0, r = d.value?.atRiskDealCount ?? 0;
            const total = h + a + r;
            return { total, score: total ? Math.round((h * 100 + a * 55 + r * 15) / total) : 0 };
        });

        const healthSegs = Vue.computed(() => {
            const rows = [
                { name: 'Healthy', count: d.value?.healthyDeals ?? 0, color: '#10b981' },
                { name: 'Needs attention', count: d.value?.attentionDeals ?? 0, color: '#f59e0b' },
                { name: 'At risk', count: d.value?.atRiskDealCount ?? 0, color: '#ef4444' },
            ];
            const total = rows.reduce((s, x) => s + x.count, 0) || 1;
            const C = 2 * Math.PI * 70;
            let acc = 0;
            return rows.map(x => {
                const len = (x.count / total) * C;
                const seg = { ...x, pct: Math.round((x.count / total) * 100), dash: `${Math.max(0, len - (x.count ? 2 : 0))} ${C}`, offset: -acc };
                acc += len;
                return seg;
            });
        });

        const ageingTotal = Vue.computed(() => (d.value?.ageing ?? []).reduce((s, a) => s + (a.amount ?? 0), 0));
        const brief = Vue.computed(() => {
            const p = d.value?.pipeline ?? [];
            return {
                priorities: d.value?.priorities?.length ?? 0,
                riskCount: d.value?.atRiskDealCount ?? 0,
                riskAmount: (d.value?.atRiskDeals ?? []).reduce((s, x) => s + (x.amount ?? 0), 0),
                overdue: (d.value?.ageing ?? []).filter(a => a.name !== 'Not due').reduce((s, a) => s + (a.amount ?? 0), 0),
                pipeline: p.reduce((s, x) => s + (x.amount ?? 0), 0),
                weighted: p.reduce((s, x) => s + (x.weighted ?? 0), 0),
            };
        });

        /* ----- revenue area chart ----- */
        const W = { l: 46, r: 632, t: 14, b: 214 };
        const revenue = Vue.computed(() => d.value?.revenueByMonth ?? []);
        const revenueTotal = Vue.computed(() => revenue.value.reduce((s, m) => s + (m.value ?? 0), 0));
        const niceMax = (v) => { if (v <= 0) return 1; const p = Math.pow(10, Math.floor(Math.log10(v))); const n = v / p; return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10) * p; };
        const yMax = Vue.computed(() => niceMax(Math.max(...revenue.value.map(m => m.value ?? 0), 0)));
        const points = Vue.computed(() => {
            const n = revenue.value.length;
            return revenue.value.map((m, i) => ({
                name: m.name, value: m.value ?? 0,
                x: n === 1 ? (W.l + W.r) / 2 : W.l + 18 + (i * (W.r - W.l - 36)) / (n - 1),
                y: W.b - ((m.value ?? 0) / yMax.value) * (W.b - W.t),
            }));
        });
        const yTicks = Vue.computed(() => [0, 1, 2, 3, 4].map(i => ({ v: (yMax.value * i) / 4, y: W.b - (i / 4) * (W.b - W.t) })));
        const smooth = (pts) => {
            if (pts.length < 2) return pts.length ? `M${pts[0].x},${pts[0].y}` : '';
            let p = `M${pts[0].x},${pts[0].y}`;
            for (let i = 0; i < pts.length - 1; i++) {
                const a = pts[i], b = pts[i + 1], mx = (a.x + b.x) / 2;
                p += ` C${mx},${a.y} ${mx},${b.y} ${b.x},${b.y}`;
            }
            return p;
        };
        const linePath = Vue.computed(() => smooth(points.value));
        const areaPath = Vue.computed(() => points.value.length < 2 ? '' : `${linePath.value} L${points.value[points.value.length - 1].x},${W.b} L${points.value[0].x},${W.b} Z`);

        const onMove = (ev) => {
            const r = ev.currentTarget.getBoundingClientRect();
            const x = ((ev.clientX - r.left) / r.width) * 640;
            let best = -1, dist = 1e9;
            points.value.forEach((p, i) => { const dd = Math.abs(p.x - x); if (dd < dist) { dist = dd; best = i; } });
            hover.value = best;
        };

        const methods = {
            load: async () => {
                state.loading = true;
                state.error = '';
                try {
                    const response = await AxiosManager.get('/Insights/GetTodayBriefing', {});
                    state.data = response?.data?.content ?? null;
                } catch (e) {
                    state.error = 'Could not load your insights. Please refresh the page.';
                } finally {
                    state.loading = false;
                }
            },
            money: (v) => nice(v),
            short: (v) => {
                const n = Number(v ?? 0);
                if (n >= 1e6) return (n / 1e6).toFixed(n % 1e6 ? 1 : 0) + 'M';
                if (n >= 1e3) return (n / 1e3).toFixed(n % 1e3 ? 1 : 0) + 'k';
                return n.toFixed(0);
            },
            fmt: (k) => k.format === 'percent' ? (k.value ?? 0) + '%' : k.format === 'number' ? String(k.value ?? 0) : nice(k.value),
            delta: (k) => (!k.previous ? (k.value > 0 ? 100 : 0) : ((k.value - k.previous) / k.previous) * 100),
            deltaClass: (k) => {
                const v = (!k.previous ? (k.value > 0 ? 100 : 0) : ((k.value - k.previous) / k.previous) * 100);
                const goodUp = !/overdue|risk|open case|backlog/i.test(k.key + ' ' + k.label);
                return v === 0 ? 'flat' : ((v > 0) === goodUp ? 'up' : 'down');
            },
            style: (k, i) => {
                const hit = kpiIcons.find(([re]) => re.test(k.key + ' ' + k.label));
                return { color: k.key === 'overdue' ? '#ef4444' : kpiColors[i % kpiColors.length], icon: hit ? hit[1] : 'fa-chart-simple' };
            },
            spark: (series) => {
                const max = Math.max(1, ...series), min = Math.min(0, ...series);
                const step = 92 / Math.max(1, series.length - 1);
                return series.map((v, i) => `${(i * step).toFixed(1)},${(30 - ((v - min) / (max - min || 1)) * 26).toFixed(1)}`).join(' ');
            },
            icon: (kind) => ({ Invoice: 'fa-file-invoice-dollar', Support: 'fa-headset', Deal: 'fa-handshake', Quote: 'fa-file-signature' }[kind] ?? 'fa-bell'),
            ring: (score) => score >= 70 ? 'good' : score >= 40 ? 'mid' : 'bad',
            pct: (v, max) => Math.max(34, Math.round(((v ?? 0) / max) * 100)),
            ageColor: (i) => ['#10b981', '#f59e0b', '#fb923c', '#ef4444', '#b91c1c'][i % 5],
            askAssistant: () => window.CrmShell?.openAssistant('What should I do today?'),
        };

        const greeting = Vue.computed(() => {
            const h = new Date().getHours();
            const part = h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
            const email = StorageManager.getEmail?.() ?? '';
            const raw = email ? email.split('@')[0].replace(/[._-]+/g, ' ') : '';
            const name = raw.replace(/\b\w/g, c => c.toUpperCase());
            return name ? `${part}, ${name}` : part;
        });
        const todayText = new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
        const maxStage = Vue.computed(() => maxOf(d.value?.pipeline ?? [], 'amount'));

        const quick = [
            { label: 'New lead', href: '/Leads/LeadList', icon: 'fa-bullseye', color: '#6366f1' },
            { label: 'New quote', href: '/Quotations/QuotationList', icon: 'fa-file-signature', color: '#06b6d4' },
            { label: 'New invoice', href: '/Invoices/InvoiceList', icon: 'fa-file-invoice-dollar', color: '#10b981' },
            { label: 'Log a case', href: '/SupportCases/SupportCaseList', icon: 'fa-headset', color: '#f59e0b' },
            { label: 'Customer 360', href: '/Customers/Customer360', icon: 'fa-address-card', color: '#ec4899' },
            { label: 'Import data', href: '/Imports/ImportCustomers', icon: 'fa-file-import', color: '#3b82f6' },
        ];

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Dashboards']);
                await SecurityManager.validateToken();
                await methods.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return {
            state, d, hover, health, healthSegs, ageingTotal, brief, revenueTotal, points, yTicks, linePath, areaPath, onMove,
            greeting, todayText, maxStage, palette, gaugeLen, quick, ...methods,
        };
    }
};

Vue.createApp(App).mount('#app');
