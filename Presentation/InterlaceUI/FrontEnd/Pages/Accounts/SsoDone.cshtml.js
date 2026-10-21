const App = {
    setup() {
        const state = Vue.reactive({ error: '' });
        Vue.onMounted(async () => {
            const code = new URLSearchParams(location.hash.replace(/^#/, '')).get('code');
            history.replaceState(null, '', location.pathname); // keep the one-time code out of history
            if (!code) { state.error = 'The sign-in link is not valid.'; return; }
            try {
                const response = await AxiosManager.post('/Sso/Exchange', { code });
                if (response.data.code === 200 && response.data.content?.data?.accessToken) {
                    StorageManager.saveLoginResult(response.data);
                    window.location.href = (window.SecurityManager && SecurityManager.homeUrl) ? SecurityManager.homeUrl() : '/Dashboards/MyWork';
                } else {
                    state.error = 'Sign-in did not complete.';
                }
            } catch (e) {
                state.error = e.response?.data?.message || 'Sign-in did not complete. Please try again.';
            }
        });
        return { state };
    }
};
Vue.createApp(App).mount('#app');
