/* Free CRM shell: theme toggle, command palette (Ctrl/Cmd+K) and the assistant drawer. */
(function () {
    'use strict';

    function safe(fn, fallback) { try { return fn(); } catch (e) { return fallback; } }

    /* ---------------- theme ---------------- */
    var Theme = {
        get: function () { return safe(function () { return localStorage.getItem('crm-theme'); }, null); },
        apply: function (t) { document.documentElement.setAttribute('data-theme', t); },
        toggle: function () {
            var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            Theme.apply(next);
            safe(function () { localStorage.setItem('crm-theme', next); });
            Theme.icon();
        },
        icon: function () {
            var b = document.getElementById('themeToggle');
            if (b) {
                var dark = document.documentElement.getAttribute('data-theme') === 'dark';
                b.innerHTML = '<i class="fas ' + (dark ? 'fa-sun' : 'fa-moon') + '"></i>';
                b.title = dark ? 'Switch to light mode' : 'Switch to dark mode';
            }
        }
    };

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    function hasApi() { return typeof AxiosManager !== 'undefined' && typeof StorageManager !== 'undefined'; }

    /* ---------------- command palette ---------------- */
    var Palette = {
        overlay: null, input: null, list: null, items: [], active: 0, timer: null, seq: 0,

        build: function () {
            var o = el('div', 'cp-overlay');
            o.setAttribute('role', 'dialog');
            o.setAttribute('aria-modal', 'true');
            o.setAttribute('aria-label', 'Command palette');
            var box = el('div', 'cp-box');
            var input = el('input', 'cp-input');
            input.type = 'text';
            input.placeholder = 'Search pages, customers, leads, invoices... or type a command';
            input.setAttribute('aria-label', 'Search');
            var list = el('div', 'cp-list');
            var foot = el('div', 'cp-foot');
            foot.appendChild(el('span', null, '↑↓ navigate'));
            foot.appendChild(el('span', null, '↵ open'));
            foot.appendChild(el('span', null, 'Esc close'));
            box.appendChild(input); box.appendChild(list); box.appendChild(foot);
            o.appendChild(box);
            document.body.appendChild(o);
            Palette.overlay = o; Palette.input = input; Palette.list = list;

            o.addEventListener('mousedown', function (e) { if (e.target === o) Palette.close(); });
            input.addEventListener('input', function () { Palette.onType(); });
            input.addEventListener('keydown', function (e) {
                if (e.key === 'ArrowDown') { e.preventDefault(); Palette.move(1); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); Palette.move(-1); }
                else if (e.key === 'Enter') { e.preventDefault(); Palette.run(Palette.items[Palette.active]); }
                else if (e.key === 'Escape') { Palette.close(); }
            });
        },

        roles: function () {
            return safe(function () { return (StorageManager.getUserRoles() || []).map(function (r) { return String(r).toLowerCase(); }); }, []);
        },

        pages: function () {
            var roles = Palette.roles();
            var menu = safe(function () { return StorageManager.getMenuNavigation() || []; }, []);
            return menu.filter(function (m) {
                if (!m.navURL) return false;
                var seg = String(m.navURL).split('/')[1] || '';
                if (roles.indexOf(seg.toLowerCase()) < 0) return false;
                return !m.roles || !m.roles.length || m.roles.some(function (r) { return roles.indexOf(String(r).toLowerCase()) >= 0; });
            }).map(function (m) {
                return { group: 'Go to', icon: 'fa-arrow-right', title: m.name, sub: m.navURL, url: m.navURL };
            });
        },

        actions: function () {
            var mine = Palette.roles();
            var can = function (a) { return !a.role || mine.indexOf(a.role.toLowerCase()) >= 0; };
            return [
                { group: 'Actions', icon: 'fa-wand-magic-sparkles', title: 'Ask the assistant', sub: 'What should I do today?', fn: function () { Assistant.open('What should I do today?'); } },
                { group: 'Actions', icon: 'fa-circle-half-stroke', title: 'Toggle dark mode', sub: '', fn: Theme.toggle },
                { group: 'Actions', icon: 'fa-plus', title: 'New quotation', sub: 'Sales', role: 'Quotations', url: '/Quotations/QuotationList' },
                { group: 'Actions', icon: 'fa-plus', title: 'New invoice', sub: 'Sales', role: 'Invoices', url: '/Invoices/InvoiceList' },
                { group: 'Actions', icon: 'fa-plus', title: 'New lead', sub: 'CRM', role: 'Leads', url: '/Leads/LeadList' },
                { group: 'Actions', icon: 'fa-plus', title: 'New support case', sub: 'Support', role: 'SupportCases', url: '/SupportCases/SupportCaseList' },
                { group: 'Actions', icon: 'fa-gauge-high', title: 'Open my dashboard', sub: 'Your day at a glance', url: (window.SecurityManager && SecurityManager.homeUrl) ? SecurityManager.homeUrl() : '/Dashboards/MyWork' }
            ].filter(can);
        },

        recents: function () {
            var r = safe(function () { return JSON.parse(localStorage.getItem('crm-recents') || '[]'); }, []);
            return r.map(function (x) { return { group: 'Recent', icon: 'fa-clock-rotate-left', title: x.title, sub: x.sub, url: x.url }; });
        },

        remember: function (item) {
            if (!item || !item.url) return;
            var list = safe(function () { return JSON.parse(localStorage.getItem('crm-recents') || '[]'); }, []);
            list = list.filter(function (x) { return x.url !== item.url || x.title !== item.title; });
            list.unshift({ title: item.title, sub: item.sub, url: item.url });
            safe(function () { localStorage.setItem('crm-recents', JSON.stringify(list.slice(0, 5))); });
        },

        open: function () {
            if (!Palette.overlay) Palette.build();
            Palette.overlay.classList.add('open');
            Palette.input.value = '';
            Palette.onType();
            setTimeout(function () { Palette.input.focus(); }, 0);
        },

        close: function () { if (Palette.overlay) Palette.overlay.classList.remove('open'); },

        score: function (text, q) {
            text = String(text || '').toLowerCase();
            if (text.indexOf(q) === 0) return 3;
            if (text.indexOf(q) > 0) return 2;
            //>>> fuzzy: all characters of q appear in order
            var i = 0;
            for (var c = 0; c < text.length && i < q.length; c++) { if (text[c] === q[i]) i++; }
            return i === q.length ? 1 : 0;
        },

        onType: function () {
            var q = Palette.input.value.trim().toLowerCase();
            var local = [];
            if (!q) {
                local = Palette.recents().concat(Palette.actions());
            } else {
                var pool = Palette.actions().concat(Palette.pages());
                pool.forEach(function (it) {
                    var s = Math.max(Palette.score(it.title, q), Palette.score(it.sub, q) - 1);
                    if (s > 0) { it._s = s; local.push(it); }
                });
                local.sort(function (a, b) { return b._s - a._s; });
                local = local.slice(0, 10);
            }
            Palette.items = local;
            Palette.active = 0;
            Palette.render();

            clearTimeout(Palette.timer);
            if (q.length >= 2 && hasApi()) {
                var my = ++Palette.seq;
                Palette.timer = setTimeout(function () {
                    AxiosManager.get('/Search/GlobalSearch?q=' + encodeURIComponent(q), {}).then(function (r) {
                        if (my !== Palette.seq) return;
                        var hits = (r && r.data && r.data.content && r.data.content.hits) || [];
                        var mapped = hits.map(function (h) { return { group: h.type + 's', icon: 'fa-magnifying-glass', title: h.title, sub: h.subtitle, url: h.url }; });
                        Palette.items = Palette.items.concat(mapped);
                        Palette.render();
                    }).catch(function () { });
                }, 220);
            }
        },

        render: function () {
            var list = Palette.list;
            list.textContent = '';
            if (!Palette.items.length) {
                list.appendChild(el('div', 'empty-state', 'No matches. Try a customer name, a document number, or a page name.'));
                return;
            }
            var lastGroup = null;
            Palette.items.forEach(function (it, idx) {
                if (it.group !== lastGroup) { list.appendChild(el('div', 'cp-group', it.group)); lastGroup = it.group; }
                var row = el('div', 'cp-item' + (idx === Palette.active ? ' active' : ''));
                var icon = el('i', 'fas ' + it.icon);
                row.appendChild(icon);
                row.appendChild(el('span', null, it.title));
                if (it.sub) row.appendChild(el('span', 'cp-sub', it.sub));
                row.addEventListener('mousemove', function () { Palette.active = idx; Palette.highlight(); });
                row.addEventListener('click', function () { Palette.run(it); });
                list.appendChild(row);
            });
        },

        highlight: function () {
            var rows = Palette.list.querySelectorAll('.cp-item');
            rows.forEach(function (r, i) { r.classList.toggle('active', i === Palette.active); });
            var cur = rows[Palette.active];
            if (cur && cur.scrollIntoView) cur.scrollIntoView({ block: 'nearest' });
        },

        move: function (d) {
            if (!Palette.items.length) return;
            Palette.active = (Palette.active + d + Palette.items.length) % Palette.items.length;
            Palette.highlight();
        },

        run: function (it) {
            if (!it) return;
            Palette.close();
            if (it.fn) { it.fn(); return; }
            Palette.remember(it);
            if (it.url) window.location.href = it.url;
        }
    };

    /* ---------------- assistant ---------------- */
    var Assistant = {
        drawer: null, body: null, input: null, started: false, busy: false,

        build: function () {
            var d = el('aside', 'ai-drawer');
            d.setAttribute('aria-label', 'CRM assistant');
            var head = el('div', 'ai-head');
            head.appendChild(el('i', 'fas fa-wand-magic-sparkles'));
            head.appendChild(el('h6', null, 'CRM assistant'));
            head.appendChild(el('span', 'ai-mode', 'Live data'));
            var close = el('button', 'shell-iconbtn ml-auto');
            close.type = 'button';
            close.title = 'Close';
            close.innerHTML = '<i class="fas fa-xmark"></i>';
            close.addEventListener('click', Assistant.close);
            head.appendChild(close);
            var body = el('div', 'ai-body');
            var foot = el('div', 'ai-foot');
            var form = el('form', 'ai-form');
            var input = el('input');
            input.type = 'text';
            input.placeholder = 'Ask about deals, invoices, cases... or "draft reminder for INV-0001"';
            input.setAttribute('aria-label', 'Ask the assistant');
            var send = el('button', 'btn btn-primary btn-sm', 'Send');
            send.type = 'submit';
            form.appendChild(input); form.appendChild(send);
            foot.appendChild(form);
            d.appendChild(head); d.appendChild(body); d.appendChild(foot);
            document.body.appendChild(d);
            Assistant.drawer = d; Assistant.body = body; Assistant.input = input;
            form.addEventListener('submit', function (e) { e.preventDefault(); Assistant.ask(input.value); input.value = ''; });
        },

        open: function (question) {
            if (!Assistant.drawer) Assistant.build();
            Assistant.drawer.classList.add('open');
            if (!Assistant.started) {
                Assistant.started = true;
                Assistant.say('bot', 'Hi! I read your live CRM data. Ask me what needs attention, which deals are at risk, or to draft a follow-up for a record number.', [
                    'What should I do today?', 'Which invoices are overdue?', 'Which deals are at risk?', 'How is the forecast?'
                ]);
            }
            if (question) Assistant.ask(question);
            else setTimeout(function () { Assistant.input.focus(); }, 0);
        },

        close: function () { if (Assistant.drawer) Assistant.drawer.classList.remove('open'); },

        say: function (who, text, chips, links) {
            var m = el('div', 'ai-msg ' + who);
            m.appendChild(document.createTextNode(text));
            var hasChips = chips && chips.length, hasLinks = links && links.length;
            if (who === 'bot' && (hasChips || hasLinks)) {
                var a = el('div', 'ai-actions');
                (links || []).forEach(function (l) {
                    var link = el('a', 'ai-chip', l.title);
                    link.href = l.url;
                    a.appendChild(link);
                });
                (chips || []).forEach(function (c) {
                    var b = el('button', 'ai-chip', c);
                    b.type = 'button';
                    b.addEventListener('click', function () { Assistant.ask(c); });
                    a.appendChild(b);
                });
                m.appendChild(a);
            }
            Assistant.body.appendChild(m);
            Assistant.body.scrollTop = Assistant.body.scrollHeight;
            return m;
        },

        ask: function (q) {
            q = String(q || '').trim();
            if (!q || Assistant.busy || !hasApi()) return;
            Assistant.busy = true;
            Assistant.say('user', q);
            var typing = Assistant.say('bot', '');
            typing.innerHTML = '<span class="ai-typing"><i></i><i></i><i></i></span>';
            AxiosManager.post('/Assistant/Ask', { question: q }).then(function (r) {
                var data = r && r.data && r.data.content && r.data.content.data;
                typing.remove();
                if (!data) { Assistant.say('bot', 'Sorry, I could not get an answer. Please try again.'); return; }
                Assistant.say('bot', data.answer, data.suggestions, data.links);
                var badge = Assistant.drawer.querySelector('.ai-mode');
                if (badge) badge.textContent = data.mode === 'ai' ? 'AI' : 'Live data';
            }).catch(function () {
                typing.remove();
                Assistant.say('bot', 'Sorry, something went wrong while reading your data.');
            }).then(function () { Assistant.busy = false; });
        }
    };

    /* ---------------- boot ---------------- */
    Theme.icon();
    document.addEventListener('DOMContentLoaded', function () {
        var crumb = document.getElementById('pageCrumb');
        if (!crumb || !hasApi()) return;
        safe(function () {
            var menu = StorageManager.getMenuNavigation() || [];
            var path = window.location.pathname.toLowerCase();
            var cur = menu.filter(function (m) { return m.navURL && String(m.navURL).toLowerCase() === path; })[0];
            if (!cur) return;
            var parent = menu.filter(function (m) { return m.id === cur.pid; })[0];
            crumb.textContent = parent ? parent.name + '  \u203A  ' + cur.name : cur.name;
        });
    });
    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) { e.preventDefault(); Palette.open(); }
        else if (e.key === 'Escape') { Assistant.close(); }
    });
    document.addEventListener('click', function (e) {
        var t = e.target.closest ? e.target.closest('[data-shell]') : null;
        if (!t) return;
        e.preventDefault();
        var what = t.getAttribute('data-shell');
        if (what === 'search') Palette.open();
        else if (what === 'assistant') Assistant.open();
        else if (what === 'theme') Theme.toggle();
    });

    window.CrmShell = { openAssistant: Assistant.open, openPalette: Palette.open, toggleTheme: Theme.toggle };
})();
