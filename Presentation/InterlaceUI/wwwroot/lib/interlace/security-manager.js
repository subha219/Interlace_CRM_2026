const SecurityManager = {
    hasAny: (roles) => {
        const mine = (StorageManager.getUserRoles() || []).map(r => String(r).toLowerCase());
        return (roles || []).some(r => mine.includes(String(r).toLowerCase()));
    },

    // can the user open this page? needs the section role (first URL part) and, for pages that list them, one of the extra roles
    canOpen: (url) => {
        const path = String(url || '').split('?')[0].replace(/\/+$/, '').toLowerCase();
        const seg = path.split('/')[1] || '';
        if (!SecurityManager.hasAny([seg])) return false;
        const menu = StorageManager.getMenuNavigation() || [];
        const item = menu.find(i => i.navURL && String(i.navURL).replace(/\/+$/, '').toLowerCase() === path);
        return !(item && item.roles && item.roles.length) || SecurityManager.hasAny(item.roles);
    },

    // the first dashboard that suits this user's roles
    homeUrl: () => {
        const list = ['/Dashboards/InsightsDashboard', '/Dashboards/DefaultDashboard', '/Dashboards/SupportDashboard', '/Dashboards/MyWork'];
        return list.find(u => SecurityManager.canOpen(u)) || '/Dashboards/MyWork';
    },

    authorizePage: async (requiredRoles) => {
        const userRoles = StorageManager.getUserRoles();
        const isAuthorized = userRoles?.some(role => requiredRoles.includes(role));
        if (isAuthorized && !SecurityManager.canOpen(window.location.pathname)) {
            Swal.fire({ icon: 'info', title: 'Not part of your role', text: 'Taking you to your own dashboard...', timer: 1800, showConfirmButton: false });
            setTimeout(() => { window.location.href = SecurityManager.homeUrl(); }, 1800);
            return;
        }
        if (!isAuthorized) {
            Swal.fire({
                icon: 'error',
                title: 'Unauthorized',
                text: 'You are being redirected...',
                timer: 2000,
                showConfirmButton: false
            });
            setTimeout(() => {
                window.location.href = '/Accounts/Login';
            }, 2000);
        }

    },

    validateToken: async () => {

        try {
            const response = await AxiosManager.post('/Security/ValidateToken',
                {},
            );

            if (response?.data?.code === 200) {
                return true;
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Token not valid',
                    text: 'You are being redirected...',
                    timer: 2000,
                    showConfirmButton: false
                });
                setTimeout(() => {
                    window.location.href = '/Accounts/Login';
                }, 2000);
                return false;
            }
        } catch (error) {
            Swal.fire({
                icon: 'error',
                title: error?.response?.data?.message || 'Error validating token',
                text: 'You are being redirected...',
                timer: 2000,
                showConfirmButton: false
            });
            setTimeout(() => {
                window.location.href = '/Accounts/Login';
            }, 2000);
            return false;
        }
    }


};