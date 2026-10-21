const App = {
    setup() {
        const state = Vue.reactive({ groups: [], edit: {}, clear: {}, busy: false });
        const origin = window.location.origin;
        const fail = (e, t) => Swal.fire({ icon: 'error', title: t, text: e.response?.data?.message || e.response?.data?.errors?.join?.(' ') || 'Please try again.' });

        const load = async () => {
            try {
                const r = (await AxiosManager.get('/Connection/GetConnections', {})).data.content;
                state.groups = r.data; state.edit = {}; state.clear = {};
                r.data.forEach(g => g.fields.forEach(f => { if (!f.secret && f.kind !== 'bool') state.edit[f.key] = f.value || ''; }));
            } catch (e) { fail(e, 'Could not load the connections'); }
        };

        // a group counts as set up when its required boxes have a value
        const needed = { google: ['Google:ClientId', 'Google:ClientSecret'], sso: ['Sso:AllowedDomains'], whatsapp: ['WhatsApp:PhoneNumberId', 'WhatsApp:AccessToken'],
            sms: ['Msg91:AuthKey'], telephony: ['Exotel:AccountSid', 'Exotel:ApiKey', 'Exotel:ApiToken', 'Exotel:CallerId'], ai: ['Assistant:ApiKey'] };
        const ready = (g) => (needed[g.id] || []).every(k => (g.fields.find(f => f.key === k) || {}).hasValue);

        const urls = (g) => {
            if (g.id === 'google') return [
                { label: 'Calendar redirect', url: origin + '/api/Calendar/Callback' }, { label: 'Gmail redirect', url: origin + '/api/MailSync/Callback' }, { label: 'Sign-in redirect', url: origin + '/api/Sso/Callback' }];
            if (g.id === 'sso') return [{ label: 'Sign-in redirect (Google and Microsoft)', url: origin + '/api/Sso/Callback' }, { label: 'Outlook calendar redirect (Microsoft app, permission Calendars.ReadWrite)', url: origin + '/api/Calendar/OutlookCallback' }];
            if (g.id === 'whatsapp') return [{ label: 'Webhook callback URL', url: origin + '/api/ChannelWebhook/WhatsApp' }];
            if (g.id === 'telephony') return [{ label: 'Status callback', url: origin + '/api/ChannelWebhook/Exotel?key=<your status callback key>' }];
            return [];
        };

        const changes = (g) => {
            const out = {};
            g.fields.forEach(f => {
                if (state.clear[f.key]) { out[f.key] = ''; return; }
                const v = state.edit[f.key];
                if (v === undefined || v === null) return;
                if (f.secret) { if (v !== '') out[f.key] = v; return; }
                if (f.kind === 'bool') { out[f.key] = v; return; }
                if (String(v).trim() !== (f.value || '')) out[f.key] = String(v).trim();
            });
            return out;
        };
        const dirty = (g) => Object.keys(changes(g)).length > 0;

        const save = async (g) => {
            const values = changes(g);
            state.busy = true;
            try {
                const r = (await AxiosManager.post('/Connection/Save', { values })).data.content;
                Swal.fire({ icon: 'success', title: 'Saved', text: r.changed + ' setting(s) updated.', timer: 1800, showConfirmButton: false });
                await load();
            } catch (e) { fail(e, 'Could not save'); } finally { state.busy = false; }
        };

        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Integrations']); await SecurityManager.validateToken(); } catch (e) { console.error('page init error:', e); }
            await load();
        });
        return { state, ready, urls, dirty, save };
    }
};

Vue.createApp(App).mount('#app');
