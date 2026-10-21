/* Saved views + bulk actions for the main grid of a list page.
   crm-grid.js calls CrmViews.attach(grid) for every main grid after it is created. */
(function () {
    'use strict';
    var BULK = { '/Leads/LeadList': 'Lead', '/Customers/CustomerList': 'Customer' };
    var CUSTOM = { '/Leads/LeadList': 'Lead', '/Customers/CustomerList': 'Customer', '/SupportCases/SupportCaseList': 'SupportCase' };
    var QUOTE = '/Quotations/QuotationList';
    var STAGES = ['Prospecting', 'Qualification', 'NeedAnalysis', 'Proposal', 'Negotiation', 'DecisionMaking', 'Won', 'Lost'];
    var LIFECYCLE = ['Subscriber', 'Lead', 'MarketingQualified', 'SalesQualified', 'Opportunity', 'Customer', 'Evangelist', 'Other'];

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
    function page() { return location.pathname.replace(/\/+$/, ''); }
    function api() { return typeof AxiosManager !== 'undefined' ? AxiosManager : null; }

    function capture(g) {
        var hidden = (g.getColumns ? g.getColumns() : []).filter(function (c) { return c.visible === false && c.field; }).map(function (c) { return c.field; });
        return {
            v: 1,
            search: (g.searchSettings && g.searchSettings.key) || '',
            filters: JSON.parse(JSON.stringify((g.filterSettings && g.filterSettings.columns) || [])),
            sorts: JSON.parse(JSON.stringify((g.sortSettings && g.sortSettings.columns) || [])),
            group: ((g.groupSettings && g.groupSettings.columns) || []).slice(),
            pageSize: g.pageSettings && g.pageSettings.pageSize,
            hidden: hidden
        };
    }

    function apply(g, st) {
        try {
            var cols = (g.getColumns ? g.getColumns() : []).filter(function (c) { return c.field; });
            var all = cols.map(function (c) { return c.field; });
            var hide = (st.hidden || []).filter(function (f) { return all.indexOf(f) > -1; });
            var show = all.filter(function (f) { return hide.indexOf(f) === -1; });
            if (show.length) g.showColumns(show, 'field');
            if (hide.length) g.hideColumns(hide, 'field');
            g.setProperties({
                searchSettings: { key: st.search || '' },
                filterSettings: { columns: st.filters || [] },
                sortSettings: { columns: st.sorts || [] },
                groupSettings: { columns: st.group || [] }
            });
            if (st.pageSize) g.pageSettings.pageSize = st.pageSize;
            if (g.refresh) g.refresh();
        } catch (e) { if (window.console) console.warn('apply view failed', e); }
    }

    function attach(g) {
        var A = api();
        if (!A || !g.element || g.element.__crmViews) return;
        g.element.__crmViews = true;
        var entity = BULK[page()];
        var bar = document.createElement('div');
        bar.className = 'crm-views-bar';
        bar.style.cssText = 'display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin:0 0 8px';
        bar.innerHTML =
            '<label class="small text-muted mb-0">View</label>' +
            '<select class="form-select form-select-sm cv-select" style="width:auto;min-width:180px"><option value="">Default view</option></select>' +
            '<button type="button" class="btn btn-sm btn-outline-secondary cv-save">Save view…</button>' +
            '<button type="button" class="btn btn-sm btn-outline-danger cv-del" style="display:none">Delete view</button>' +
            (page() === QUOTE ? '<button type="button" class="btn btn-sm btn-outline-secondary cv-qt" disabled>Discount &amp; approval…</button><button type="button" class="btn btn-sm btn-outline-primary cv-qs" disabled>Send for signature…</button>' : '') +
            (CUSTOM[page()] ? '<button type="button" class="btn btn-sm btn-outline-secondary cv-cf" disabled>Custom fields…</button><button type="button" class="btn btn-sm btn-outline-secondary cv-nf" disabled>Activities &amp; files…</button><button type="button" class="btn btn-sm btn-outline-primary cv-ai" disabled>AI assistant…</button>' : '') +
            (BULK[page()] ? '<button type="button" class="btn btn-sm btn-outline-secondary cv-mc" disabled>Messages &amp; calls…</button>' : '') +
            (page() === '/SupportCases/SupportCaseList' ? '<button type="button" class="btn btn-sm btn-outline-secondary cv-cat" disabled>Category…</button><button type="button" class="btn btn-sm btn-outline-secondary cv-rep" disabled>Reply to customer…</button><button type="button" class="btn btn-sm btn-outline-secondary cv-cs" disabled>Customer feedback…</button>' : '') +
            (entity ? '<span class="ms-auto"></span><span class="small text-muted cv-count"></span><button type="button" class="btn btn-sm btn-primary cv-bulk" disabled>Bulk actions…</button>' : '') +
            '<div class="cv-panel w-100" style="display:none"></div>';
        g.element.parentNode.insertBefore(bar, g.element);

        var sel = bar.querySelector('.cv-select'), panel = bar.querySelector('.cv-panel'), views = [];
        function msg(t, ok) { panel.style.display = ''; panel.innerHTML = '<div class="alert alert-' + (ok ? 'success' : 'danger') + ' py-1 px-2 mb-0 small">' + esc(t) + '</div>'; }

        function load(selectId) {
            A.get('/SavedView/GetSavedViewList?page=' + encodeURIComponent(page()), {}).then(function (r) {
                views = (r.data.content && r.data.content.data) || [];
                sel.innerHTML = '<option value="">Default view</option>' + views.map(function (v) {
                    return '<option value="' + esc(v.id) + '">' + esc(v.name) + (v.isShared ? (v.isMine ? ' (shared)' : ' (shared by ' + esc(v.ownerName) + ')') : '') + '</option>';
                }).join('');
                if (selectId) sel.value = selectId;
                bar.querySelector('.cv-del').style.display = current() && current().isMine ? '' : 'none';
            }).catch(function () { });
        }
        function current() { return views.filter(function (v) { return v.id === sel.value; })[0]; }

        sel.addEventListener('change', function () {
            var v = current();
            bar.querySelector('.cv-del').style.display = v && v.isMine ? '' : 'none';
            if (!v) { apply(g, { search: '', filters: [], sorts: [], group: [], hidden: [] }); return; }
            try { apply(g, JSON.parse(v.stateJson)); } catch (e) { msg('This view could not be read.', false); }
        });

        bar.querySelector('.cv-save').addEventListener('click', function () {
            var v = current();
            panel.style.display = '';
            panel.innerHTML = '<div class="d-flex flex-wrap gap-2 align-items-center border rounded p-2">' +
                '<input class="form-control form-control-sm cv-name" style="max-width:240px" maxlength="100" placeholder="View name" value="' + esc(v && v.isMine ? v.name : '') + '">' +
                '<label class="small mb-0"><input type="checkbox" class="cv-shared"' + (v && v.isShared ? ' checked' : '') + '> Share with everyone</label>' +
                '<button type="button" class="btn btn-sm btn-primary cv-ok">Save</button>' +
                '<button type="button" class="btn btn-sm btn-light cv-cancel">Cancel</button></div>';
            panel.querySelector('.cv-cancel').onclick = function () { panel.style.display = 'none'; };
            panel.querySelector('.cv-ok').onclick = function () {
                var name = panel.querySelector('.cv-name').value.trim();
                if (!name) { return; }
                A.post('/SavedView/SaveSavedView', { page: page(), name: name, stateJson: JSON.stringify(capture(g)), isShared: panel.querySelector('.cv-shared').checked })
                    .then(function (r) { var d = r.data.content && r.data.content.data; panel.style.display = 'none'; load(d && d.id); })
                    .catch(function () { msg('Could not save the view.', false); });
            };
        });

        bar.querySelector('.cv-del').addEventListener('click', function () {
            var v = current(); if (!v || !v.isMine) return;
            A.post('/SavedView/DeleteSavedView', { id: v.id }).then(function () { sel.value = ''; load(); }).catch(function () { msg('Could not delete the view.', false); });
        });

        if (page() === QUOTE) {
            var qBtn = bar.querySelector('.cv-qt');
            function qOne() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            var qsBtn = bar.querySelector('.cv-qs');
            function qState() { qBtn.disabled = !qOne(); qsBtn.disabled = !qOne(); }
            qsBtn.addEventListener('click', function () {
                var rec = qOne(); if (!rec || typeof Swal === 'undefined') return;
                Swal.fire({
                    title: 'Send ' + (rec.number || 'quotation') + ' for signature', showCancelButton: true, confirmButtonText: 'Send',
                    html: '<input id="qs-name" class="swal2-input" maxlength="200" placeholder="Signer full name"><input id="qs-mail" type="email" class="swal2-input" maxlength="255" placeholder="Signer e-mail">',
                    preConfirm: function () {
                        var name = document.getElementById('qs-name').value.trim(), mail = document.getElementById('qs-mail').value.trim();
                        if (!name || !mail) { Swal.showValidationMessage('Enter the signer name and e-mail.'); return false; }
                        return { name: name, mail: mail };
                    }
                }).then(function (ask) {
                    if (!ask.isConfirmed) return;
                    A.post('/Signature/Create', { relatedType: 'Quotation', relatedId: rec.id, signerName: ask.value.name, signerEmail: ask.value.mail, expiryDays: 14 }).then(function (x) {
                        var s = x.data.content || {};
                        msg('Sent as ' + (s.number || 'a signature request') + '. The signer has been e-mailed a link; the quotation turns Accepted when it is signed.', true);
                    }).catch(function (e) { msg((e && e.response && e.response.status === 403) ? 'Your role does not include signatures.' : ((e && e.response && e.response.data && e.response.data.message) || 'Could not send for signature.'), false); });
                });
            });
            g.addEventListener('rowSelected', qState);
            g.addEventListener('rowDeselected', qState);
            g.addEventListener('dataBound', qState);
            function money(v) { return (v || 0).toLocaleString(undefined, { maximumFractionDigits: 2 }); }
            function fail(e, d) { msg((e && e.response && e.response.data && e.response.data.message) || d, false); }
            function openQuote() {
                var rec = qOne(); if (!rec) return;
                A.get('/QuotationApproval/GetPricing?quotationId=' + encodeURIComponent(rec.id), {}).then(function (r) {
                    var p = r.data.content;
                    var badge = { None: 'secondary', Required: 'warning text-dark', Pending: 'info text-dark', Approved: 'success', Rejected: 'danger' }[p.approvalStatus] || 'secondary';
                    panel.style.display = '';
                    panel.innerHTML = '<div class="border rounded p-2"><div class="d-flex flex-wrap gap-3 align-items-end">' +
                        '<div><div class="small text-muted">' + esc(p.number) + (p.revisionNumber > 1 ? ' (revision ' + p.revisionNumber + ')' : '') + '</div><div>Subtotal <strong>' + money(p.subtotal) + '</strong> · Discount <strong>-' + money(p.discountAmount) + '</strong> · Net <strong>' + money(p.net) + '</strong> · With tax <strong>' + money(p.total) + '</strong></div></div>' +
                        '<div><label class="form-label small mb-0">Discount %</label><input type="number" min="0" max="100" step="0.5" class="form-control form-control-sm qt-pct" style="width:110px" value="' + esc(p.discountPercent) + '"' + (p.locked ? ' disabled' : '') + '></div>' +
                        '<button type="button" class="btn btn-sm btn-primary qt-apply"' + (p.locked ? ' disabled' : '') + '>Apply discount</button>' +
                        '<span class="badge bg-' + badge + '">Approval: ' + esc(p.approvalStatus) + '</span>' +
                        ((p.approvalStatus === 'Required' || p.approvalStatus === 'Rejected') ? '<input class="form-control form-control-sm qt-note" style="max-width:240px" placeholder="Note for the approver"><button type="button" class="btn btn-sm btn-warning qt-req">Request approval</button>' : '') +
                        '<button type="button" class="btn btn-sm btn-outline-secondary qt-rev"' + (p.locked ? ' disabled' : '') + '>New revision</button>' +
                        '<button type="button" class="btn btn-sm btn-light qt-close">Close</button></div>' +
                        '<div class="small text-muted mt-1">Discounts above ' + p.threshold + '% need approval before the quotation can be sent or converted.' + (p.approvalNote ? ' Note: ' + esc(p.approvalNote) : '') + '</div></div>';
                    panel.querySelector('.qt-close').onclick = function () { panel.style.display = 'none'; };
                    panel.querySelector('.qt-apply').onclick = function () {
                        A.post('/QuotationApproval/SetDiscount', { quotationId: rec.id, discountPercent: Number(panel.querySelector('.qt-pct').value) || 0 })
                            .then(function () { openQuote(); }).catch(function (e) { fail(e, 'Could not apply the discount.'); });
                    };
                    var req = panel.querySelector('.qt-req');
                    if (req) req.onclick = function () {
                        A.post('/QuotationApproval/RequestApproval', { quotationId: rec.id, note: panel.querySelector('.qt-note').value })
                            .then(function () { openQuote(); }).catch(function (e) { fail(e, 'Could not request approval.'); });
                    };
                    panel.querySelector('.qt-rev').onclick = function () {
                        A.post('/QuotationApproval/CreateRevision', { quotationId: rec.id }).then(function (r2) {
                            msg('Revision ' + (r2.data.content && r2.data.content.number) + ' created. The old version is now cancelled.', true);
                            setTimeout(function () { location.reload(); }, 1100);
                        }).catch(function (e) { fail(e, 'Could not create a revision.'); });
                    };
                }).catch(function (e) { fail(e, 'Could not load pricing.'); });
            }
            qBtn.addEventListener('click', openQuote);
        }

        var cfEntity = CUSTOM[page()];
        if (cfEntity) {
            var cfBtn = bar.querySelector('.cv-cf');
            function one() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            function cfState() { cfBtn.disabled = !one(); }
            g.addEventListener('rowSelected', cfState);
            g.addEventListener('rowDeselected', cfState);
            g.addEventListener('dataBound', cfState);
            function input(f) {
                var v = f.value == null ? '' : f.value;
                if (f.fieldType === 'Number') return '<input type="number" step="any" class="form-control form-control-sm" data-def="' + esc(f.definitionId) + '" value="' + esc(v) + '">';
                if (f.fieldType === 'Date') return '<input type="date" class="form-control form-control-sm" data-def="' + esc(f.definitionId) + '" value="' + esc(String(v).substring(0, 10)) + '">';
                if (f.fieldType === 'Boolean') return '<select class="form-select form-select-sm" data-def="' + esc(f.definitionId) + '"><option value=""></option><option value="true"' + (v === 'true' ? ' selected' : '') + '>Yes</option><option value="false"' + (v === 'false' ? ' selected' : '') + '>No</option></select>';
                if (f.fieldType === 'Picklist') return '<select class="form-select form-select-sm" data-def="' + esc(f.definitionId) + '"><option value=""></option>' + (f.options || []).map(function (o) { return '<option' + (o === v ? ' selected' : '') + '>' + esc(o) + '</option>'; }).join('') + '</select>';
                return '<input class="form-control form-control-sm" maxlength="2000" data-def="' + esc(f.definitionId) + '" value="' + esc(v) + '">';
            }
            cfBtn.addEventListener('click', function () {
                var rec = one(); if (!rec) return;
                A.get('/CustomField/GetValues?entity=' + cfEntity + '&recordId=' + encodeURIComponent(rec.id), {}).then(function (r) {
                    var fields = (r.data.content && r.data.content.data) || [];
                    panel.style.display = '';
                    if (!fields.length) { panel.innerHTML = '<div class="alert alert-info py-1 px-2 mb-0 small">No custom fields are defined for this record type yet. An administrator can add them under Settings → Custom Fields.</div>'; return; }
                    panel.innerHTML = '<div class="border rounded p-2"><div class="row g-2">' + fields.map(function (f) {
                        return '<div class="col-md-3"><label class="form-label small mb-0">' + esc(f.label) + (f.isRequired ? ' *' : '') + '</label>' + input(f) + '</div>';
                    }).join('') + '</div><div class="mt-2"><button type="button" class="btn btn-sm btn-primary cf-save">Save</button> <button type="button" class="btn btn-sm btn-light cf-cancel">Cancel</button></div></div>';
                    panel.querySelector('.cf-cancel').onclick = function () { panel.style.display = 'none'; };
                    panel.querySelector('.cf-save').onclick = function () {
                        var values = {};
                        Array.prototype.forEach.call(panel.querySelectorAll('[data-def]'), function (el) { values[el.getAttribute('data-def')] = el.value; });
                        A.post('/CustomField/SaveValues', { entity: cfEntity, recordId: rec.id, values: values }).then(function () { msg('Custom fields saved.', true); })
                            .catch(function (e) { msg((e && e.response && e.response.data && e.response.data.message) || 'Could not save.', false); });
                    };
                }).catch(function () { msg('Could not load custom fields.', false); });
            });
        }

        var nfEntity = CUSTOM[page()];
        if (nfEntity) {
            var nfBtn = bar.querySelector('.cv-nf');
            function nfOne() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            function nfState() { nfBtn.disabled = !nfOne(); }
            g.addEventListener('rowSelected', nfState);
            g.addEventListener('rowDeselected', nfState);
            g.addEventListener('dataBound', nfState);
            var TYPES = ['Task', 'Call', 'Meeting', 'Email', 'Note'];
            function nfDay(v) { return v ? String(v).substring(0, 10) : ''; }
            function nfSize(n) { n = Number(n) || 0; return n > 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(n / 1024)) + ' KB'; }
            function nfDenied(e) { return e && e.response && e.response.status === 403; }
            nfBtn.addEventListener('click', function () {
                var rec = nfOne(); if (!rec) return;
                var name = rec.name || rec.title || rec.number || '';
                var q = 'relatedType=' + nfEntity + '&relatedId=' + encodeURIComponent(rec.id);
                panel.style.display = '';
                panel.innerHTML = '<div class="border rounded p-2"><div class="d-flex align-items-center mb-2"><strong class="small">' + esc(name) + '</strong><button type="button" class="btn btn-sm btn-light ms-auto nf-close">Close</button></div>' +
                    '<div class="row g-3"><div class="col-lg-6"><div class="small fw-semibold mb-1">Activities</div>' +
                    '<div class="d-flex flex-wrap gap-1 mb-2"><input class="form-control form-control-sm nf-sub" style="max-width:200px" maxlength="255" placeholder="What needs doing?">' +
                    '<select class="form-select form-select-sm nf-type" style="width:auto">' + TYPES.map(function (t, i) { return '<option value="' + i + '">' + t + '</option>'; }).join('') + '</select>' +
                    '<input type="date" class="form-control form-control-sm nf-due" style="width:auto"><button type="button" class="btn btn-sm btn-primary nf-add">Add</button></div>' +
                    '<div class="nf-acts small text-muted">Loading…</div></div>' +
                    '<div class="col-lg-6"><div class="small fw-semibold mb-1">Files</div>' +
                    '<div class="d-flex flex-wrap gap-1 mb-2"><input type="file" class="form-control form-control-sm nf-file" style="max-width:240px"><button type="button" class="btn btn-sm btn-primary nf-up">Upload</button></div>' +
                    '<div class="nf-docs small text-muted">Loading…</div></div></div></div>';
                panel.querySelector('.nf-close').onclick = function () { panel.style.display = 'none'; };
                var acts = panel.querySelector('.nf-acts'), docs = panel.querySelector('.nf-docs');

                function loadActs() {
                    A.get('/Activity/GetActivityList?' + q, {}).then(function (r) {
                        var list = (r.data.content && r.data.content.data) || [];
                        acts.className = 'nf-acts small';
                        acts.innerHTML = list.length ? list.map(function (a) {
                            return '<div class="d-flex gap-2 py-1 border-bottom"><span class="flex-grow-1">' + esc(a.subject) + '<div class="text-muted">' + esc(a.typeName || '') + ' · ' + esc(a.statusName || '') + (a.startAt ? ' · ' + esc(nfDay(a.startAt)) : '') + '</div></span>' +
                                (a.statusName === 'Open' ? '<button type="button" class="btn btn-sm btn-outline-success py-0 nf-done" data-id="' + esc(a.id) + '">Done</button>' : '') + '</div>';
                        }).join('') : '<span class="text-muted">No activities yet.</span>';
                        Array.prototype.forEach.call(acts.querySelectorAll('.nf-done'), function (b) {
                            b.onclick = function () { A.post('/Activity/SetActivityStatus', { id: b.getAttribute('data-id'), status: 1 }).then(loadActs).catch(function () { msg('Could not update the activity.', false); }); };
                        });
                    }).catch(function (e) { acts.className = 'nf-acts small text-muted'; acts.textContent = nfDenied(e) ? 'Your role does not include Activities.' : 'Could not load activities.'; });
                }
                function loadDocs() {
                    A.get('/Document/GetDocumentList?' + q, {}).then(function (r) {
                        var list = (r.data.content && r.data.content.data) || [];
                        docs.className = 'nf-docs small';
                        docs.innerHTML = list.length ? list.map(function (d) {
                            return '<div class="d-flex gap-2 py-1 border-bottom"><span class="flex-grow-1">' + esc(d.originalName) + '<div class="text-muted">' + esc(nfSize(d.fileSize)) + ' · ' + esc(d.uploadedByName || '') + ' · ' + esc(nfDay(d.createdAtUtc)) + '</div></span>' +
                                '<button type="button" class="btn btn-sm btn-outline-secondary py-0 nf-dl" data-id="' + esc(d.id) + '" data-name="' + esc(d.originalName) + '">Download</button></div>';
                        }).join('') : '<span class="text-muted">No files yet.</span>';
                        Array.prototype.forEach.call(docs.querySelectorAll('.nf-dl'), function (b) {
                            b.onclick = function () {
                                A.get('/Document/Download?id=' + encodeURIComponent(b.getAttribute('data-id')), { responseType: 'blob' }).then(function (x) {
                                    var a = document.createElement('a'); a.href = URL.createObjectURL(x.data); a.download = b.getAttribute('data-name') || 'document'; a.click(); setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
                                }).catch(function () { msg('The file could not be downloaded.', false); });
                            };
                        });
                    }).catch(function (e) { docs.className = 'nf-docs small text-muted'; docs.textContent = nfDenied(e) ? 'Your role does not include Documents.' : 'Could not load files.'; });
                }
                loadActs(); loadDocs();

                panel.querySelector('.nf-add').onclick = function () {
                    var sub = panel.querySelector('.nf-sub').value.trim(); if (!sub) return;
                    var due = panel.querySelector('.nf-due').value;
                    A.post('/Activity/SaveActivity', { subject: sub, type: Number(panel.querySelector('.nf-type').value), priority: 'Normal', startAt: due ? due + 'T09:00:00' : null, relatedType: nfEntity, relatedId: rec.id, relatedName: name })
                        .then(function () { panel.querySelector('.nf-sub').value = ''; loadActs(); })
                        .catch(function (e) { acts.className = 'nf-acts small text-danger'; acts.textContent = nfDenied(e) ? 'Your role does not include Activities.' : ((e.response && e.response.data && e.response.data.message) || 'Could not add the activity.'); });
                };
                panel.querySelector('.nf-up').onclick = function () {
                    var f = panel.querySelector('.nf-file').files[0]; if (!f) return;
                    var fd = new FormData(); fd.append('file', f); fd.append('relatedType', nfEntity); fd.append('relatedId', rec.id); fd.append('relatedName', name); fd.append('description', '');
                    A.post('/Document/Upload', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
                        .then(function () { panel.querySelector('.nf-file').value = ''; loadDocs(); })
                        .catch(function (e) { docs.className = 'nf-docs small text-danger'; docs.textContent = nfDenied(e) ? 'Your role does not include Documents.' : 'Upload failed. Try another file.'; });
                };
            });
        }

        var aiEntity = CUSTOM[page()];
        if (aiEntity) {
            var aiBtn = bar.querySelector('.cv-ai');
            function aiOne() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            function aiState() { aiBtn.disabled = !aiOne(); }
            g.addEventListener('rowSelected', aiState);
            g.addEventListener('rowDeselected', aiState);
            g.addEventListener('dataBound', aiState);
            function aiErr(e) { return (e && e.response && e.response.status === 403) ? 'Your role does not allow this.' : ((e && e.response && e.response.data && e.response.data.message) || 'Something went wrong.'); }
            function aiShow(out, r, withCopy) {
                var src = (r.sources || []).map(function (s) { return '<a href="' + esc(s.url) + '">' + esc(s.title) + '</a>'; }).join(' · ');
                out.innerHTML = (r.notice ? '<div class="alert alert-warning py-1 px-2 small">' + esc(r.notice) + (r.mode === 'facts' ? ' Showing the plain facts instead.' : '') + '</div>' : '') +
                    (r.text ? '<div style="white-space:pre-wrap">' + esc(r.text) + '</div>' : '') +
                    (r.mode === 'ai' ? '<div class="small text-muted mt-1">Written by AI from the data below – please check it.</div>' : '') +
                    (src ? '<div class="small mt-1"><span class="text-muted">Based on:</span> ' + src + '</div>' : '');
            }
            aiBtn.addEventListener('click', function () {
                var rec = aiOne(); if (!rec) return;
                panel.style.display = '';
                panel.innerHTML = '<div class="border rounded p-2"><div class="d-flex flex-wrap gap-2 align-items-center">' +
                    '<strong class="small">' + esc(rec.number || rec.name || rec.title || '') + '</strong>' +
                    '<button type="button" class="btn btn-sm btn-primary ai-sum">Summarise</button>' +
                    (aiEntity === 'SupportCase' ? '<select class="form-select form-select-sm ai-tone" style="width:auto"><option>Friendly</option><option>Formal</option><option>Brief</option></select><button type="button" class="btn btn-sm btn-outline-primary ai-draft">Draft reply</button>' : '') +
                    '<input class="form-control form-control-sm ai-ins" style="max-width:300px" maxlength="500" placeholder="e.g. remind me to call them on Friday"><button type="button" class="btn btn-sm btn-outline-primary ai-task">Prepare task</button>' +
                    '<button type="button" class="btn btn-sm btn-light ai-close ms-auto">Close</button></div><div class="ai-out mt-2"></div></div>';
                var out = panel.querySelector('.ai-out');
                panel.querySelector('.ai-close').onclick = function () { panel.style.display = 'none'; };
                panel.querySelector('.ai-sum').onclick = function () {
                    out.innerHTML = '<span class="text-muted small">Working…</span>';
                    A.post('/Ai/Summarize', { entity: aiEntity, id: rec.id }).then(function (r) { aiShow(out, r.data.content || {}); }).catch(function (e) { out.innerHTML = '<div class="alert alert-danger py-1 px-2 small">' + esc(aiErr(e)) + '</div>'; });
                };
                var dr = panel.querySelector('.ai-draft');
                if (dr) dr.onclick = function () {
                    out.innerHTML = '<span class="text-muted small">Working…</span>';
                    A.post('/Ai/DraftReply', { caseId: rec.id, tone: panel.querySelector('.ai-tone').value }).then(function (r) {
                        var c = r.data.content || {};
                        if (!c.text) { aiShow(out, c); return; }
                        out.innerHTML = '<textarea class="form-control form-control-sm ai-draft-text" rows="6" maxlength="4000">' + esc(c.text) + '</textarea>' +
                            '<div class="small text-muted mt-1">Draft only – edit it, then send it to the customer yourself.</div>' +
                            '<button type="button" class="btn btn-sm btn-primary mt-1 ai-send">Send as reply to customer</button>';
                        out.querySelector('.ai-send').onclick = function () {
                            var t = out.querySelector('.ai-draft-text').value.trim(); if (!t) return;
                            A.post('/SupportInsight/ReplyToCustomer', { caseId: rec.id, text: t }).then(function () { msg('Reply sent.', true); }).catch(function (e) { msg(aiErr(e), false); });
                        };
                    }).catch(function (e) { out.innerHTML = '<div class="alert alert-danger py-1 px-2 small">' + esc(aiErr(e)) + '</div>'; });
                };
                panel.querySelector('.ai-task').onclick = function () {
                    var ins = panel.querySelector('.ai-ins').value.trim(); if (!ins) return;
                    out.innerHTML = '<span class="text-muted small">Working…</span>';
                    A.post('/Ai/ProposeAction', { entity: aiEntity, id: rec.id, instruction: ins }).then(function (r) {
                        var c = r.data.content || {};
                        if (!c.proposal) { out.innerHTML = '<div class="alert alert-warning py-1 px-2 small">' + esc(c.notice || 'No proposal.') + '</div>'; return; }
                        out.innerHTML = '<div class="small text-muted mb-1">Proposed task – nothing is created until you confirm:</div><div class="d-flex flex-wrap gap-2 align-items-center">' +
                            '<input class="form-control form-control-sm ai-sub" style="max-width:320px" maxlength="200" value="' + esc(c.proposal.subject) + '">' +
                            '<label class="small mb-0">due in</label><input type="number" min="0" max="365" class="form-control form-control-sm ai-due" style="width:80px" value="' + esc(c.proposal.dueInDays) + '"><span class="small">days</span>' +
                            '<select class="form-select form-select-sm ai-pri" style="width:auto"><option' + (c.proposal.priority === 'Low' ? ' selected' : '') + '>Low</option><option' + (c.proposal.priority === 'Normal' ? ' selected' : '') + '>Normal</option><option' + (c.proposal.priority === 'High' ? ' selected' : '') + '>High</option></select>' +
                            '<button type="button" class="btn btn-sm btn-success ai-ok">Confirm and create</button><button type="button" class="btn btn-sm btn-light ai-no">Cancel</button></div>';
                        out.querySelector('.ai-no').onclick = function () { out.innerHTML = ''; };
                        out.querySelector('.ai-ok').onclick = function () {
                            A.post('/Ai/ExecuteAction', { entity: aiEntity, id: rec.id, subject: out.querySelector('.ai-sub').value, dueInDays: Number(out.querySelector('.ai-due').value) || 0, priority: out.querySelector('.ai-pri').value })
                                .then(function (x) { msg('Task ' + ((x.data.content && x.data.content.number) || '') + ' created for you.', true); }).catch(function (e) { msg(aiErr(e), false); });
                        };
                    }).catch(function (e) { out.innerHTML = '<div class="alert alert-danger py-1 px-2 small">' + esc(aiErr(e)) + '</div>'; });
                };
            });
        }

        if (page() === '/SupportCases/SupportCaseList') {
            var csBtn = bar.querySelector('.cv-cs'), catBtn = bar.querySelector('.cv-cat'), repBtn = bar.querySelector('.cv-rep');
            function csOne() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            function csState() { csBtn.disabled = catBtn.disabled = repBtn.disabled = !csOne(); }
            g.addEventListener('rowSelected', csState);
            g.addEventListener('rowDeselected', csState);
            g.addEventListener('dataBound', csState);
            repBtn.addEventListener('click', function () {
                var rec = csOne(); if (!rec) return;
                panel.style.display = '';
                panel.innerHTML = '<div class="border rounded p-2"><div class="small text-muted mb-1">Reply on ' + esc(rec.number || '') + ' – the customer sees this in the portal and gets an e-mail.</div>' +
                    '<textarea class="form-control form-control-sm rp-text" rows="3" maxlength="4000"></textarea>' +
                    '<div class="mt-2"><button type="button" class="btn btn-sm btn-primary rp-send">Send reply</button> <button type="button" class="btn btn-sm btn-light rp-close">Close</button></div></div>';
                panel.querySelector('.rp-close').onclick = function () { panel.style.display = 'none'; };
                panel.querySelector('.rp-send').onclick = function () {
                    var text = panel.querySelector('.rp-text').value.trim(); if (!text) return;
                    A.post('/SupportInsight/ReplyToCustomer', { caseId: rec.id, text: text }).then(function () { msg('Reply sent.', true); })
                        .catch(function (e) { msg((e && e.response && e.response.data && e.response.data.message) || 'Could not send the reply.', false); });
                };
            });
            catBtn.addEventListener('click', function () {
                var rec = csOne(); if (!rec) return;
                Promise.all([A.get('/Knowledge/GetCategories?activeOnly=true', {}), A.get('/Knowledge/GetCaseCategory?caseId=' + encodeURIComponent(rec.id), {})]).then(function (rs) {
                    var cats = (rs[0].data.content && rs[0].data.content.data) || [], cur = rs[1].data.content || '';
                    panel.style.display = '';
                    if (!cats.length) { panel.innerHTML = '<div class="alert alert-info py-1 px-2 mb-0 small">No categories yet. An administrator can add them under Settings → Support Categories.</div>'; return; }
                    panel.innerHTML = '<div class="d-flex flex-wrap gap-2 align-items-center border rounded p-2"><span class="small text-muted">' + esc(rec.number || '') + '</span>' +
                        '<select class="form-select form-select-sm ct-sel" style="width:auto"><option value="">No category</option>' + cats.map(function (c) { return '<option value="' + esc(c.id) + '"' + (c.id === cur ? ' selected' : '') + '>' + esc(c.name) + '</option>'; }).join('') + '</select>' +
                        '<div class="form-check mb-0"><input class="form-check-input ct-route" type="checkbox" id="ctr"><label class="form-check-label small" for="ctr">Also route to the category\'s default owner and priority</label></div>' +
                        '<button type="button" class="btn btn-sm btn-primary ct-save">Save</button><button type="button" class="btn btn-sm btn-light ct-close">Close</button></div>';
                    panel.querySelector('.ct-close').onclick = function () { panel.style.display = 'none'; };
                    panel.querySelector('.ct-save').onclick = function () {
                        A.post('/Knowledge/AssignCategory', { caseId: rec.id, categoryId: panel.querySelector('.ct-sel').value || null, applyRouting: panel.querySelector('.ct-route').checked })
                            .then(function () { msg('Category saved.', true); g.refresh(); })
                            .catch(function (e) { msg((e && e.response && e.response.data && e.response.data.message) || 'Could not save.', false); });
                    };
                }).catch(function () { msg('Could not load categories.', false); });
            });
            csBtn.addEventListener('click', function () {
                var rec = csOne(); if (!rec) return;
                A.get('/SupportInsight/GetCsat?caseId=' + encodeURIComponent(rec.id), {}).then(function (r) {
                    var c = r.data.content || {};
                    panel.style.display = '';
                    var shown = c.score ? '<div class="mb-2"><strong>' + c.score + ' / 5</strong>' + (c.comment ? ' · ' + esc(c.comment) : '') + '<span class="text-muted small"> (' + esc(String(c.submittedAt || '').substring(0, 10)) + ')</span></div>' : '<div class="mb-2 text-muted small">No answer yet' + (c.surveySent ? ' (survey already sent).' : '.') + '</div>';
                    panel.innerHTML = '<div class="border rounded p-2"><div class="small text-muted mb-1">' + esc(c.number || '') + ' ' + esc(c.title || '') + '</div>' + shown +
                        '<div class="d-flex flex-wrap gap-2 align-items-center"><button type="button" class="btn btn-sm btn-primary cs-send">E-mail survey' + (c.customerEmail ? ' to ' + esc(c.customerEmail) : '') + '</button>' +
                        '<span class="text-muted small">or record by hand:</span><select class="form-select form-select-sm cs-score" style="width:auto"><option>5</option><option>4</option><option>3</option><option>2</option><option>1</option></select>' +
                        '<input class="form-control form-control-sm cs-comment" style="max-width:240px" maxlength="1000" placeholder="Comment (optional)"><button type="button" class="btn btn-sm btn-outline-primary cs-rec">Record</button>' +
                        '<button type="button" class="btn btn-sm btn-light cs-close">Close</button></div></div>';
                    panel.querySelector('.cs-close').onclick = function () { panel.style.display = 'none'; };
                    panel.querySelector('.cs-send').onclick = function () {
                        A.post('/SupportInsight/SendSurvey', { caseId: rec.id }).then(function (x) {
                            var d = x.data.content || {}; msg(d.sent ? 'Survey sent.' : (d.error || 'Could not send the survey.'), !!d.sent);
                        }).catch(function (e) { msg((e && e.response && e.response.data && e.response.data.message) || 'Could not send the survey.', false); });
                    };
                    panel.querySelector('.cs-rec').onclick = function () {
                        A.post('/SupportInsight/RecordCsat', { caseId: rec.id, score: Number(panel.querySelector('.cs-score').value), comment: panel.querySelector('.cs-comment').value })
                            .then(function () { msg('Feedback recorded.', true); })
                            .catch(function (e) { msg((e && e.response && e.response.data && e.response.data.message) || 'Could not record.', false); });
                    };
                }).catch(function () { msg('Could not load feedback.', false); });
            });
        }

        var mcEntity = BULK[page()];
        if (mcEntity) {
            var mcBtn = bar.querySelector('.cv-mc');
            function mcOne() { var r = g.getSelectedRecords() || []; return r.length === 1 ? r[0] : null; }
            function mcState() { mcBtn.disabled = !mcOne(); }
            g.addEventListener('rowSelected', mcState);
            g.addEventListener('rowDeselected', mcState);
            g.addEventListener('dataBound', mcState);
            var MC_ICON = { whatsapp: 'WhatsApp', sms: 'SMS', call: 'Call', email: 'E-mail' };
            function mcWhen(v) { return v ? new Date(String(v).endsWith('Z') ? v : v + 'Z').toLocaleString() : ''; }
            mcBtn.addEventListener('click', function () {
                var rec = mcOne(); if (!rec) return;
                A.get('/Messaging/GetRecordComms?relatedType=' + mcEntity + '&relatedId=' + encodeURIComponent(rec.id), {}).then(function (r) {
                    var d = r.data.content || {};
                    var wa = d.whatsApp || d.phone, sms = d.phone || d.whatsApp;
                    var saved = ''; try { saved = localStorage.getItem('crm.agentNumber') || ''; } catch (e) { /* storage may be blocked */ }
                    panel.style.display = '';
                    panel.innerHTML = '<div class="border rounded p-2"><div class="d-flex flex-wrap gap-2 align-items-center mb-2"><strong class="small">' + esc(d.name || '') + '</strong>' +
                        (d.phone ? '<span class="small text-muted">' + esc(d.phone) + '</span>' : '') +
                        (wa ? '<a class="btn btn-sm btn-outline-success" href="/Messaging/Inbox?channel=WhatsApp&address=' + encodeURIComponent(wa) + '">WhatsApp</a>' : '') +
                        (sms ? '<a class="btn btn-sm btn-outline-secondary" href="/Messaging/Inbox?channel=SMS&address=' + encodeURIComponent(sms) + '">SMS</a>' : '') +
                        (d.phone || d.whatsApp ? '<input class="form-control form-control-sm mc-agent" style="max-width:170px" maxlength="20" placeholder="Your number" value="' + esc(saved) + '"><button type="button" class="btn btn-sm btn-success mc-call">Call</button>' : '<span class="small text-muted">No phone number on this record.</span>') +
                        '<button type="button" class="btn btn-sm btn-light ms-auto mc-close">Close</button></div>' +
                        (wa || sms ? '<div class="d-flex flex-wrap gap-2 align-items-start mb-2 mc-send">' +
                            '<select class="form-select form-select-sm mc-ch" style="width:auto">' + (wa ? '<option value="WhatsApp">WhatsApp</option>' : '') + (sms ? '<option value="SMS">SMS</option>' : '') + '</select>' +
                            '<select class="form-select form-select-sm mc-tpl" style="width:auto;max-width:200px"><option value="">Type a message</option></select>' +
                            '<textarea class="form-control form-control-sm mc-text" rows="2" style="flex:1;min-width:220px" maxlength="1000" placeholder="Write a message"></textarea>' +
                            '<div class="mc-vars d-flex flex-wrap gap-2"></div>' +
                            '<button type="button" class="btn btn-sm btn-primary mc-go">Send</button></div>' : '') +
                        '<div class="mc-note small mb-1"></div>' +
                        '<div class="small">' + ((d.items || []).length ? d.items.map(function (i) {
                            return '<div class="d-flex gap-2 py-1 border-bottom"><span class="badge bg-secondary" style="min-width:70px">' + esc(MC_ICON[i.kind] || i.kind) + '</span>' +
                                '<span class="flex-grow-1">' + esc(i.direction === 'In' ? 'Received' : 'Sent') + (i.status ? ' · ' + esc(i.status) : '') + (i.text ? ': ' + esc(i.text) : '') + '<div class="text-muted">' + esc(mcWhen(i.atUtc)) + (i.by ? ' · ' + esc(i.by) : '') + '</div></span></div>';
                        }).join('') : '<span class="text-muted">Nothing yet.</span>') + '</div></div>';
                    panel.querySelector('.mc-close').onclick = function () { panel.style.display = 'none'; };
                    var call = panel.querySelector('.mc-call');
                    if (call) call.onclick = function () {
                        var agent = panel.querySelector('.mc-agent').value.trim(), note = panel.querySelector('.mc-note');
                        if (!agent) { note.className = 'mc-note small text-danger mb-1'; note.textContent = 'Enter your own phone number first.'; return; }
                        A.post('/Call/Start', { to: d.phone || d.whatsApp, agentNumber: agent }).then(function (x) {
                            try { localStorage.setItem('crm.agentNumber', agent); } catch (e) { /* ignore */ }
                            var s = x.data.content || {};
                            note.className = 'mc-note small mb-1 ' + (s.status === 'Failed' ? 'text-danger' : 'text-success');
                            note.textContent = s.status === 'Failed' ? (s.error || 'The call could not be started.') : 'Calling you now. Pick up; the customer is dialled next.';
                        }).catch(function (e) {
                            note.className = 'mc-note small text-danger mb-1';
                            note.textContent = (e && e.response && e.response.status === 403) ? 'Your role does not include Calls.' : ((e && e.response && e.response.data && e.response.data.message) || 'Could not start the call.');
                        });
                    };

                    //>>> quick send: free text, or an approved template with its variables filled in
                    var go = panel.querySelector('.mc-go');
                    if (go) {
                        var chSel = panel.querySelector('.mc-ch'), tplSel = panel.querySelector('.mc-tpl'), txt = panel.querySelector('.mc-text'), vbox = panel.querySelector('.mc-vars'), tpls = [];
                        var sNote = panel.querySelector('.mc-note');
                        function say(t, ok) { sNote.className = 'mc-note small mb-1 ' + (ok ? 'text-success' : 'text-danger'); sNote.textContent = t; }
                        function curTpl() { return tpls.filter(function (t) { return t.id === tplSel.value; })[0]; }
                        function loadTpl() {
                            tplSel.innerHTML = '<option value="">Type a message</option>'; vbox.innerHTML = ''; txt.disabled = false;
                            A.get('/Messaging/GetTemplates?channel=' + chSel.value, {}).then(function (x) {
                                tpls = ((x.data.content || {}).data || []).filter(function (t) { return t.isActive; });
                                tplSel.innerHTML = '<option value="">Type a message</option>' + tpls.map(function (t) { return '<option value="' + esc(t.id) + '">' + esc(t.name) + '</option>'; }).join('');
                            }).catch(function () { /* templates are optional */ });
                        }
                        chSel.onchange = loadTpl;
                        tplSel.onchange = function () {
                            var t = curTpl(); vbox.innerHTML = '';
                            txt.disabled = !!t;
                            if (!t) return;
                            txt.value = t.body || '';
                            (t.variables ? t.variables.split(',') : []).filter(Boolean).forEach(function (v) {
                                var key = v.trim(), pre = /name|customer|contact/i.test(key) ? (d.name || '') : '';
                                vbox.insertAdjacentHTML('beforeend', '<input class="form-control form-control-sm mc-var" style="width:140px" data-k="' + esc(key) + '" placeholder="' + esc(key) + '" value="' + esc(pre) + '">');
                            });
                        };
                        go.onclick = function () {
                            var t = curTpl(), vars = {};
                            panel.querySelectorAll('.mc-var').forEach(function (i) { vars[i.getAttribute('data-k')] = i.value; });
                            var body = { channel: chSel.value, to: chSel.value === 'SMS' ? sms : wa, text: t ? null : txt.value.trim(), templateId: t ? t.id : null, variables: t ? vars : null };
                            if (!t && !body.text) { say('Write a message or choose a template.', false); return; }
                            go.disabled = true;
                            A.post('/Messaging/Send', body).then(function (x) {
                                var s = x.data.content || {};
                                if (s.status === 'Failed') say(s.error || 'The message could not be sent.', false);
                                else { say('Message sent.', true); txt.value = ''; if (!t) txt.disabled = false; }
                            }).catch(function (e) {
                                say((e && e.response && e.response.status === 403) ? 'Your role does not include Messaging.' : ((e && e.response && e.response.data && e.response.data.message) || 'Could not send the message.'), false);
                            }).then(function () { go.disabled = false; });
                        };
                        loadTpl();
                    }
                }).catch(function (e) { msg((e && e.response && e.response.status === 403) ? 'Your role does not include Messaging.' : 'Could not load messages and calls.', false); });
            });
        }

        if (entity) {
            var bulk = bar.querySelector('.cv-bulk'), count = bar.querySelector('.cv-count'), assignees = null;
            function ids() { return (g.getSelectedRecords() || []).map(function (r) { return r.id; }).filter(Boolean); }
            function refreshBtn() { var n = ids().length; bulk.disabled = n === 0; count.textContent = n ? n + ' selected' : ''; }
            g.addEventListener('rowSelected', refreshBtn);
            g.addEventListener('rowDeselected', refreshBtn);
            g.addEventListener('dataBound', refreshBtn);

            function actions() {
                var a = [['SetOwner', 'Change owner'], ['AddTag', 'Add tag'], ['RemoveTag', 'Remove tag'], ['SetSource', 'Set source'], ['SetConsent', 'Set marketing consent']];
                a.push(entity === 'Lead' ? ['SetStage', 'Move to stage'] : ['SetLifecycle', 'Set lifecycle stage']);
                return a;
            }
            function valueControl(action) {
                if (action === 'SetOwner') return '<select class="form-select form-select-sm cv-val" style="width:auto">' + (assignees || []).map(function (u) { return '<option value="' + esc(u.id) + '">' + esc(u.name) + '</option>'; }).join('') + '</select>';
                if (action === 'SetStage') return '<select class="form-select form-select-sm cv-val" style="width:auto">' + STAGES.map(function (s) { return '<option>' + s + '</option>'; }).join('') + '</select>';
                if (action === 'SetLifecycle') return '<select class="form-select form-select-sm cv-val" style="width:auto">' + LIFECYCLE.map(function (s) { return '<option>' + s + '</option>'; }).join('') + '</select>';
                if (action === 'SetConsent') return '<select class="form-select form-select-sm cv-val" style="width:auto"><option value="yes">Agreed</option><option value="no">Opted out</option><option value="">Unknown</option></select>';
                return '<input class="form-control form-control-sm cv-val" style="max-width:220px" maxlength="100" placeholder="Value">';
            }
            bulk.addEventListener('click', function () {
                function open() {
                    panel.style.display = '';
                    panel.innerHTML = '<div class="d-flex flex-wrap gap-2 align-items-center border rounded p-2">' +
                        '<strong class="small">' + ids().length + ' ' + entity.toLowerCase() + '(s):</strong>' +
                        '<select class="form-select form-select-sm cv-act" style="width:auto">' + actions().map(function (a) { return '<option value="' + a[0] + '">' + a[1] + '</option>'; }).join('') + '</select>' +
                        '<span class="cv-valwrap"></span>' +
                        '<button type="button" class="btn btn-sm btn-primary cv-go">Apply</button>' +
                        '<button type="button" class="btn btn-sm btn-light cv-cancel">Cancel</button></div>';
                    var act = panel.querySelector('.cv-act'), wrap = panel.querySelector('.cv-valwrap');
                    function draw() { wrap.innerHTML = valueControl(act.value); }
                    act.onchange = draw; draw();
                    panel.querySelector('.cv-cancel').onclick = function () { panel.style.display = 'none'; };
                    panel.querySelector('.cv-go').onclick = function () {
                        var val = panel.querySelector('.cv-val').value;
                        A.post('/Bulk/Update', { entity: entity, ids: ids(), action: act.value, value: val }).then(function (r) {
                            var n = r.data.content && r.data.content.updated;
                            msg(n + ' record(s) updated.', true);
                            setTimeout(function () { location.reload(); }, 900);
                        }).catch(function (e) {
                            var m = e && e.response && e.response.data && e.response.data.message;
                            msg(m || 'Bulk update failed.', false);
                        });
                    };
                }
                if (assignees) return open();
                A.get('/Activity/GetAssignees', {}).then(function (r) { assignees = r.data.content || []; open(); }).catch(function () { assignees = []; open(); });
            });
        }
        load();
    }

    window.CrmViews = { attach: attach };
})();
