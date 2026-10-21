/* Adds a chat button to any page. Usage:
   <script src="https://YOUR-CRM/js/chat-embed.js" async></script>  */
(function () {
    var me = document.currentScript;
    var origin = me && me.src ? new URL(me.src).origin : location.origin;
    var open = false;

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.textContent = 'Chat';
    btn.setAttribute('aria-label', 'Open chat');
    btn.style.cssText = 'position:fixed;right:20px;bottom:20px;z-index:2147483000;padding:12px 18px;border:0;border-radius:24px;background:#2563eb;color:#fff;font:600 14px system-ui,sans-serif;cursor:pointer;box-shadow:0 2px 8px rgba(0,0,0,.25)';

    var frame = document.createElement('iframe');
    frame.title = 'Chat';
    frame.src = origin + '/Accounts/ChatWidget';
    frame.style.cssText = 'position:fixed;right:20px;bottom:76px;z-index:2147483000;width:340px;max-width:calc(100vw - 40px);height:480px;max-height:calc(100vh - 100px);border:1px solid #dde3ea;border-radius:10px;box-shadow:0 4px 18px rgba(0,0,0,.25);background:#fff;display:none';

    btn.addEventListener('click', function () {
        open = !open;
        frame.style.display = open ? 'block' : 'none';
        btn.textContent = open ? 'Close' : 'Chat';
    });
    document.body.appendChild(frame);
    document.body.appendChild(btn);
})();
