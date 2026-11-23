/* Evaluation overview widget (Views/Shared/Partials/_EvaluationOverview.cshtml). */
(function () {
    'use strict';

    var root = document.getElementById('evoRoot');
    if (!root) return;

    var charts = {};
    var lastFilter = {};
    var pendingLink = root.getAttribute('data-pending-link') === '1';
    var showMine = root.getAttribute('data-show-mine') === '1';

    var $ = function (id) { return document.getElementById(id); };
    var num = function (v) { return (Number(v) || 0).toLocaleString('en-IN'); };
    var esc = function (v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    };

    function ensureChartJs() {
        if (window.Chart) return Promise.resolve();
        return new Promise(function (resolve) {
            var s = document.createElement('script');
            s.src = 'https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js';
            s.onload = resolve;
            s.onerror = resolve;   // cards and tables still work without charts
            document.head.appendChild(s);
        });
    }

    function draw(id, config) {
        if (!window.Chart) return;
        var canvas = $(id);
        if (!canvas) return;
        if (charts[id]) charts[id].destroy();
        charts[id] = new Chart(canvas, config);
    }

    function render(d) {
        var t = d.totals;
        $('evoPeriod').textContent = '(' + d.period + ')';
        $('evoSubmitted').textContent = num(t.submitted);
        $('evoEvaluated').textContent = num(t.evaluated);
        $('evoPending').textContent = num(t.pending);
        $('evoLast7').textContent = num(t.last7);
        $('evoToday').textContent = 'Today: ' + num(t.today);

        if (showMine) {
            $('evoMine').hidden = false;
            $('evoMyToday').textContent = num(d.mine.today);
            $('evoMy7').textContent = num(d.mine.last7);
            $('evoMyTotal').textContent = num(d.mine.total);
        }

        $('evoTypes').innerHTML = d.types.map(function (x) {
            var pct = x.submitted ? Math.round(x.evaluated * 100 / x.submitted) : 0;
            var action = x.pending > 0
                ? (pendingLink
                    ? '<a class="evo-btn is-sm" href="/ProjectEvaluation/index?type=' + encodeURIComponent(x.key) + '&status=pending">View pending</a>'
                    : '<button type="button" class="evo-btn is-sm" data-evo-pending="' + esc(x.key) + '">View pending</button>')
                : '<span class="evo-pct">All done</span>';

            return '<tr>' +
                '<td data-label="Type"><b>' + esc(x.label) + '</b></td>' +
                '<td data-label="Submitted" class="n">' + num(x.submitted) + '</td>' +
                '<td data-label="Evaluated" class="n">' + num(x.evaluated) + '</td>' +
                '<td data-label="Pending" class="n is-pending">' + num(x.pending) + '</td>' +
                '<td data-label="Progress"><div class="evo-bar"><span style="width:' + pct + '%"></span></div><span class="evo-pct">' + pct + '% evaluated</span></td>' +
                '<td data-label="Last 7 days" class="n">' + num(x.last7) + '</td>' +
                '<td data-label="" class="n">' + action + '</td></tr>';
        }).join('');

        $('evoEvaluators').innerHTML = d.evaluators.length
            ? d.evaluators.map(function (e) {
                return '<tr><td data-label="Name">' + esc(e.name) + '</td><td data-label="7 days" class="n">' + num(e.last7) +
                    '</td><td data-label="30 days" class="n">' + num(e.last30) + '</td><td data-label="Total" class="n">' + num(e.total) + '</td></tr>';
            }).join('')
            : '<tr><td colspan="4" class="evo-empty">No evaluations yet.</td></tr>';

        ensureChartJs().then(function () {
            var common = { responsive: true, maintainAspectRatio: false };

            draw('evoChartStatus', {
                type: 'bar',
                data: {
                    labels: d.types.map(function (x) { return x.label; }),
                    datasets: [
                        { label: 'Evaluated', data: d.types.map(function (x) { return x.evaluated; }), backgroundColor: '#10b981', borderRadius: 6 },
                        { label: 'Pending', data: d.types.map(function (x) { return x.pending; }), backgroundColor: '#f59e0b', borderRadius: 6 }
                    ]
                },
                options: Object.assign({}, common, {
                    plugins: { legend: { position: 'bottom' } },
                    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } } }
                })
            });

            var c = t.colors;
            var colorValues = [c.green, c.orange, c.blue, c.yellow, c.red];
            var anyColor = colorValues.some(function (v) { return v > 0; });
            draw('evoChartColors', {
                type: 'doughnut',
                data: {
                    labels: anyColor ? ['Green', 'Orange', 'Blue', 'Yellow', 'Red'] : ['No evaluations yet'],
                    datasets: [{
                        data: anyColor ? colorValues : [1],
                        backgroundColor: anyColor ? ['#10b981', '#f59e0b', '#3b82f6', '#eab308', '#ef4444'] : ['#e5e7eb'],
                        borderWidth: 2
                    }]
                },
                options: Object.assign({}, common, { plugins: { legend: { position: 'bottom' }, tooltip: { enabled: anyColor } } })
            });

            draw('evoChartTrend', {
                type: 'line',
                data: {
                    labels: d.trend.map(function (x) { return x.date; }),
                    datasets: [{
                        label: 'Evaluations', data: d.trend.map(function (x) { return x.count; }),
                        borderColor: '#5850ec', backgroundColor: 'rgba(88, 80, 236, .12)', fill: true, tension: .3, pointRadius: 3
                    }]
                },
                options: Object.assign({}, common, {
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
                })
            });
        });
    }

    function load(filter) {
        lastFilter = filter || lastFilter || {};
        var q = (lastFilter.month && lastFilter.year) ? '?month=' + lastFilter.month + '&year=' + lastFilter.year : '';

        $('evoTypes').innerHTML = '<tr><td colspan="7" class="evo-empty">Loading…</td></tr>';

        fetch('/api/project-evaluations/summary' + q, { credentials: 'same-origin', cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d || !d.success) throw new Error((d && d.message) || 'Could not load the evaluation summary.');
                render(d);
            })
            .catch(function (e) {
                $('evoTypes').innerHTML = '<tr><td colspan="7" class="evo-empty">' + esc(e.message) + '</td></tr>';
            });
    }

    $('evoRefresh').addEventListener('click', function () { load(); });

    root.addEventListener('click', function (e) {
        var b = e.target.closest('[data-evo-pending]');
        if (b && typeof window.evoShowPending === 'function') window.evoShowPending(b.getAttribute('data-evo-pending'));
    });

    window.EvaluationOverview = { load: load };
})();
