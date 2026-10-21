(function () {
    'use strict';
    var KEY = 'crm.chat.token';
    var token = null, last = null, timer = null, shown = {};
    var $ = function (id) { return document.getElementById(id); };

    function load() { try { return sessionStorage.getItem(KEY); } catch (e) { return null; } }
    function save(v) { try { if (v) sessionStorage.setItem(KEY, v); else sessionStorage.removeItem(KEY); } catch (e) { /* storage may be blocked */ } }

    function api(method, path, body) {
        var headers = { 'Content-Type': 'application/json' };
        if (token) headers['X-Chat-Token'] = token;
        return fetch('/api/PublicChat/' + path, { method: method, headers: headers, body: body ? JSON.stringify(body) : undefined, credentials: 'omit' })
            .then(function (r) {
                return r.json().catch(function () { return {}; }).then(function (j) {
                    if (!r.ok) { var err = new Error(j.message || (r.status === 429 ? 'Too many requests. Please slow down.' : 'Something went wrong.')); err.status = r.status; throw err; }
                    return j.content;
                });
            });
    }

    // text is always added with textContent, never as HTML
    function add(line) {
        if (shown[line.id]) return;
        shown[line.id] = true;
        var row = document.createElement('div');
        row.className = 'row' + (line.fromVisitor ? ' me' : '');
        var box = document.createElement('div');
        if (!line.fromVisitor && line.senderName) { var w = document.createElement('div'); w.className = 'who'; w.textContent = line.senderName; box.appendChild(w); }
        var b = document.createElement('div'); b.className = 'bubble'; b.textContent = line.body || '';
        box.appendChild(b); row.appendChild(box); $('log').appendChild(row);
        if (line.atUtc && (!last || line.atUtc > last)) last = line.atUtc;
        $('log').scrollTop = $('log').scrollHeight;
    }

    function open() {
        $('start').hidden = true; $('log').hidden = false; $('form').hidden = false; $('cerr').hidden = false;
        timer = setInterval(poll, 3000);
    }
    function end() {
        clearInterval(timer); save(null); $('form').hidden = true; $('ended').hidden = false;
    }
    function poll() {
        if (document.hidden) return;
        api('GET', 'Poll' + (last ? '?afterUtc=' + encodeURIComponent(last) : '')).then(function (r) {
            r.messages.forEach(add);
            if (r.closed) end();
        }).catch(function (e) { if (e.status === 401) end(); });
    }

    $('go').addEventListener('click', function () {
        $('serr').textContent = '';
        var name = $('n').value.trim(), msg = $('m').value.trim();
        if (!name || !msg) { $('serr').textContent = 'Please enter your name and a message.'; return; }
        $('go').disabled = true;
        api('POST', 'Start', { name: name, email: $('e').value.trim() || null, message: msg }).then(function (r) {
            token = r.token; save(token); r.messages.forEach(add); open();
        }).catch(function (e) { $('serr').textContent = e.message; }).then(function () { $('go').disabled = false; });
    });

    $('form').addEventListener('submit', function (ev) {
        ev.preventDefault();
        var text = $('t').value.trim();
        if (!text) return;
        $('send').disabled = true; $('cerr').textContent = '';
        api('POST', 'Send', { text: text }).then(function () { $('t').value = ''; poll(); })
            .catch(function (e) { if (e.status === 401) end(); else $('cerr').textContent = e.message; })
            .then(function () { $('send').disabled = false; });
    });

    token = load();
    if (token) {
        api('GET', 'Poll').then(function (r) {
            if (r.closed) { save(null); token = null; return; }
            r.messages.forEach(add); open();
        }).catch(function () { save(null); token = null; });
    }
})();
