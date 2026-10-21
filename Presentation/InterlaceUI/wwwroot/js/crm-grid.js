/* Global data-grid enhancer: gives every Syncfusion grid modern defaults (Excel-style filters,
   proper paging, column chooser, row actions, viewport-fit height) without touching each page.
   ej.grids is frozen, so we hook Grid.prototype.appendTo and adjust the instance before it renders. */
(function () {
    'use strict';
    if (!window.ej || !ej.grids || !ej.grids.Grid || ej.grids.Grid.prototype.__crmHooked) return;
    var Base = ej.grids.Grid;
    var PAGE_SIZES = ['10', '20', '50', '100', '200', 'All'];

    try {
        if (ej.grids.ColumnChooser) Base.Inject(ej.grids.ColumnChooser);
        if (ej.base && ej.base.L10n) {
            ej.base.L10n.load({ 'en-US': { grid: {
                EmptyRecord: 'No records to display', EmptyDataSourceError: 'No records to display',
                FilterButton: 'Apply', ClearButton: 'Clear', SelectAll: 'Select all', Blanks: 'Blanks',
                ColumnChooserDialogTitle: 'Choose columns', ColumnChooserOkButton: 'Apply', ColumnChooserCancelButton: 'Cancel'
            } } });
        }
    } catch (e) { /* optional */ }

    function isMain(g) {
        return typeof g.height === 'string' && parseInt(g.height, 10) >= 240;
    }

    function addActions(g) {
        var tb = g.toolbar;
        if (!Array.isArray(tb) || !Array.isArray(g.columns)) return;
        var ids = tb.map(function (i) { return i && i.id; });
        var hasEdit = ids.indexOf('EditCustom') > -1, hasDel = ids.indexOf('DeleteCustom') > -1;
        if (!hasEdit && !hasDel) return;
        var html = '<div class="row-actions">' +
            (hasEdit ? '<button type="button" class="ra ra-edit" data-act="EditCustom" title="Edit"><svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg></button>' : '') +
            (hasDel ? '<button type="button" class="ra ra-del" data-act="DeleteCustom" title="Delete"><svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18"/><path d="M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/></svg></button>' : '') +
            '</div>';
        var col = { headerText: 'Actions', width: hasEdit && hasDel ? 96 : 60, minWidth: 60, textAlign: 'Center', allowFiltering: false, allowSorting: false,
            allowGrouping: false, allowResizing: false, allowReordering: false, showInColumnChooser: false, template: html };
        var cols = g.columns.slice(), at = 0;
        for (var i = 0; i < cols.length; i++) if (cols[i] && cols[i].type === 'checkbox') at = i + 1;
        cols.splice(at, 0, col);
        g.setProperties({ columns: cols }, true);
    }

    function enhance(g) {
        var main = isMain(g);
        if (g.allowFiltering) g.setProperties({ filterSettings: { type: 'Excel' } }, true);
        if (g.allowPaging !== false) {
            var ps = {};
            if (main) { ps.pageSizes = PAGE_SIZES; if (!g.pageSettings.pageSize || g.pageSettings.pageSize === 'All') ps.pageSize = 20; }
            g.setProperties({ pageSettings: ps }, true);
        }
        g.setProperties({ enableHover: true }, true);
        if (main) {
            g.setProperties({ rowHeight: 46, allowReordering: true }, true);
            if (g.allowFiltering && g.showColumnMenu) g.setProperties({ showColumnMenu: false }, true);
            addActions(g);
            var tb = g.toolbar;
            if (Array.isArray(tb) && ej.grids.ColumnChooser && tb.indexOf('Search') > -1 && tb.indexOf('ColumnChooser') === -1) {
                g.setProperties({ toolbar: tb.concat([{ type: 'Separator' }, 'ColumnChooser']), showColumnChooser: true }, true);
            }
        }
    }

    function onAction(e, grid) {
        var b = e.target && e.target.closest ? e.target.closest('.ra') : null;
        if (!b) return;
        e.preventDefault(); e.stopPropagation();
        var info = grid.getRowInfo(b);
        if (!info || info.rowIndex === undefined || info.rowIndex === null) return;
        grid.clearSelection();
        grid.selectRow(info.rowIndex);
        var act = b.getAttribute('data-act');
        setTimeout(function () {
            var host = grid.element.querySelector('.e-toolbar');
            if (!host) return;
            var item = host.querySelector('[id="' + act + '"]');
            if (!item) {
                var label = act === 'EditCustom' ? 'Edit' : 'Delete';
                Array.prototype.forEach.call(host.querySelectorAll('.e-toolbar-item'), function (n) { if (n.textContent.trim() === label) item = n; });
            }
            var btn = item && (item.matches && item.matches('.e-tbar-btn') ? item : item.querySelector('.e-tbar-btn'));
            if (btn) btn.click();
        }, 0);
    }

    function fit(grid) {
        try {
            var el = grid.element;
            if (!el || !el.getBoundingClientRect) return;
            var c = el.querySelector('.e-gridcontent');
            var pager = el.querySelector('.e-gridpager');
            if (!c) return;
            var absTop = c.getBoundingClientRect().top + window.pageYOffset;
            var h = Math.max(300, Math.floor(window.innerHeight - absTop - (pager ? pager.offsetHeight : 64) - 36));
            if (grid.height !== h) grid.height = h;
        } catch (e) { /* ignore */ }
    }

    var original = Base.prototype.appendTo;
    Base.prototype.appendTo = function () {
        var grid = this;
        if (!grid.__crm) {
            grid.__crm = true;
            var main = isMain(grid);
            try { enhance(grid); } catch (e) { if (window.console) console.warn('crm-grid enhance skipped', e); }
            var result = original.apply(grid, arguments);
            if (grid.element) grid.element.addEventListener('click', function (e) { onAction(e, grid); });
            if (main) {
                try { if (window.CrmViews) window.CrmViews.attach(grid); } catch (e) { if (window.console) console.warn('views skipped', e); }
                fit(grid);
                setTimeout(function () { fit(grid); }, 300);
                setTimeout(function () { fit(grid); }, 900);
                var t;
                window.addEventListener('resize', function () { clearTimeout(t); t = setTimeout(function () { fit(grid); }, 120); });
            }
            return result;
        }
        return original.apply(grid, arguments);
    };
    Base.prototype.__crmHooked = true;
    ej.__crmGrid = true;
})();
