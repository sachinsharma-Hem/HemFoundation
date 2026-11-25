/* Parent dashboard (Views/Parent/Index.cshtml) */
(function () {
    'use strict';
    var root = document.getElementById('pdRoot');
    if (!root || !document.getElementById('pdKids')) return;

    var API = '/api/parent';
    var parentQ = root.getAttribute('data-parent') ? 'parentId=' + encodeURIComponent(root.getAttribute('data-parent')) : '';
    var state = { children: [], current: null, data: null, filter: 'all', cache: {} };
    var $ = function (id) { return document.getElementById(id); };

    // ------------------------------------------------------------ helpers
    function esc(v) { return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function num(v, d) { return Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: d == null ? 1 : d }); }
    function url(path, extra) {
        var q = [parentQ, extra || ''].filter(Boolean).join('&');
        return API + path + (q ? '?' + q : '');
    }
    function api(method, path, extra) {
        return fetch(url(path, extra), { method: method, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) {
                if (r.status === 401) { window.location.href = '/'; throw new Error('Session expired'); }
                return r.json().catch(function () { return {}; }).then(function (j) {
                    if (!r.ok || j.success === false) throw new Error(j.message || 'Something went wrong. Please try again.');
                    return j;
                });
            });
    }
    function toast(icon, text) {
        if (window.Swal) Swal.fire({ toast: true, position: 'top-end', icon: icon, title: text, showConfirmButton: false, timer: 3200 });
        else alert(text);
    }
    function initials(name) {
        var p = String(name || '?').trim().split(/\s+/);
        return ((p[0] || '?')[0] + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase();
    }
    function avatar(c, cls) {
        var hue = (Number(c.studentId) * 47) % 360;
        if (c.photoUrl) return '<span class="pd-avatar ' + (cls || '') + '"><img src="' + esc(c.photoUrl) + '" alt="" loading="lazy" onerror="this.remove()"><em>' + esc(initials(c.name)) + '</em></span>';
        return '<span class="pd-avatar ' + (cls || '') + '" style="--h:' + hue + '"><em>' + esc(initials(c.name)) + '</em></span>';
    }
    function cls(c) { return [c.standard ? 'Std ' + c.standard : '', c.division ? 'Div ' + c.division : ''].filter(Boolean).join(' · '); }
    function when(d) {
        if (!d) return '';
        var t = new Date(d); if (isNaN(t)) return '';
        var days = Math.floor((Date.now() - t.getTime()) / 864e5);
        if (days <= 0) return 'Today';
        if (days === 1) return 'Yesterday';
        if (days < 30) return days + ' days ago';
        return t.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
    }
    function date(d) { var t = d ? new Date(d) : null; return t && !isNaN(t) ? t.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }) : ''; }

    // ------------------------------------------------------------ children
    function loadChildren(selectId) {
        return api('GET', '/children').then(function (j) {
            state.children = j.children || [];
            renderKids();
            if (!state.children.length) { $('pdEmpty').classList.remove('d-none'); $('pdBoard').classList.add('d-none'); return; }
            $('pdEmpty').classList.add('d-none');
            var remembered = null;
            try { remembered = Number(localStorage.getItem('pd-child')); } catch (e) { }
            var pick = state.children.find(function (c) { return c.studentId === (selectId || remembered); }) || state.children[0];
            select(pick.studentId);
        }).catch(function (e) {
            $('pdKids').innerHTML = '<div class="pd-error"><i class="bi bi-exclamation-triangle"></i> ' + esc(e.message) + '</div>';
        });
    }

    function renderKids() {
        var html = state.children.map(function (c) {
            return '<button type="button" class="pd-kid' + (state.current === c.studentId ? ' active' : '') + '" data-id="' + c.studentId + '">' +
                avatar(c) +
                '<span class="pd-kid-text"><b>' + esc(c.name || 'Student') + '</b><small>' + esc(cls(c) || '—') + '</small>' +
                (c.school ? '<small class="school">' + esc(c.school) + '</small>' : '') + '</span>' +
                '<span class="pd-kid-stats"><span><b>' + num(c.points, 0) + '</b> pts</span><span><b>' + c.certificates + '</b> <i class="bi bi-patch-check"></i></span></span>' +
                '</button>';
        }).join('');
        if (state.children.length) html += '<button type="button" class="pd-kid is-add" id="pdKidAdd"><i class="bi bi-plus-lg"></i><span>Add child</span></button>';
        $('pdKids').innerHTML = html;
        var add = $('pdKidAdd'); if (add) add.addEventListener('click', openFind);
    }

    $('pdKids').addEventListener('click', function (e) {
        var b = e.target.closest('.pd-kid[data-id]');
        if (b) select(Number(b.getAttribute('data-id')));
    });

    function select(id, fresh) {
        state.current = id;
        try { localStorage.setItem('pd-child', id); } catch (e) { }
        Array.prototype.forEach.call(document.querySelectorAll('.pd-kid[data-id]'), function (b) { b.classList.toggle('active', Number(b.getAttribute('data-id')) === id); });
        var board = $('pdBoard');
        board.classList.remove('d-none');
        if (!fresh && state.cache[id]) { render(state.cache[id]); return; }
        board.classList.add('is-loading');
        api('GET', '/children/' + id + '/dashboard', fresh ? 'fresh=true' : '').then(function (j) {
            state.cache[id] = j;
            if (state.current === id) render(j);
        }).catch(function (e) { toast('error', e.message); })
            .finally(function () { board.classList.remove('is-loading'); });
    }

    // ------------------------------------------------------------ dashboard
    function render(j) {
        state.data = j;
        var c = j.child, s = j.summary;
        $('pdChildAvatar').outerHTML = avatar(c, 'lg').replace('class="pd-avatar', 'id="pdChildAvatar" class="pd-avatar');
        $('pdChildName').textContent = c.name || 'Student';
        $('pdChildMeta').innerHTML = [
            cls(c) ? '<span><i class="bi bi-mortarboard"></i> ' + esc(cls(c)) + '</span>' : '',
            c.rollNo ? '<span><i class="bi bi-hash"></i> Roll ' + esc(c.rollNo) + '</span>' : '',
            c.school ? '<span><i class="bi bi-building"></i> ' + esc(c.school) + '</span>' : '',
            c.username ? '<span><i class="bi bi-person-badge"></i> ' + esc(c.username) + '</span>' : ''
        ].join('');
        $('pdReportCard').href = '/Academic/Scorecard/' + c.studentId;

        var pct = s.totalCourses ? Math.round(s.coursesDone * 100 / s.totalCourses) : 0;
        $('pdRing').style.setProperty('--p', pct);
        $('pdRing').style.setProperty('--q', s.totalCourses ? Math.round((s.coursesDone + s.coursesInProgress) * 100 / s.totalCourses) : 0);
        $('pdRingPct').textContent = pct + '%';
        $('kDone').textContent = s.coursesDone; $('kProg').textContent = s.coursesInProgress; $('kNot').textContent = s.coursesNotStarted; $('kTotal').textContent = s.totalCourses;

        $('kMarks').textContent = num(s.marksGained); $('kMax').textContent = num(s.totalMarks, 0);
        $('kMarksBar').style.width = (s.totalMarks ? Math.min(100, s.marksGained * 100 / s.totalMarks) : 0) + '%';
        $('kRank').textContent = s.classRank ? '#' + s.classRank : '–';
        $('kRankSub').textContent = s.classRank ? 'of ' + s.classSize + ' active students' : 'no points yet';
        $('kCert').textContent = s.certificates;
        var last = (j.recent && j.recent[0]) ? j.recent[0].date : null;
        $('kLast').textContent = last ? when(last) : 'Not yet';
        $('kLastSub').textContent = last ? date(last) : 'no activity recorded';

        $('aMcq').textContent = s.mcq; $('aPrac').textContent = s.practical; $('aFam').textContent = s.familyProjects; $('aGrp').textContent = s.groupProjects; $('aSur').textContent = s.surveys;

        renderCourses();
        renderRecent(j.recent || []);
    }

    var statusCls = { 'Completed': 'is-done', 'In progress': 'is-prog', 'Not started': 'is-not' };
    function renderCourses() {
        var list = (state.data.courses || []).filter(function (x) { return state.filter === 'all' || x.status === state.filter; });
        if (!list.length) {
            $('pdCourses').innerHTML = '<div class="pd-none"><i class="bi bi-journal"></i> ' + (state.data.courses.length ? 'No courses in this list.' : 'No value courses found for this class yet.') + '</div>';
            return;
        }
        $('pdCourses').innerHTML = list.map(function (x) {
            var p = x.maxMarks ? Math.min(100, x.marks * 100 / x.maxMarks) : 0;
            function chip(icon, label, v) { return '<span class="pd-c-chip' + (v ? ' on' : '') + '" title="' + label + '"><i class="bi ' + icon + '"></i> ' + v + '</span>'; }
            return '<article class="pd-course">' +
                '<div class="pd-course-top"><h5>' + esc(x.courseName) + '</h5><span class="pd-status ' + statusCls[x.status] + '">' + esc(x.status) + '</span></div>' +
                '<div class="pd-course-marks"><div class="pd-bar"><i style="width:' + p + '%"></i></div><b>' + num(x.marks) + '<em>/' + num(x.maxMarks, 0) + '</em></b></div>' +
                '<div class="pd-course-chips">' +
                chip('bi-ui-checks', 'MCQ tests', x.mcq) + chip('bi-tools', 'Practicals', x.practical) +
                chip('bi-house-heart', 'Family projects', x.familyProject) + chip('bi-people', 'Group projects', x.groupProject) +
                chip('bi-clipboard-data', 'Surveys', x.survey) +
                (x.certified ? '<span class="pd-c-chip cert"><i class="bi bi-patch-check-fill"></i> Certified</span>' : '') +
                '</div>' +
                (x.lastActivity ? '<small class="pd-course-last">Last activity ' + esc(when(x.lastActivity)) + '</small>' : '') +
                '</article>';
        }).join('');
    }

    document.querySelector('.pd-filter').addEventListener('click', function (e) {
        var b = e.target.closest('button[data-f]'); if (!b) return;
        state.filter = b.getAttribute('data-f');
        Array.prototype.forEach.call(this.querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); });
        if (state.data) renderCourses();
    });

    var typeIcon = { 'MCQ': 'bi-ui-checks', 'Pratical': 'bi-tools', 'Practical': 'bi-tools', 'Survey': 'bi-clipboard-data', 'Group PBL': 'bi-people', 'Individual PBL': 'bi-house-heart', 'Family PBL': 'bi-house-heart', 'Quiz': 'bi-question-circle', 'HEM Project': 'bi-lightbulb' };
    function renderRecent(list) {
        if (!list.length) { $('pdRecent').innerHTML = '<li class="pd-none"><i class="bi bi-hourglass"></i> No activity yet.</li>'; return; }
        $('pdRecent').innerHTML = list.map(function (r) {
            var type = r.type === 'Pratical' ? 'Practical' : r.type;
            var pts = r.points && /^\s*-?\d+(\.\d+)?\s*$/.test(r.points) ? '<span class="pts">+' + esc(r.points.trim()) + '</span>' : '';
            return '<li><span class="pd-tl-ic"><i class="bi ' + (typeIcon[r.type] || 'bi-check2') + '"></i></span><div><b>' + esc(type || 'Activity') + '</b> ' + pts +
                '<small>' + esc([r.courseName, r.topic].filter(Boolean).join(' · ') || '—') + '</small><time>' + esc(when(r.date)) + '</time></div></li>';
        }).join('');
    }

    $('pdRefresh').addEventListener('click', function () { if (state.current) select(state.current, true); });

    $('pdExport').addEventListener('click', function () {
        var d = state.data; if (!d) return;
        var rows = [['Course', 'Status', 'Marks', 'Out of', 'MCQ tests', 'Practicals', 'Family projects', 'Group projects', 'Surveys', 'Certified']];
        d.courses.forEach(function (x) { rows.push([x.courseName, x.status, x.marks, x.maxMarks, x.mcq, x.practical, x.familyProject, x.groupProject, x.survey, x.certified ? 'Yes' : 'No']); });
        var csv = '﻿' + rows.map(function (r) { return r.map(function (v) { v = String(v == null ? '' : v); return /[",\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v; }).join(','); }).join('\r\n');
        var a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
        a.download = 'Progress_' + String(d.child.name || 'student').replace(/[^A-Za-z0-9]+/g, '_') + '.csv';
        document.body.appendChild(a); a.click(); a.remove();
    });

    // ------------------------------------------------------------ add child
    var dialog = $('pdDialog');
    function openFind() {
        $('pdFound').innerHTML = '<div class="pd-none"><span class="spinner-border spinner-border-sm"></span> Looking for your children…</div>';
        if (dialog.showModal) dialog.showModal(); else dialog.setAttribute('open', '');
        api('GET', '/children/find').then(function (j) {
            $('pdDialogSub').textContent = j.hasMobile ? 'Students registered with your mobile number ' + j.mobile + '.' : 'Your account has no mobile number.';
            if (!j.children.length) {
                $('pdFound').innerHTML = '<div class="pd-none big"><i class="bi bi-search"></i><b>No new children found</b>' +
                    '<span>' + (j.hasMobile ? 'Only students registered with your mobile number can be added. If your child used another number, ask the school to update it.' : 'Add your mobile number to your profile first.') + '</span></div>';
                return;
            }
            $('pdFound').innerHTML = j.children.map(function (c) {
                return '<div class="pd-found-row">' + avatar(c) + '<div class="grow"><b>' + esc(c.name || 'Student') + '</b><small>' + esc([cls(c), c.school].filter(Boolean).join(' · ') || '—') + '</small>' +
                    (c.username ? '<small>Login: ' + esc(c.username) + '</small>' : '') + '</div>' +
                    '<button type="button" class="pd-btn sm" data-map="' + c.studentId + '"><i class="bi bi-plus-lg"></i> Add</button></div>';
            }).join('');
        }).catch(function (e) { $('pdFound').innerHTML = '<div class="pd-error">' + esc(e.message) + '</div>'; });
    }
    $('pdFound').addEventListener('click', function (e) {
        var b = e.target.closest('[data-map]'); if (!b) return;
        var id = Number(b.getAttribute('data-map'));
        b.disabled = true; b.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
        api('POST', '/children/' + id).then(function (j) {
            toast('success', j.message);
            var row = b.closest('.pd-found-row'); row.classList.add('done');
            b.outerHTML = '<span class="pd-added"><i class="bi bi-check2-circle"></i> Added</span>';
            loadChildren(id);
        }).catch(function (err) { toast('error', err.message); b.disabled = false; b.innerHTML = '<i class="bi bi-plus-lg"></i> Add'; });
    });
    $('pdAddChild').addEventListener('click', openFind);
    $('pdEmptyAdd').addEventListener('click', openFind);
    dialog.addEventListener('click', function (e) { if (e.target === dialog) dialog.close(); });

    // greeting from the login session
    try {
        var u = JSON.parse(localStorage.getItem('user_session') || 'null');
        var first = u && (u.firstName || u.FirstName || (u.name || u.fullName || '').split(' ')[0]);
        if (first && !parentQ) $('pdGreeting').textContent = 'Hello, ' + first + '!';
    } catch (e) { }

    loadChildren();
})();
