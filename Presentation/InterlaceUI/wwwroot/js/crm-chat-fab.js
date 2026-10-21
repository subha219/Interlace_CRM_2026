/* Floating live-chat button for agents. Shown only to users with the "Chats" role; the badge counts chats waiting for an answer. */
(function () {
    'use strict';
    function safe(fn, fallback) { try { return fn(); } catch (e) { return fallback; } }
    function ready(fn) { if (document.readyState !== 'loading') fn(); else document.addEventListener('DOMContentLoaded', fn); }

    ready(function () {
        if (typeof AxiosManager === 'undefined' || typeof StorageManager === 'undefined') return;
        var roles = safe(function () { return (StorageManager.getUserRoles() || []).map(function (r) { return String(r).toLowerCase(); }); }, []);
        if (roles.indexOf('chats') < 0) return;
        if (window.location.pathname.toLowerCase().indexOf('/chats/') === 0) return; // already on the inbox

        var style = document.createElement('style');
        style.textContent =
            '.crm-chat-fab{position:fixed;right:22px;bottom:22px;z-index:1040;width:52px;height:52px;border-radius:50%;border:0;background:#2563eb;color:#fff;font-size:20px;cursor:pointer;box-shadow:0 3px 10px rgba(0,0,0,.3);display:flex;align-items:center;justify-content:center;text-decoration:none}' +
            '.crm-chat-fab:hover{background:#1d4ed8;color:#fff}' +
            '.crm-chat-fab .crm-chat-badge{position:absolute;top:-4px;right:-4px;min-width:20px;height:20px;padding:0 5px;border-radius:10px;background:#dc2626;color:#fff;font:600 11px/20px system-ui,sans-serif;text-align:center;display:none}' +
            '.crm-chat-fab.has-new{animation:crmChatPulse 1.6s ease-in-out infinite}' +
            '@keyframes crmChatPulse{0%,100%{box-shadow:0 3px 10px rgba(0,0,0,.3)}50%{box-shadow:0 0 0 8px rgba(37,99,235,.25)}}';
        document.head.appendChild(style);

        var a = document.createElement('a');
        a.href = '/Chats/ChatInbox';
        a.className = 'crm-chat-fab';
        a.title = 'Live chat';
        a.setAttribute('aria-label', 'Open live chat');
        a.innerHTML = '<svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 11.5a8.4 8.4 0 0 1-9 8.4 9 9 0 0 1-3.8-.8L3 21l1.9-5.1A8.4 8.4 0 0 1 3 11.5 8.5 8.5 0 0 1 12 3a8.5 8.5 0 0 1 9 8.5z"/></svg><span class="crm-chat-badge"></span>';
        document.body.appendChild(a);
        var badge = a.querySelector('.crm-chat-badge');

        function refresh() {
            if (document.hidden) return;
            AxiosManager.get('/Chat/GetChats?includeClosed=false', {}).then(function (r) {
                var n = (r && r.data && r.data.content && r.data.content.unreadCount) || 0;
                badge.textContent = n > 99 ? '99+' : String(n);
                badge.style.display = n > 0 ? 'block' : 'none';
                a.classList.toggle('has-new', n > 0);
            }).catch(function () { /* try again next time */ });
        }
        refresh();
        setInterval(refresh, 20000);
    });
})();
