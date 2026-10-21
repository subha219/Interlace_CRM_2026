const App = {
    setup() {
        const state = Vue.reactive({
            email: '',
            password: '',
            isSubmitting: false,
            needsCode: false,
            providers: [],
            passwordLogin: true,
            showPassword: false,
            ssoError: '',
            code: '',
            errors: {
                email: '',
                password: ''
            }
        });

        const validateForm = () => {
            state.errors.email = '';
            state.errors.password = '';
            let isValid = true;

            if (!state.email) {
                state.errors.email = 'Email is required.';
                isValid = false;
            } else if (!/\S+@\S+\.\S+/.test(state.email)) {
                state.errors.email = 'Please enter a valid email address.';
                isValid = false;
            }

            if (!state.password) {
                state.errors.password = 'Password is required.';
                isValid = false;
            } else if (state.password.length < 6) {
                state.errors.password = 'Password must be at least 6 characters.';
                isValid = false;
            }

            return isValid;
        };

        const sendEmailCode = async () => {
            try {
                state.isSubmitting = true;
                await AxiosManager.post('/Security/Login', { email: state.email, password: state.password, sendEmailCode: true });
                Swal.fire({ icon: 'info', title: 'Code sent', text: 'Check your email for the verification code.', timer: 2500, showConfirmButton: false });
            } catch (error) {
                Swal.fire({ icon: 'error', title: 'Could not send code', text: error.response?.data?.message || 'Email is not configured.' });
            } finally {
                state.isSubmitting = false;
            }
        };

        const handleSubmit = async () => {

            try {
                state.isSubmitting = true;
                await new Promise(resolve => setTimeout(resolve, 300));

                if (!validateForm()) {
                    return;
                }

                const response = await AxiosManager.post('/Security/Login', {
                    email: state.email,
                    password: state.password,
                    twoFactorCode: state.needsCode ? state.code : null
                });

                if (response.data.code === 200 && response.data.content?.data?.requiresTwoFactor) {
                    state.needsCode = true;
                    return;
                }

                if (response.data.code === 200) {
                    StorageManager.saveLoginResult(response.data);
                    Swal.fire({
                        icon: 'success',
                        title: 'Login Successful',
                        text: 'You are being redirected...',
                        timer: 2000,
                        showConfirmButton: false
                    });

                    setTimeout(() => {
                        window.location.href = SecurityManager.homeUrl();
                    }, 2000);
                } else {
                    Swal.fire({
                        icon: 'error',
                        title: 'Login Failed',
                        text: response.data.message || 'Please check your credentials.',
                        confirmButtonText: 'Try Again'
                    });
                }
            } catch (error) {
                Swal.fire({
                    icon: 'error',
                    title: 'An Error Occurred',
                    text: error.response?.data?.message || 'Please try again.',
                    confirmButtonText: 'OK'
                });
            } finally {
                state.isSubmitting = false;
            }
        };

        const ssoTexts = {
            expired: 'The sign-in took too long. Please try again.',
            denied: 'Sign-in was cancelled.',
            domain: 'This e-mail domain is not allowed to sign in. Ask your administrator.',
            notconfigured: 'This sign-in method is not set up.',
            failed: 'Single sign-on did not work. Please try again or use your password.'
        };
        const code = new URLSearchParams(window.location.search).get('sso_error');
        if (code) state.ssoError = ssoTexts[code] || ssoTexts.failed;

        axios.get('/api/Sso/Providers').then((r) => {
            const c = r.data.content || {};
            state.providers = c.providers || [];
            state.passwordLogin = c.passwordLogin !== false;
        }).catch(() => { /* password sign-in keeps working */ });

        return {
            state,
            handleSubmit,
            sendEmailCode
        };
    }
};

Vue.createApp(App).mount('#app');