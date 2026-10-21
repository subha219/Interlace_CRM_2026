const App = {
    setup() {
        const qrRef = Vue.ref(null);
        const state = Vue.reactive({
            loading: true, busy: false, enabled: false,
            sharedKey: '', sharedKeyFormatted: '', uri: '',
            code: '', password: '', recoveryCodes: []
        });

        const drawQr = () => {
            Vue.nextTick(() => {
                try {
                    if (qrRef.value && state.uri && !window.qrcode) {
                        qrRef.value.innerHTML = '<span class="small text-muted">The QR code could not be drawn. Use the setup key above.</span>';
                    }
                    if (qrRef.value && window.qrcode && state.uri) {
                        // drawn here as inline SVG on a white card so every authenticator app can read it, even in dark mode
                        const qr = window.qrcode(0, 'M');
                        qr.addData(state.uri);
                        qr.make();
                        const n = qr.getModuleCount(), m = 3, size = n + m * 2;
                        let d = '';
                        for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) if (qr.isDark(r, c)) d += 'M' + (c + m) + ' ' + (r + m) + 'h1v1h-1z';
                        qrRef.value.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ' + size + ' ' + size + '" width="180" height="180" shape-rendering="crispEdges" role="img" aria-label="QR code for the authenticator app" style="background:#fff;border-radius:6px"><path d="' + d + '" fill="#000"/></svg>';
                    }
                } catch (e) {
                    if (qrRef.value) qrRef.value.innerHTML = '<span class="small text-muted">The QR code could not be drawn. Use the setup key above.</span>';
                    console.error('QR error:', e);
                }
            });
        };

        const load = async () => {
            state.loading = true;
            try {
                const r = await AxiosManager.get('/Security/GetTwoFactorSetup');
                const d = r.data.content.data;
                state.enabled = d.enabled;
                state.sharedKey = d.sharedKey || '';
                state.sharedKeyFormatted = (d.sharedKey || '').replace(/(.{4})/g, '$1 ').trim();
                state.uri = d.authenticatorUri || '';
                if (!state.enabled) drawQr();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Error', text: e.response?.data?.message || 'Could not load settings.' });
            } finally {
                state.loading = false;
            }
        };

        const enable = async () => {
            state.busy = true;
            try {
                const r = await AxiosManager.post('/Security/EnableTwoFactor', { code: state.code });
                state.recoveryCodes = r.data.content.recoveryCodes || [];
                state.code = '';
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Invalid code', text: e.response?.data?.message || 'Check the code and try again.' });
            } finally {
                state.busy = false;
            }
        };

        const disable = async () => {
            state.busy = true;
            try {
                await AxiosManager.post('/Security/DisableTwoFactor', { password: state.password });
                state.password = '';
                state.recoveryCodes = [];
                await load();
            } catch (e) {
                Swal.fire({ icon: 'error', title: 'Could not disable', text: e.response?.data?.message || 'Wrong password.' });
            } finally {
                state.busy = false;
            }
        };

        // draw whenever the box appears or the key changes, whatever order things load in
        Vue.watch(() => [qrRef.value, state.uri], drawQr, { flush: 'post' });
        Vue.onMounted(load);
        return { state, qrRef, enable, disable };
    }
};

Vue.createApp(App).mount('#app');
