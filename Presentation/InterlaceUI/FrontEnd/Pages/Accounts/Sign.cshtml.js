const App = {
    setup() {
        const token = new URLSearchParams(location.search).get('token') || '';
        const state = Vue.reactive({ mode: 'loading', d: null, codeSent: false, code: '', name: '', agree: false, busy: false, error: '', closedTitle: '', closedText: '' });
        const headers = { headers: { 'X-Sign-Token': token } };
        const msg = (e, fallback) => e.response?.data?.message || (e.response?.data?.errors ? Object.values(e.response.data.errors).flat().join(' ') : null) || fallback;
        const sendCode = async () => {
            state.busy = true; state.error = '';
            try { await axios.post('/api/PublicSign/SendCode', {}, headers); state.codeSent = true; }
            catch (e) { state.error = msg(e, 'Could not send the code. Please try again.'); }
            finally { state.busy = false; }
        };
        const sign = async () => {
            state.busy = true; state.error = '';
            try { await axios.post('/api/PublicSign/Sign', { code: state.code, typedName: state.name, agree: state.agree }, headers); state.mode = 'signed'; }
            catch (e) { state.error = msg(e, 'Could not sign. Please try again.'); }
            finally { state.busy = false; }
        };
        const decline = async () => {
            const ask = await Swal.fire({ title: 'Decline to sign?', input: 'text', inputPlaceholder: 'Reason (optional)', inputAttributes: { maxlength: 500 }, showCancelButton: true, confirmButtonText: 'Decline' });
            if (!ask.isConfirmed) return;
            try { await axios.post('/api/PublicSign/Decline', { reason: ask.value || null }, headers); state.mode = 'declined'; }
            catch (e) { state.error = msg(e, 'Could not decline. Please try again.'); }
        };
        Vue.onMounted(async () => {
            try {
                const d = (await axios.get('/api/PublicSign/Get', headers)).data.content;
                state.d = d;
                if (d.status === 'Signed') { state.mode = 'closed'; state.closedTitle = 'Already signed'; state.closedText = 'This document was signed on ' + new Date(d.signedAtUtc.endsWith('Z') ? d.signedAtUtc : d.signedAtUtc + 'Z').toLocaleString() + '.'; }
                else if (d.status === 'Expired') { state.mode = 'closed'; state.closedTitle = 'Link expired'; state.closedText = 'Please ask ' + (d.requestedByName || 'the sender') + ' for a new link.'; }
                else if (d.status === 'Declined' || d.status === 'Cancelled') { state.mode = 'closed'; state.closedTitle = 'No longer available'; state.closedText = 'This request is closed.'; }
                else state.mode = 'form';
            } catch (e) { state.mode = 'invalid'; }
        });
        return { state, sendCode, sign, decline };
    }
};
Vue.createApp(App).mount('#app');
