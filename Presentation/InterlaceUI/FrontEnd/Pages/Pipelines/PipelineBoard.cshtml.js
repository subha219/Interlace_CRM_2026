const App = {
    setup() {
        const state = Vue.reactive({
            columns: [],
            search: '',
            overKey: null,
            dragged: null,
            explain: null,
            openTotal: 0,
            openWeighted: 0,
            openCount: 0
        });

        const format = (v) => NumberFormatManager.formatToLocale(v ?? 0);

        const methods = {
            load: async () => {
                const response = await AxiosManager.get('/Lead/GetPipelineBoard', {});
                state.columns = response?.data?.content?.data ?? [];
                const open = state.columns.filter(c => c.key !== 'Won' && c.key !== 'Lost');
                state.openTotal = open.reduce((a, c) => a + c.totalAmount, 0);
                state.openWeighted = open.reduce((a, c) => a + c.weightedAmount, 0);
                state.openCount = open.reduce((a, c) => a + c.count, 0);
            },
            filtered: () => {
                const q = (state.search ?? '').trim().toLowerCase();
                if (!q) return state.columns;
                return state.columns.map(c => ({
                    ...c,
                    cards: c.cards.filter(x => (x.title ?? '').toLowerCase().includes(q) || (x.companyName ?? '').toLowerCase().includes(q) || (x.number ?? '').toLowerCase().includes(q))
                }));
            },
            toggleExplain: (id) => { state.explain = state.explain === id ? null : id; },
            dateText: (d) => d ? new Date(d).toISOString().substring(0, 10) : '',
            dragStart: (event, card, col) => {
                state.dragged = { id: card.id, from: col.key };
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', card.id);
                event.target.classList.add('dragging');
            },
            dragEnd: () => {
                state.dragged = null;
                state.overKey = null;
                document.querySelectorAll('.kanban-card.dragging').forEach(el => el.classList.remove('dragging'));
            },
            drop: async (col) => {
                const dragged = state.dragged;
                state.overKey = null;
                if (!dragged || dragged.from === col.key) return;
                try {
                    const response = await AxiosManager.post('/Lead/MoveLeadStage', {
                        leadId: dragged.id,
                        target: col.key,
                        updatedById: StorageManager.getUserId()
                    });
                    if (response.data.code !== 200) {
                        throw new Error(response.data.message);
                    }
                } catch (error) {
                    Swal.fire({
                        icon: 'error',
                        title: 'Could not move the lead',
                        text: error.response?.data?.message ?? error.message ?? 'Please try again.',
                        confirmButtonText: 'OK'
                    });
                }
                await methods.load();
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

        return { state, methods, format };
    }
};

Vue.createApp(App).mount('#app');
