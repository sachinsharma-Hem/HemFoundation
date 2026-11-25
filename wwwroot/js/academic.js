/* Academics page (Views/Academic/Index.cshtml) – api/academic */
(function () {
    'use strict';

    var root = document.getElementById('acadRoot');
    if (!root) return;

    var inst = root.getAttribute('data-institute');            // only for an admin viewing a school
    var Q = inst ? '?instituteId=' + encodeURIComponent(inst) : '';
    var API = '/api/academic';
    var $ = function (id) { return document.getElementById(id); };
    var data = { overview: {}, exams: [], standards: [], subjects: [], terms: [] };
    var marks = { exam: null, students: [], dirty: false };

    // ------------------------------------------------------------------ helpers
    function esc(v) { return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function num(v, d) { return v == null || v === '' ? '–' : Number(v).toLocaleString('en-IN', { maximumFractionDigits: d == null ? 2 : d }); }
    function url(path) { return API + path + (path.indexOf('?') >= 0 ? (inst ? '&instituteId=' + encodeURIComponent(inst) : '') : Q); }

    function api(method, path, body) {
        var opts = { method: method, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } };
        if (body instanceof FormData) opts.body = body;
        else if (body !== undefined) { opts.headers['Content-Type'] = 'application/json'; opts.body = JSON.stringify(body); }
        return fetch(url(path), opts).then(function (r) {
            if (r.status === 401) { window.location.href = '/'; throw new Error('Session expired'); }
            return r.json().catch(function () { return {}; }).then(function (j) {
                if (!r.ok || j.success === false) { var e = new Error(j.message || 'Something went wrong. Please try again.'); e.errors = j.errors; throw e; }
                return j;
            });
        });
    }

    function toast(icon, text) {
        if (window.Swal) Swal.fire({ toast: true, position: 'top-end', icon: icon, title: text, showConfirmButton: false, timer: 3200 });
        else alert(text);
    }
    function confirmBox(title, text) {
        if (!window.Swal) return Promise.resolve(window.confirm(title));
        return Swal.fire({ title: title, text: text, icon: 'warning', showCancelButton: true, confirmButtonText: 'Yes', confirmButtonColor: '#4B2E83' }).then(function (r) { return r.isConfirmed; });
    }
    function busy(btn, on, label) {
        if (!btn) return;
        if (on) { btn.dataset.html = btn.innerHTML; btn.disabled = true; btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> ' + (label || 'Saving…'); }
        else { btn.disabled = false; if (btn.dataset.html) btn.innerHTML = btn.dataset.html; }
    }
    function examLabel(e) { return e.standard + ' · ' + e.subjectName + ' · ' + e.termName + ' (' + e.academicYear + ')'; }
    function pct(a, b) { return b ? Math.round(a * 100 / b) : 0; }
    function showErrors(box, err) {
        var list = err && err.errors;
        box.hidden = !list || !list.length;
        if (!box.hidden) box.innerHTML = '<b><i class="bi bi-exclamation-triangle"></i> ' + esc(err.message) + '</b><ul>' + list.slice(0, 200).map(function (x) { return '<li>' + esc(x) + '</li>'; }).join('') + '</ul>' + (list.length > 200 ? '<div>…and ' + (list.length - 200) + ' more</div>' : '');
        if (!box.hidden) box.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    // ------------------------------------------------------------------ tabs
    var tabs = root.querySelectorAll('.acad-tabs button');
    function showTab(name) {
        if (!root.querySelector('[data-panel="' + name + '"]')) name = 'overview';
        tabs.forEach(function (b) { var on = b.getAttribute('data-tab') === name; b.classList.toggle('active', on); b.setAttribute('aria-selected', on); });
        root.querySelectorAll('.acad-tab').forEach(function (p) { p.classList.toggle('active', p.getAttribute('data-panel') === name); });
        if (location.hash !== '#' + name) history.replaceState(null, '', '#' + name);
    }
    tabs.forEach(function (b) { b.addEventListener('click', function () { showTab(b.getAttribute('data-tab')); }); });
    root.addEventListener('click', function (e) {
        var g = e.target.closest('[data-go]'); if (!g) return;
        e.preventDefault(); showTab(g.getAttribute('data-go'));
        var exam = g.getAttribute('data-exam');
        if (exam) { $('marksExam').value = exam; loadMarks(); }
    });

    // ------------------------------------------------------------------ load everything
    function load() {
        return api('GET', '/overview').then(function (j) {
            data = { overview: j.overview, exams: j.exams, standards: j.standards, subjects: j.subjects, terms: j.terms };
            renderOverview(); renderSubjects(); renderTerms(); renderExamForm(); renderExams(); renderMarksPicker(); renderRcStandards();
        }).catch(function (e) { toast('error', e.message); });
    }

    // ------------------------------------------------------------------ overview
    function renderOverview() {
        var o = data.overview, k = function (key, v) { var el = root.querySelector('[data-k="' + key + '"]'); if (el) el.textContent = v; };
        k('students', num(o.students, 0)); k('subjects', num(o.subjects, 0)); k('terms', num(o.terms, 0)); k('exams', num(o.exams, 0));
        k('entered', num(o.marksEntered, 0) + (o.marksExpected ? ' of ' + num(o.marksExpected, 0) : ''));
        k('pass', o.passRate == null ? '–' : o.passRate + '%');
        var bar = root.querySelector('[data-k="enteredBar"]'); if (bar) bar.style.width = pct(o.marksEntered, o.marksExpected) + '%';

        var steps = [
            [o.subjects > 0, 'Add your subjects', 'subjects'],
            [o.terms > 0, 'Add exam terms, e.g. Half Yearly 2026-27', 'terms'],
            [o.exams > 0, 'Set max and passing marks for each standard', 'exams'],
            [o.marksEntered > 0, 'Enter marks online or upload Excel', 'marks'],
            [o.marksEntered > 0, 'Open and print report cards', 'report-cards']
        ];
        $('steps').innerHTML = steps.map(function (s, i) {
            return '<li class="' + (s[0] ? 'done' : '') + '"><span class="n">' + (s[0] ? '<i class="bi bi-check-lg"></i>' : i + 1) + '</span><span class="t">' + esc(s[1]) + '</span>' +
                '<a href="#' + s[2] + '" data-go="' + s[2] + '">' + (s[0] ? 'Open' : 'Start') + ' <i class="bi bi-arrow-right"></i></a></li>';
        }).join('');

        $('examCountNote').textContent = data.exams.length ? data.exams.length + ' exams' : '';
        $('overviewExams').innerHTML = data.exams.length ? data.exams.slice(0, 12).map(function (e) {
            var p = pct(e.entered, e.students), present = e.entered - e.absent;
            return '<tr><td><b>' + esc(e.standard) + ' · ' + esc(e.subjectName) + '</b><div class="acad-sub">' + esc(e.termName) + ' ' + esc(e.academicYear) + '</div></td>' +
                '<td style="min-width:150px"><div class="acad-bar small"><i style="width:' + p + '%" class="' + (p >= 100 ? 'ok' : '') + '"></i></div><div class="acad-sub">' + e.entered + ' of ' + e.students + '</div></td>' +
                '<td class="r">' + (e.average == null ? '–' : num(e.average) + ' / ' + num(e.maxMarks)) + '</td>' +
                '<td class="r">' + (present ? pct(e.passed, present) + '%' : '–') + '</td>' +
                '<td class="r"><a class="btn btn-sm btn-light rounded-pill" href="#marks" data-go="marks" data-exam="' + e.configId + '">Marks</a></td></tr>';
        }).join('') + (data.exams.length > 12 ? '<tr><td colspan="5" class="acad-sub"><a href="#exams" data-go="exams">See all ' + data.exams.length + ' exams</a></td></tr>' : '')
            : '<tr><td colspan="5" class="acad-empty">No exams yet. <a href="#exams" data-go="exams">Set up exams</a></td></tr>';
    }

    // ------------------------------------------------------------------ subjects
    function renderSubjects() {
        $('subjectCount').textContent = data.subjects.length ? data.subjects.length + ' subjects' : '';
        $('subjectList').innerHTML = data.subjects.length ? data.subjects.map(function (s, i) {
            return '<div class="acad-item"><span class="acad-avatar">' + esc((s.subjectName || '?').slice(0, 2).toUpperCase()) + '</span>' +
                '<div class="grow"><b>' + esc(s.subjectName) + '</b><div class="acad-sub">' + (s.subjectCode ? esc(s.subjectCode) + ' · ' : '') + s.examCount + ' exam' + (s.examCount === 1 ? '' : 's') + '</div></div>' +
                '<button type="button" class="btn btn-sm btn-light" data-sedit="' + i + '" aria-label="Edit"><i class="bi bi-pencil"></i></button>' +
                '<button type="button" class="btn btn-sm btn-light text-danger" data-sdel="' + i + '" aria-label="Remove"><i class="bi bi-trash"></i></button></div>';
        }).join('') : '<div class="acad-empty"><i class="bi bi-book"></i>No subjects yet. Add your first subject.</div>';
    }

    function resetSubjectForm() {
        $('subjectId').value = ''; $('subjectName').value = ''; $('subjectCode').value = '';
        $('subjectFormTitle').innerHTML = '<i class="bi bi-plus-circle"></i> Add a subject'; $('subjectCancel').hidden = true;
    }
    $('subjectForm').addEventListener('submit', function (e) {
        e.preventDefault();
        var btn = $('subjectSave'); busy(btn, true);
        api('POST', '/subjects', { id: Number($('subjectId').value) || null, name: $('subjectName').value, code: $('subjectCode').value })
            .then(function (j) { toast('success', j.message); resetSubjectForm(); return load(); })
            .catch(function (er) { toast('error', er.message); })
            .then(function () { busy(btn, false); });
    });
    $('subjectCancel').addEventListener('click', resetSubjectForm);
    $('subjectList').addEventListener('click', function (e) {
        var ed = e.target.closest('[data-sedit]'), del = e.target.closest('[data-sdel]');
        if (ed) {
            var s = data.subjects[Number(ed.getAttribute('data-sedit'))];
            $('subjectId').value = s.subjectId; $('subjectName').value = s.subjectName; $('subjectCode').value = s.subjectCode || '';
            $('subjectFormTitle').innerHTML = '<i class="bi bi-pencil"></i> Edit subject'; $('subjectCancel').hidden = false; $('subjectName').focus();
        }
        if (del) {
            var d = data.subjects[Number(del.getAttribute('data-sdel'))];
            confirmBox('Remove ' + d.subjectName + '?', 'It will not be offered for new exams. Exams and marks already entered stay.').then(function (ok) {
                if (!ok) return;
                api('DELETE', '/subjects/' + d.subjectId).then(function (j) { toast('success', j.message); load(); }).catch(function (er) { toast('error', er.message); });
            });
        }
    });

    // ------------------------------------------------------------------ terms
    function renderTerms() {
        if (!$('termYear').value) $('termYear').value = root.getAttribute('data-year') || '';
        $('termCount').textContent = data.terms.length ? data.terms.length + ' terms' : '';
        $('termList').innerHTML = data.terms.length ? data.terms.map(function (t, i) {
            return '<div class="acad-item"><span class="acad-avatar is-teal"><i class="bi bi-calendar-week"></i></span>' +
                '<div class="grow"><b>' + esc(t.termName) + '</b><div class="acad-sub">' + esc(t.academicYear) + ' · ' + t.examCount + ' exam' + (t.examCount === 1 ? '' : 's') + '</div></div>' +
                '<button type="button" class="btn btn-sm btn-light" data-tedit="' + i + '" aria-label="Edit"><i class="bi bi-pencil"></i></button>' +
                '<button type="button" class="btn btn-sm btn-light text-danger" data-tdel="' + i + '" aria-label="Remove"' + (t.examCount ? ' disabled title="Has exams"' : '') + '><i class="bi bi-trash"></i></button></div>';
        }).join('') : '<div class="acad-empty"><i class="bi bi-calendar-week"></i>No terms yet. Add e.g. “Half Yearly”.</div>';
    }
    function resetTermForm() {
        $('termId').value = ''; $('termName').value = ''; $('termYear').value = root.getAttribute('data-year') || '';
        $('termFormTitle').innerHTML = '<i class="bi bi-plus-circle"></i> Add a term'; $('termCancel').hidden = true;
    }
    $('termForm').addEventListener('submit', function (e) {
        e.preventDefault();
        var btn = $('termSave'); busy(btn, true);
        api('POST', '/terms', { id: Number($('termId').value) || null, name: $('termName').value, year: $('termYear').value })
            .then(function (j) { toast('success', j.message); resetTermForm(); return load(); })
            .catch(function (er) { toast('error', er.message); })
            .then(function () { busy(btn, false); });
    });
    $('termCancel').addEventListener('click', resetTermForm);
    $('termList').addEventListener('click', function (e) {
        var ed = e.target.closest('[data-tedit]'), del = e.target.closest('[data-tdel]');
        if (ed) {
            var t = data.terms[Number(ed.getAttribute('data-tedit'))];
            $('termId').value = t.termId; $('termName').value = t.termName; $('termYear').value = t.academicYear;
            $('termFormTitle').innerHTML = '<i class="bi bi-pencil"></i> Edit term'; $('termCancel').hidden = false; $('termName').focus();
        }
        if (del) {
            var d = data.terms[Number(del.getAttribute('data-tdel'))];
            confirmBox('Remove ' + d.termName + ' (' + d.academicYear + ')?', '').then(function (ok) {
                if (!ok) return;
                api('DELETE', '/terms/' + d.termId).then(function (j) { toast('success', j.message); load(); }).catch(function (er) { toast('error', er.message); });
            });
        }
    });

    // ------------------------------------------------------------------ exam setup
    function options(list, value, text, first) {
        return (first ? '<option value="">' + esc(first) + '</option>' : '') + list.map(function (x) { return '<option value="' + esc(value(x)) + '">' + esc(text(x)) + '</option>'; }).join('');
    }
    function renderExamForm() {
        var term = $('examTerm').value, std = $('examStandard').value;
        $('examTerm').innerHTML = data.terms.length ? options(data.terms, function (t) { return t.termId; }, function (t) { return t.termName + ' (' + t.academicYear + ')'; }, 'Choose term') : '<option value="">Add a term first</option>';
        $('examStandard').innerHTML = data.standards.length ? options(data.standards, function (s) { return s; }, function (s) { return s; }, 'Choose standard') : '<option value="">No students found in your school</option>';
        $('examTerm').value = term; $('examStandard').value = std;
        var chosen = Array.prototype.map.call(root.querySelectorAll('#examSubjects input:checked'), function (c) { return c.value; });
        $('examSubjects').innerHTML = data.subjects.length ? data.subjects.map(function (s) {
            return '<label class="acad-check"><input type="checkbox" value="' + s.subjectId + '"' + (chosen.indexOf(String(s.subjectId)) >= 0 ? ' checked' : '') + '><span>' + esc(s.subjectName) + '</span></label>';
        }).join('') : '<span class="acad-sub">Add subjects first in the <a href="#subjects" data-go="subjects">Subjects</a> tab.</span>';

        $('fTerm').innerHTML = options(data.terms, function (t) { return t.termId; }, function (t) { return t.termName + ' (' + t.academicYear + ')'; }, 'All terms');
        $('fStandard').innerHTML = options(data.standards, function (s) { return s; }, function (s) { return s; }, 'All standards');
    }
    $('examAllSubjects').addEventListener('click', function () {
        var boxes = root.querySelectorAll('#examSubjects input'), all = Array.prototype.every.call(boxes, function (b) { return b.checked; });
        boxes.forEach(function (b) { b.checked = !all; });
    });
    $('examForm').addEventListener('submit', function (e) {
        e.preventDefault();
        var subjects = Array.prototype.map.call(root.querySelectorAll('#examSubjects input:checked'), function (c) { return Number(c.value); });
        var max = Number($('examMax').value), pass = Number($('examPass').value), err = $('examError');
        var problem = !$('examTerm').value ? 'Choose the term.' : !$('examStandard').value ? 'Choose the standard.' : !subjects.length ? 'Choose at least one subject.'
            : !(max > 0) ? 'Enter max marks.' : !(pass >= 0) || pass > max ? 'Passing marks must be between 0 and the max marks.' : '';
        err.textContent = problem; err.hidden = !problem;
        if (problem) return;
        var btn = $('examSave'); busy(btn, true);
        api('POST', '/exams', { termId: Number($('examTerm').value), standard: $('examStandard').value, subjectIds: subjects, maxMarks: max, passingMarks: pass })
            .then(function (j) { toast('success', j.message); root.querySelectorAll('#examSubjects input').forEach(function (b) { b.checked = false; }); return load(); })
            .catch(function (er) { err.textContent = er.message; err.hidden = false; })
            .then(function () { busy(btn, false); });
    });

    function filteredExams() {
        var t = $('fTerm').value, s = $('fStandard').value, q = $('fSearch').value.trim().toLowerCase();
        return data.exams.filter(function (e) { return (!t || String(e.termId) === t) && (!s || e.standard === s) && (!q || e.subjectName.toLowerCase().indexOf(q) >= 0); });
    }
    function renderExams() {
        var list = filteredExams();
        $('examRows').innerHTML = list.length ? list.map(function (e) {
            var p = pct(e.entered, e.students);
            return '<tr><td data-label="Term"><b>' + esc(e.termName) + '</b><div class="acad-sub">' + esc(e.academicYear) + '</div></td>' +
                '<td data-label="Standard"><span class="acad-pill">' + esc(e.standard) + '</span></td><td data-label="Subject">' + esc(e.subjectName) + '</td>' +
                '<td data-label="Max" class="r"><span class="acad-badge is-purple">' + num(e.maxMarks) + '</span></td>' +
                '<td data-label="Pass" class="r"><span class="acad-badge is-amber">' + num(e.passingMarks) + '</span></td>' +
                '<td data-label="Marks entered" style="min-width:140px"><div class="acad-bar small"><i style="width:' + p + '%" class="' + (p >= 100 ? 'ok' : '') + '"></i></div><div class="acad-sub">' + e.entered + ' of ' + e.students + '</div></td>' +
                '<td class="r is-actions"><a class="btn btn-sm btn-light rounded-pill" href="#marks" data-go="marks" data-exam="' + e.configId + '"><i class="bi bi-pencil-square"></i> Marks</a> ' +
                '<button type="button" class="btn btn-sm btn-light text-danger" data-edel="' + e.configId + '" aria-label="Remove"' + (e.entered ? ' disabled title="Marks entered"' : '') + '><i class="bi bi-trash"></i></button></td></tr>';
        }).join('') : '<tr><td colspan="7" class="acad-empty">' + (data.exams.length ? 'No exams match the filters.' : 'No exams yet. Use the form above.') + '</td></tr>';
    }
    ['fTerm', 'fStandard'].forEach(function (id) { $(id).addEventListener('change', renderExams); });
    $('fSearch').addEventListener('input', renderExams);
    $('examRows').addEventListener('click', function (e) {
        var del = e.target.closest('[data-edel]'); if (!del) return;
        var ex = data.exams.filter(function (x) { return String(x.configId) === del.getAttribute('data-edel'); })[0];
        confirmBox('Remove this exam?', examLabel(ex)).then(function (ok) {
            if (!ok) return;
            api('DELETE', '/exams/' + ex.configId).then(function (j) { toast('success', j.message); load(); }).catch(function (er) { toast('error', er.message); });
        });
    });
    $('examExport').addEventListener('click', function () {
        var rows = [['Term', 'Academic year', 'Standard', 'Subject', 'Max marks', 'Passing marks', 'Students', 'Marks entered', 'Absent', 'Passed', 'Average']];
        filteredExams().forEach(function (e) { rows.push([e.termName, e.academicYear, e.standard, e.subjectName, e.maxMarks, e.passingMarks, e.students, e.entered, e.absent, e.passed, e.average == null ? '' : e.average]); });
        var csv = '﻿' + rows.map(function (r) { return r.map(function (v) { v = String(v == null ? '' : v); return /[",\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v; }).join(','); }).join('\r\n');
        var a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' })); a.download = 'Exams_' + new Date().toISOString().slice(0, 10) + '.csv';
        document.body.appendChild(a); a.click(); a.remove();
    });

    // ------------------------------------------------------------------ marks
    var mode = 'online';
    function renderMarksPicker() {
        var keep = $('marksExam').value;
        $('marksExam').innerHTML = data.exams.length ? '<option value="">Choose an exam</option>' + data.exams.map(function (e) {
            return '<option value="' + e.configId + '">' + esc(examLabel(e)) + ' – ' + e.entered + '/' + e.students + '</option>';
        }).join('') : '<option value="">No exams set up yet</option>';
        $('marksExam').value = keep;
        if (keep && !marks.dirty) loadMarks();
        if (!keep) showMarksPanels();
    }
    function showMarksPanels() {
        var has = !!marks.exam && String(marks.exam.configId) === $('marksExam').value;
        $('marksEmpty').hidden = has; $('examInfo').hidden = !has;
        $('onlinePanel').hidden = !has || mode !== 'online'; $('excelPanel').hidden = !has || mode !== 'excel';
    }
    root.querySelectorAll('.acad-mode button').forEach(function (b) {
        b.addEventListener('click', function () {
            mode = b.getAttribute('data-mode');
            root.querySelectorAll('.acad-mode button').forEach(function (x) { x.classList.toggle('active', x === b); });
            showMarksPanels();
        });
    });
    $('marksExam').addEventListener('change', function () {
        if (marks.dirty && !window.confirm('You have marks that are not saved. Switch the exam anyway?')) { $('marksExam').value = marks.exam ? marks.exam.configId : ''; return; }
        loadMarks();
    });

    function loadMarks() {
        var id = $('marksExam').value;
        marks = { exam: null, students: [], dirty: false };
        $('markErrors').hidden = true; $('xlsErrors').hidden = true;
        if (!id) { showMarksPanels(); return; }
        $('markRows').innerHTML = '<div class="acad-empty">Loading…</div>';
        api('GET', '/exams/' + id + '/marks').then(function (j) {
            marks.exam = j.exam; marks.students = j.students;
            var e = j.exam;
            $('examInfo').innerHTML = '<span><i class="bi bi-mortarboard"></i> ' + esc(e.standard) + '</span><span><i class="bi bi-book"></i> ' + esc(e.subjectName) + '</span>' +
                '<span><i class="bi bi-calendar-week"></i> ' + esc(e.termName) + ' ' + esc(e.academicYear) + '</span><span class="acad-badge is-purple">Max ' + num(e.maxMarks) + '</span>' +
                '<span class="acad-badge is-amber">Pass ' + num(e.passingMarks) + '</span><span class="acad-sub">' + e.entered + ' of ' + e.students + ' entered</span>';
            renderMarkRows(); showMarksPanels();
        }).catch(function (er) { toast('error', er.message); });
    }

    function markState(s) {
        if (s.isAbsent) return 'absent';
        if (s.marksObtained == null || s.marksObtained === '') return 'empty';
        var m = Number(s.marksObtained);
        if (isNaN(m) || m < 0 || m > marks.exam.maxMarks || Math.abs(Math.round(m * 100) - m * 100) > 1e-6) return 'bad';
        return m >= marks.exam.passingMarks ? 'pass' : 'fail';
    }
    function renderMarkRows() {
        var q = $('markSearch').value.trim().toLowerCase();
        var list = marks.students.map(function (s, i) { return { s: s, i: i }; }).filter(function (x) {
            return !q || (x.s.name || '').toLowerCase().indexOf(q) >= 0 || String(x.s.rollNo || '').toLowerCase().indexOf(q) >= 0;
        });
        $('markRows').innerHTML = marks.students.length ? '<div class="acad-mark head"><span>Roll</span><span>Student</span><span>Marks / ' + num(marks.exam.maxMarks) + '</span><span>Absent</span><span>Remarks</span></div>' + list.map(function (x) {
            var s = x.s, st = markState(s);
            return '<div class="acad-mark is-' + st + '" data-row="' + x.i + '">' +
                '<span class="roll">' + esc(s.rollNo || '—') + '</span><span class="name"><b>' + esc(s.name || 'Student ' + s.studentId) + '</b>' + (s.division ? '<small> · ' + esc(s.division) + '</small>' : '') + '</span>' +
                '<span class="mk"><input type="number" inputmode="decimal" min="0" max="' + marks.exam.maxMarks + '" step="0.25" aria-label="Marks of ' + esc(s.name) + '" value="' + (s.isAbsent || s.marksObtained == null ? '' : s.marksObtained) + '"' + (s.isAbsent ? ' disabled' : '') + ' data-f="marks">' +
                '<i class="st"></i></span>' +
                '<span class="ab"><label class="acad-switch"><input type="checkbox" data-f="absent"' + (s.isAbsent ? ' checked' : '') + ' aria-label="Absent"><i></i></label></span>' +
                '<span class="rm"><input type="text" maxlength="200" placeholder="Remarks" value="' + esc(s.remarks || '') + '" data-f="remarks" aria-label="Remarks for ' + esc(s.name) + '"></span></div>';
        }).join('') : '<div class="acad-empty">No students of ' + esc(marks.exam.standard) + ' were found in your school.</div>';
        updateMarkStatus();
    }
    function updateMarkStatus() {
        var c = { filled: 0, bad: 0, absent: 0 };
        marks.students.forEach(function (s) { var st = markState(s); if (st === 'bad') c.bad++; else if (st === 'absent') c.absent++; else if (st !== 'empty') c.filled++; });
        var total = marks.students.length, missing = total - c.filled - c.absent - c.bad;
        $('markStatus').innerHTML = c.filled + ' entered · ' + c.absent + ' absent' + (missing ? ' · <b>' + missing + ' empty</b>' : '') + (c.bad ? ' · <b class="text-danger">' + c.bad + ' out of range</b>' : '') + (marks.dirty ? ' · <span class="text-warning">not saved</span>' : '');
        $('markSave').disabled = !marks.dirty || c.bad > 0;
    }
    $('markRows').addEventListener('input', function (e) {
        var row = e.target.closest('[data-row]'); if (!row) return;
        var s = marks.students[Number(row.getAttribute('data-row'))], f = e.target.getAttribute('data-f');
        if (f === 'marks') s.marksObtained = e.target.value === '' ? null : e.target.value;
        if (f === 'remarks') s.remarks = e.target.value;
        if (f === 'absent') { s.isAbsent = e.target.checked; var mi = row.querySelector('[data-f="marks"]'); mi.disabled = s.isAbsent; if (s.isAbsent) mi.value = ''; }
        marks.dirty = true;
        row.className = 'acad-mark is-' + markState(s);
        updateMarkStatus();
    });
    $('markRows').addEventListener('change', function (e) { if (e.target.getAttribute('data-f') === 'absent') e.target.dispatchEvent(new Event('input', { bubbles: true })); });
    $('markRows').addEventListener('keydown', function (e) {
        // Enter moves to the next student's marks, like a spreadsheet.
        if (e.key !== 'Enter' || e.target.getAttribute('data-f') !== 'marks') return;
        e.preventDefault();
        var inputs = Array.prototype.slice.call($('markRows').querySelectorAll('[data-f="marks"]:not([disabled])'));
        var next = inputs[inputs.indexOf(e.target) + 1]; if (next) next.focus();
    });
    $('markSearch').addEventListener('input', renderMarkRows);
    $('markSave').addEventListener('click', function () {
        var rows = marks.students.filter(function (s) { return markState(s) !== 'empty'; }).map(function (s) {
            return { studentId: s.studentId, marksObtained: s.isAbsent || s.marksObtained == null ? null : Number(s.marksObtained), isAbsent: !!s.isAbsent, remarks: s.remarks || '' };
        });
        var btn = this; busy(btn, true);
        api('POST', '/exams/' + marks.exam.configId + '/marks', rows).then(function (j) {
            toast('success', j.message); $('markErrors').hidden = true; marks.dirty = false;
            return load().then(loadMarks);
        }).catch(function (er) { showErrors($('markErrors'), er); if (!er.errors) toast('error', er.message); })
          .then(function () { busy(btn, false); updateMarkStatus(); });
    });
    window.addEventListener('beforeunload', function (e) { if (marks.dirty) { e.preventDefault(); e.returnValue = ''; } });

    // Excel
    $('tplDownload').addEventListener('click', function () {
        if (!marks.exam) return;
        window.location.href = url('/exams/' + marks.exam.configId + '/template');
    });
    $('xlsFile').addEventListener('change', function () {
        var f = this.files && this.files[0];
        $('xlsName').textContent = f ? f.name : 'Choose the .xlsx file';
        $('xlsUpload').disabled = !f || !/\.xlsx$/i.test(f.name);
        if (f && !/\.xlsx$/i.test(f.name)) toast('error', 'Choose the .xlsx template file.');
    });
    $('xlsUpload').addEventListener('click', function () {
        var f = $('xlsFile').files[0]; if (!f || !marks.exam) return;
        var fd = new FormData(); fd.append('file', f, f.name);
        var btn = this; busy(btn, true, 'Uploading…'); $('xlsErrors').hidden = true;
        api('POST', '/exams/' + marks.exam.configId + '/upload', fd).then(function (j) {
            toast('success', j.message); $('xlsFile').value = ''; $('xlsName').textContent = 'Choose the .xlsx file';
            if (window.Swal) Swal.fire({ icon: 'success', title: 'Marks uploaded', text: j.message, confirmButtonColor: '#4B2E83' });
            return load().then(loadMarks);
        }).catch(function (er) { showErrors($('xlsErrors'), er); if (!er.errors) toast('error', er.message); })
          .then(function () { busy(btn, false); $('xlsUpload').disabled = !$('xlsFile').files.length; });
    });

    // ------------------------------------------------------------------ report cards
    var rcStudents = [];
    function renderRcStandards() {
        var keep = $('rcStandard').value;
        $('rcStandard').innerHTML = data.standards.length ? options(data.standards, function (s) { return s; }, function (s) { return s; }, 'Choose standard') : '<option value="">No students found</option>';
        $('rcStandard').value = keep;
    }
    function renderRc() {
        var q = $('rcSearch').value.trim().toLowerCase();
        var list = rcStudents.filter(function (s) { return !q || (s.name || '').toLowerCase().indexOf(q) >= 0 || String(s.rollNo || '').toLowerCase() === q; });
        $('rcList').innerHTML = list.length ? list.map(function (s) {
            return '<a class="acad-student" href="/Academic/Scorecard/' + s.studentId + Q + '" target="_blank" rel="noopener">' +
                '<span class="acad-avatar">' + esc((s.name || '?').split(/\s+/).map(function (w) { return w[0]; }).slice(0, 2).join('').toUpperCase()) + '</span>' +
                '<span class="grow"><b>' + esc(s.name || 'Student ' + s.studentId) + '</b><small>Roll ' + esc(s.rollNo || '—') + (s.division ? ' · Div ' + esc(s.division) : '') + '</small></span>' +
                '<span class="go"><i class="bi bi-file-earmark-text"></i> Report card</span></a>';
        }).join('') : '<div class="acad-empty">' + ($('rcStandard').value ? 'No students match.' : 'Choose a standard.') + '</div>';
    }
    $('rcStandard').addEventListener('change', function () {
        var std = this.value; rcStudents = [];
        if (!std) { renderRc(); return; }
        $('rcList').innerHTML = '<div class="acad-empty">Loading…</div>';
        api('GET', '/students?standard=' + encodeURIComponent(std)).then(function (j) { rcStudents = j.students; renderRc(); }).catch(function (er) { toast('error', er.message); });
    });
    $('rcSearch').addEventListener('input', renderRc);

    // ------------------------------------------------------------------ start
    showTab((location.hash || '#overview').slice(1));
    window.addEventListener('hashchange', function () { showTab((location.hash || '#overview').slice(1)); });
    load();
})();
