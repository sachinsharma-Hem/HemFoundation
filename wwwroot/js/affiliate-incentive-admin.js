/* Admin Affiliate Incentive Report (Views/Affiliate/AdminIncentiveReport.cshtml).
   Every interaction is one request to /api/affiliate-incentive/admin/report,
   which returns the summary cards and one page of rows. Amounts are computed
   on the server only. */
(function () {
    'use strict';

    var root = document.getElementById('afiAdmin');
    if (!root) return;

    var API = '/api/affiliate-incentive/admin';
    var state = { view: 'affiliates', page: 1, pageSize: 20, sort: '', dir: 'desc', requestId: 0 };

    var $ = function (id) { return document.getElementById(id); };
    var rupees = function (v) { return '₹' + (Number(v) || 0).toLocaleString('en-IN'); };
    var count = function (v) { return (Number(v) || 0).toLocaleString('en-IN'); };
    var esc = function (v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    };

    function authHeaders() {
        var token = localStorage.getItem('jwt_token') || sessionStorage.getItem('jwt_token') || '';
        return { 'Accept': 'application/json', 'Authorization': 'Bearer ' + token };
    }

    function filters() {
        return {
            view: state.view,
            search: $('afiSearch').value.trim(),
            year: $('afiYear').value,
            month: $('afiMonth').value,
            from: $('afiFrom').value,
            to: $('afiTo').value,
            status: $('afiStatus').value,
            sort: state.sort,
            dir: state.dir
        };
    }

    function query(extra) {
        var params = new URLSearchParams();
        var all = Object.assign({}, filters(), extra || {});
        Object.keys(all).forEach(function (key) {
            if (all[key] !== '' && all[key] != null) params.set(key, all[key]);
        });
        return params.toString();
    }

    function showError(message) {
        $('afiErrorText').textContent = message || '';
        $('afiError').hidden = !message;
    }

    // ------------------------------------------------------------------
    // Columns for the two views
    // ------------------------------------------------------------------
    var columns = {
        affiliates: [
            { key: 'name', label: 'Affiliate', sort: 'name' },
            { key: 'id', label: 'Affiliate ID', sort: 'id' },
            { key: 'current', label: 'Current month', sort: 'current', num: true },
            { key: 'previous', label: 'Previous month', sort: 'previous', num: true },
            { key: 'period', label: 'Selected period', sort: 'period', num: true, periodOnly: true },
            { key: 'lifetime', label: 'Lifetime', sort: 'lifetime', num: true },
            { key: 'months', label: 'Earning months', sort: 'months', num: true },
            { key: 'updated', label: 'Last updated' }
        ],
        monthly: [
            { key: 'name', label: 'Affiliate', sort: 'name' },
            { key: 'id', label: 'Affiliate ID', sort: 'id' },
            { key: 'month', label: 'Month', sort: 'month' },
            { key: 'year', label: 'Year' },
            { key: 'eligible', label: 'Eligible activity' },
            { key: 'incentive', label: 'Incentive', sort: 'incentive', num: true },
            { key: 'status', label: 'Status' },
            { key: 'updated', label: 'Last updated' }
        ]
    };

    function visibleColumns(periodFiltered) {
        return columns[state.view].filter(function (c) { return !c.periodOnly || periodFiltered; });
    }

    function renderHead(cols) {
        $('afiHead').innerHTML = '<tr>' + cols.map(function (c) {
            var sorted = c.sort && state.sort === c.sort;
            var icon = !c.sort ? '' : ' <i class="bi ' +
                (sorted ? (state.dir === 'asc' ? 'bi-caret-up-fill' : 'bi-caret-down-fill') : 'bi-chevron-expand') + '"></i>';

            return '<th' + (c.num ? ' class="is-num' + (sorted ? ' is-sorted' : '') + '"' : (sorted ? ' class="is-sorted"' : '')) +
                (c.sort ? ' data-sort="' + c.sort + '" tabindex="0" role="button"' : '') + '>' + esc(c.label) + icon + '</th>';
        }).join('') + '</tr>';
    }

    function cell(col, r) {
        var label = ' data-label="' + esc(col.label) + '"';

        switch (col.key) {
            case 'name':
                return '<td' + label + '><span class="afi-name">' + esc(r.name) + '</span>' +
                    (r.mobile ? '<div class="afi-sub">' + esc(r.mobile) + '</div>' : '') + '</td>';
            case 'id':
                return '<td' + label + '>' + esc(r.userId) + (r.code ? '<div class="afi-sub">' + esc(r.code) + '</div>' : '') + '</td>';
            case 'current': return '<td' + label + ' class="is-num"><span class="afi-money">' + rupees(r.currentMonthIncentive) + '</span></td>';
            case 'previous': return '<td' + label + ' class="is-num">' + rupees(r.previousMonthIncentive) + '</td>';
            case 'period': return '<td' + label + ' class="is-num"><span class="afi-money">' + rupees(r.periodIncentive) + '</span></td>';
            case 'lifetime': return '<td' + label + ' class="is-num"><span class="afi-money">' + rupees(r.lifetimeIncentive) + '</span></td>';
            case 'months': return '<td' + label + ' class="is-num">' + count(r.earningMonths) + '</td>';
            case 'month': return '<td' + label + '>' + esc(r.month) + '</td>';
            case 'year': return '<td' + label + '>' + esc(r.year) + '</td>';
            case 'eligible':
                return '<td' + label + ' class="is-block"><div class="afi-chips">' +
                    '<span class="afi-chip">Students <b>' + count(r.students) + '</b></span>' +
                    '<span class="afi-chip">Schools <b>' + count(r.schools) + '</b></span>' +
                    '<span class="afi-chip">Teachers <b>' + count(r.teachers) + '</b></span>' +
                    '<span class="afi-chip">Certificates <b>' + count(r.certificates) + '</b></span>' +
                    '<span class="afi-chip">Projects <b>' + count(r.projects) + '</b></span></div></td>';
            case 'incentive': return '<td' + label + ' class="is-num"><span class="afi-money">' + rupees(r.incentive) + '</span></td>';
            case 'status':
                return '<td' + label + '><span class="afi-badge ' +
                    (r.status === 'In progress' ? 'afi-badge-progress' : 'afi-badge-closed') + '">' + esc(r.status) + '</span></td>';
            case 'updated': return '<td' + label + '><span class="afi-sub">' + esc(r.lastUpdated || '—') + '</span></td>';
            default: return '<td></td>';
        }
    }

    function renderOverview(o) {
        $('afiCardLifetime').textContent = rupees(o.lifetimeIncentive);
        $('afiCardAffiliates').textContent = count(o.totalAffiliates);
        $('afiCardEarning').textContent = count(o.earningAffiliates) + ' have earned incentive';
        $('afiCardCurrent').textContent = rupees(o.currentMonthIncentive);
        $('afiCardCurrentLabel').textContent = o.currentMonthLabel + ' (in progress)';
        $('afiCardPrevious').textContent = rupees(o.previousMonthIncentive);
        $('afiCardPreviousLabel').textContent = o.previousMonthLabel;

        var e = o.eligible || {};
        $('afiCardEligible').innerHTML =
            '<span class="afi-chip">Students <b>' + count(e.students) + '</b></span>' +
            '<span class="afi-chip">Schools <b>' + count(e.schools) + '</b></span>' +
            '<span class="afi-chip">Teachers <b>' + count(e.teachers) + '</b></span>' +
            '<span class="afi-chip">Certificates <b>' + count(e.certificates) + '</b></span>' +
            '<span class="afi-chip">Projects <b>' + count(e.projects) + '</b></span>';

        // Fill the year filter once, keeping the current choice.
        var select = $('afiYear');
        if (select.options.length <= 1 && Array.isArray(o.years)) {
            var chosen = select.value;
            select.innerHTML = '<option value="">All years</option>' +
                o.years.map(function (y) { return '<option value="' + y + '">' + y + '</option>'; }).join('');
            select.value = chosen;
        }
    }

    function renderPager(data) {
        var pages = Math.max(1, Math.ceil(data.total / data.pageSize));
        var from = data.total === 0 ? 0 : (data.page - 1) * data.pageSize + 1;
        var to = Math.min(data.total, data.page * data.pageSize);

        $('afiPager').innerHTML =
            '<span>Showing ' + from + '–' + to + ' of ' + count(data.total) + '</span>' +
            '<div><select id="afiPageSize" class="afi-input" style="width:auto;height:32px" aria-label="Rows per page">' +
            [10, 20, 50, 100].map(function (n) {
                return '<option value="' + n + '"' + (n === data.pageSize ? ' selected' : '') + '>' + n + ' / page</option>';
            }).join('') + '</select>' +
            '<button type="button" class="afi-btn afi-btn-sm" data-page="' + (data.page - 1) + '"' + (data.page <= 1 ? ' disabled' : '') + '><i class="bi bi-chevron-left"></i> Prev</button>' +
            '<span>Page ' + data.page + ' of ' + pages + '</span>' +
            '<button type="button" class="afi-btn afi-btn-sm" data-page="' + (data.page + 1) + '"' + (data.page >= pages ? ' disabled' : '') + '>Next <i class="bi bi-chevron-right"></i></button></div>';
    }

    // ------------------------------------------------------------------
    // Load: one request per interaction; a newer request wins
    // ------------------------------------------------------------------
    function load(options) {
        options = options || {};
        var requestId = ++state.requestId;
        showError('');

        var refreshButton = $('afiRefresh');
        if (options.refresh) {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Refreshing…';
        }

        var url = API + '/report?' + query({ page: state.page, pageSize: state.pageSize, refresh: options.refresh ? 'true' : '' });

        return fetch(url, { headers: authHeaders(), credentials: 'same-origin', cache: 'no-store' })
            .then(function (response) {
                if (response.status === 401) { window.location.replace('/'); return null; }
                return response.json().catch(function () { return null; }).then(function (json) {
                    if (!response.ok || !json || json.success !== true) {
                        throw new Error((json && json.message) || 'The incentive report could not be loaded.');
                    }
                    return json;
                });
            })
            .then(function (data) {
                if (!data || requestId !== state.requestId) return;   // superseded

                renderOverview(data.overview);

                var cols = visibleColumns(data.periodFiltered);
                renderHead(cols);

                $('afiRows').innerHTML = data.items.length
                    ? data.items.map(function (r) {
                        return '<tr class="is-clickable" data-user="' + esc(r.userId) + '" tabindex="0">' +
                            cols.map(function (c) { return cell(c, r); }).join('') + '</tr>';
                    }).join('')
                    : '<tr><td colspan="' + cols.length + '"><div class="afi-empty"><i class="bi bi-inbox"></i>No records match these filters.</div></td></tr>';

                $('afiFilteredTotal').textContent =
                    count(data.total) + (state.view === 'monthly' ? ' month record' : ' affiliate') + (data.total === 1 ? '' : 's') +
                    ' · ' + (data.periodFiltered || state.view === 'monthly' ? 'Incentive in view: ' : 'Lifetime incentive: ') + rupees(data.filteredIncentive);

                $('afiAsOf').textContent = 'Current month: ' + data.overview.currentMonthLabel + '.';
                renderPager(data);
            })
            .catch(function (error) {
                if (requestId !== state.requestId) return;
                showError(error.message);
            })
            .then(function () {
                if (options.refresh) {
                    refreshButton.disabled = false;
                    refreshButton.innerHTML = '<i class="bi bi-arrow-repeat"></i> Refresh data';
                }
            });
    }

    function reload() { state.page = 1; load(); }

    // ------------------------------------------------------------------
    // Affiliate detail
    // ------------------------------------------------------------------
    function openDetail(userId) {
        var modalEl = $('afiDetailModal');
        $('afiDetailTitle').firstChild.textContent = 'Loading… ';
        $('afiDetailSub').textContent = '';
        $('afiDetailBody').innerHTML = '<div class="afi-empty"><span class="afi-skeleton" style="width:220px"></span></div>';
        bootstrap.Modal.getOrCreateInstance(modalEl).show();

        fetch(API + '/affiliates/' + encodeURIComponent(userId), { headers: authHeaders(), credentials: 'same-origin', cache: 'no-store' })
            .then(function (response) {
                return response.json().catch(function () { return null; }).then(function (json) {
                    if (!response.ok || !json || json.success !== true) {
                        throw new Error((json && json.message) || 'The affiliate details could not be loaded.');
                    }
                    return json;
                });
            })
            .then(function (d) {
                var a = d.affiliate, s = d.summary;

                $('afiDetailTitle').firstChild.textContent = (a.name || 'Affiliate ' + a.userId) + ' ';
                $('afiDetailSub').textContent = 'Affiliate ID ' + a.userId + (a.code ? ' · ' + a.code : '') +
                    (a.mobile ? ' · ' + a.mobile : '') + ' · Joined ' + a.joined;

                $('afiDetailBody').innerHTML =
                    '<div class="afi-mini">' +
                        '<div class="is-main"><span>Lifetime incentive</span><b>' + rupees(s.lifetimeIncentive) + '</b></div>' +
                        '<div><span>Current month (' + esc(s.currentMonthLabel) + ')</span><b>' + rupees(s.currentMonthIncentive) + '</b></div>' +
                        '<div><span>Previous month (' + esc(s.previousMonthLabel) + ')</span><b>' + rupees(s.previousMonthIncentive) + '</b></div>' +
                    '</div>' +
                    '<p class="afi-section-title">Month-wise history</p>' +
                    '<div class="afi-table-wrap"><table class="afi-table"><thead><tr>' +
                        '<th>Month</th><th class="is-num">Students</th><th class="is-num">Schools</th><th class="is-num">Teachers</th>' +
                        '<th class="is-num">Certificates</th><th class="is-num">Projects</th><th class="is-num">Incentive</th><th>Status</th>' +
                    '</tr></thead><tbody>' +
                    d.months.map(function (m) {
                        return '<tr>' +
                            '<td data-label="Month"><span class="afi-name">' + esc(m.label) + '</span></td>' +
                            '<td data-label="Students" class="is-num">' + count(m.activeStudents) + '<div class="afi-sub">' + rupees(m.studentIncentive) + '</div></td>' +
                            '<td data-label="Schools" class="is-num">' + count(m.activeSchools) + '<div class="afi-sub">' + rupees(m.schoolIncentive) + '</div></td>' +
                            '<td data-label="Teachers" class="is-num">' + count(m.activeTeachers) + '<div class="afi-sub">' + rupees(m.teacherIncentive) + '</div></td>' +
                            '<td data-label="Certificates" class="is-num">' + count(m.totalCertificates) + '<div class="afi-sub">' + rupees(m.certificateIncentive) + '</div></td>' +
                            '<td data-label="Projects" class="is-num">' + count(m.evaluatedProjects) + '<div class="afi-sub">' + rupees(m.projectIncentive) + '</div></td>' +
                            '<td data-label="Incentive" class="is-num"><span class="afi-money">' + rupees(m.totalIncentive) + '</span></td>' +
                            '<td data-label="Status"><span class="afi-badge ' + (m.isCurrent ? 'afi-badge-progress' : 'afi-badge-closed') + '">' + esc(m.status) + '</span></td></tr>';
                    }).join('') +
                    '</tbody></table></div>';
            })
            .catch(function (error) {
                $('afiDetailTitle').firstChild.textContent = 'Affiliate ';
                $('afiDetailBody').innerHTML = '<div class="afi-empty"><i class="bi bi-exclamation-circle"></i>' + esc(error.message) + '</div>';
            });
    }

    // ------------------------------------------------------------------
    // Export (the API needs the bearer token, so it is fetched, not linked)
    // ------------------------------------------------------------------
    function exportCsv() {
        var button = $('afiExport');
        button.disabled = true;

        fetch(API + '/export?' + query(), { headers: authHeaders(), credentials: 'same-origin' })
            .then(function (response) {
                if (!response.ok) throw new Error('The export could not be created.');
                var name = (response.headers.get('Content-Disposition') || '').match(/filename="?([^";]+)"?/);
                return response.blob().then(function (blob) {
                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = name ? name[1] : 'Affiliate-Incentive.csv';
                    document.body.appendChild(link);
                    link.click();
                    link.remove();
                    setTimeout(function () { URL.revokeObjectURL(link.href); }, 2000);
                });
            })
            .catch(function (error) { showError(error.message); })
            .then(function () { button.disabled = false; });
    }

    // ------------------------------------------------------------------
    // Wiring
    // ------------------------------------------------------------------
    var searchTimer;
    $('afiSearch').addEventListener('input', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(reload, 350);
    });

    ['afiYear', 'afiMonth', 'afiFrom', 'afiTo', 'afiStatus'].forEach(function (id) {
        $(id).addEventListener('change', reload);
    });

    $('afiReset').addEventListener('click', function () {
        ['afiSearch', 'afiYear', 'afiMonth', 'afiFrom', 'afiTo', 'afiStatus'].forEach(function (id) { $(id).value = ''; });
        state.sort = ''; state.dir = 'desc';
        reload();
    });

    root.querySelectorAll('.afi-tabs button').forEach(function (button) {
        button.addEventListener('click', function () {
            if (state.view === button.getAttribute('data-view')) return;
            root.querySelectorAll('.afi-tabs button').forEach(function (b) { b.classList.toggle('active', b === button); });
            state.view = button.getAttribute('data-view');
            state.sort = ''; state.dir = 'desc';
            reload();
        });
    });

    function sortBy(th) {
        var key = th.getAttribute('data-sort');
        if (state.sort === key) state.dir = state.dir === 'asc' ? 'desc' : 'asc';
        else { state.sort = key; state.dir = (key === 'name' || key === 'id') ? 'asc' : 'desc'; }
        reload();
    }

    $('afiHead').addEventListener('click', function (e) {
        var th = e.target.closest('th[data-sort]');
        if (th) sortBy(th);
    });
    $('afiHead').addEventListener('keydown', function (e) {
        var th = e.target.closest('th[data-sort]');
        if (th && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); sortBy(th); }
    });

    $('afiRows').addEventListener('click', function (e) {
        var row = e.target.closest('tr[data-user]');
        if (row) openDetail(row.getAttribute('data-user'));
    });
    $('afiRows').addEventListener('keydown', function (e) {
        var row = e.target.closest('tr[data-user]');
        if (row && e.key === 'Enter') openDetail(row.getAttribute('data-user'));
    });

    $('afiPager').addEventListener('click', function (e) {
        var button = e.target.closest('button[data-page]');
        if (!button || button.disabled) return;
        state.page = Number(button.getAttribute('data-page'));
        load();
    });
    $('afiPager').addEventListener('change', function (e) {
        if (e.target.id !== 'afiPageSize') return;
        state.pageSize = Number(e.target.value);
        reload();
    });

    $('afiRefresh').addEventListener('click', function () {
        var message = 'This recalculates every month for every affiliate from the latest activity. ' +
            'It can take a while. The report normally stays up to date by itself.';

        if (window.Swal) {
            Swal.fire({
                icon: 'question', title: 'Recalculate all incentives?', text: message,
                showCancelButton: true, confirmButtonText: 'Recalculate', confirmButtonColor: '#059669', reverseButtons: true
            }).then(function (result) { if (result.isConfirmed) load({ refresh: true }); });
        } else if (window.confirm(message)) {
            load({ refresh: true });
        }
    });
    $('afiRetry').addEventListener('click', function () { load(); });

    // ------------------------------------------------------------------
    // Incentive settings (school rate)
    // ------------------------------------------------------------------
    function settingsError(message) {
        $('afiSettingsError').textContent = message || '';
        $('afiSettingsError').hidden = !message;
    }

    $('afiSettings').addEventListener('click', function () {
        settingsError('');
        $('afiSchoolRate').value = '';
        bootstrap.Modal.getOrCreateInstance($('afiSettingsModal')).show();

        fetch(API + '/settings', { headers: authHeaders(), credentials: 'same-origin', cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (json) {
                if (!json || json.success !== true) throw new Error((json && json.message) || 'Settings could not be loaded.');
                $('afiSchoolRate').value = json.schoolRate;
                $('afiSchoolRate').focus();
            })
            .catch(function (e) { settingsError(e.message); });
    });

    $('afiSettingsForm').addEventListener('submit', function (e) {
        e.preventDefault();
        settingsError('');

        var value = Number($('afiSchoolRate').value);
        if ($('afiSchoolRate').value === '' || !Number.isFinite(value) || value < 0 || value > 100000) {
            settingsError('Enter a school rate between ₹0 and ₹1,00,000.');
            return;
        }

        var button = $('afiSettingsSave');
        button.disabled = true;

        fetch(API + '/settings', {
            method: 'PUT',
            headers: Object.assign({ 'Content-Type': 'application/json' }, authHeaders()),
            credentials: 'same-origin',
            body: JSON.stringify({ schoolRate: value })
        })
            .then(function (r) { return r.json().catch(function () { return null; }); })
            .then(function (json) {
                if (!json || json.success !== true) throw new Error((json && json.message) || 'Settings could not be saved.');
                bootstrap.Modal.getOrCreateInstance($('afiSettingsModal')).hide();
                if (window.Swal) Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: json.message, showConfirmButton: false, timer: 3500 });
                load();
            })
            .catch(function (e) { settingsError(e.message); })
            .then(function () { button.disabled = false; });
    });
    $('afiExport').addEventListener('click', exportCsv);

    load();
})();
