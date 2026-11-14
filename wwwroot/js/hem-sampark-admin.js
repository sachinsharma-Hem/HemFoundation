/* HEM Sampark admin panel (Views/HemSampark/Admin.cshtml).
   Uses jQuery, Bootstrap 5 and SweetAlert2, all already loaded by _AdminLayout. */
(function ($) {
    'use strict';

    var API = '/api/hem-sampark/admin';

    // SweetAlert2 comes from a CDN. If it cannot be loaded, fall back to the
    // browser's own dialogs so every action keeps working.
    var Swal = window.Swal || {
        fire: function (o) {
            o = o || {};
            if (o.toast || o.didOpen) return Promise.resolve({ isConfirmed: false });
            var text = [o.title, o.text || String(o.html || '').replace(/<br\s*\/?>/gi, '\n').replace(/<[^>]+>/g, '')]
                .filter(Boolean).join('\n\n');
            if (o.showCancelButton) return Promise.resolve({ isConfirmed: window.confirm(text) });
            window.alert(text);
            return Promise.resolve({ isConfirmed: true });
        },
        close: function () { },
        showLoading: function () { }
    };
    var state = {
        pubPage: 1, pubPageSize: 10,
        repPage: 1, repPageSize: 20,
        editing: null,        // publication detail currently in the editor
        cleanForm: '',        // serialized form at load time, for unsaved-change detection
        preview: null,        // publication detail currently in the preview
        previewVersion: 'draft',
        loaded: {}
    };

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    function esc(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function api(method, url, body) {
        var options = {
            url: url.charAt(0) === '/' ? url : API + '/' + url,
            type: method,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        };

        if (body instanceof FormData) {
            options.data = body;
            options.processData = false;
            options.contentType = false;
        } else if (body !== undefined) {
            options.data = JSON.stringify(body);
            options.contentType = 'application/json';
        }

        return $.ajax(options).then(null, function (xhr) {
            if (xhr.status === 401) {
                window.location.replace('/');
                return $.Deferred().reject('Your session has expired.');
            }

            var message = xhr.responseJSON && xhr.responseJSON.message;
            if (!message) {
                message = xhr.status === 0
                    ? 'Could not reach the server. Check your connection and try again.'
                    : 'Something went wrong (error ' + xhr.status + '). Please try again.';
            }
            return $.Deferred().reject(message);
        });
    }

    function toast(icon, message) {
        Swal.fire({
            toast: true, position: 'top-end', icon: icon, title: message,
            showConfirmButton: false, timer: icon === 'error' ? 6000 : 3500, timerProgressBar: true
        });
    }

    function showError(message) {
        Swal.fire({ icon: 'error', title: 'Could not complete', text: String(message), confirmButtonColor: '#5b3fd6' });
    }

    function confirmAction(title, text, confirmText, icon) {
        return Swal.fire({
            icon: icon || 'question', title: title, text: text,
            showCancelButton: true, confirmButtonText: confirmText,
            confirmButtonColor: '#5b3fd6', reverseButtons: true
        }).then(function (result) { return result.isConfirmed; });
    }

    function busy($button, on) {
        if (on) {
            $button.data('html', $button.html()).prop('disabled', true)
                .html('<span class="hsa-spin"></span> Please wait…');
        } else {
            $button.prop('disabled', false).html($button.data('html'));
        }
    }

    function debounce(fn, wait) {
        var timer;
        return function () {
            clearTimeout(timer);
            timer = setTimeout(fn, wait);
        };
    }

    function badges(p) {
        var html;
        if (p.status === 'Published' && p.isScheduled) {
            html = '<span class="hsa-badge hsa-badge-Scheduled"><i class="bi bi-clock"></i> Scheduled</span>';
        } else {
            html = '<span class="hsa-badge hsa-badge-' + esc(p.status) + '">' + esc(p.status) + '</span>';
        }
        if (p.isToday) html += ' <span class="hsa-badge hsa-badge-today">Today</span>';
        if (p.hasUnpublishedChanges) {
            html += ' <span class="hsa-badge hsa-badge-warn" title="Users still see the previously published version"><i class="bi bi-exclamation-triangle"></i> Unpublished changes</span>';
        } else if (p.pdfOutdated) {
            html += ' <span class="hsa-badge hsa-badge-warn" title="Content changed after the PDF was generated"><i class="bi bi-exclamation-triangle"></i> PDF outdated</span>';
        }
        return html;
    }

    function pager($el, data, onPage) {
        var pages = Math.max(1, Math.ceil(data.total / data.pageSize));
        var from = data.total === 0 ? 0 : (data.page - 1) * data.pageSize + 1;
        var to = Math.min(data.total, data.page * data.pageSize);

        $el.html(
            '<span>Showing ' + from + '–' + to + ' of ' + data.total + '</span>' +
            '<div class="hsa-pager-btns">' +
            '<button type="button" class="btn hsa-btn hsa-btn-light" data-page="' + (data.page - 1) + '"' + (data.page <= 1 ? ' disabled' : '') + '><i class="bi bi-chevron-left"></i> Previous</button>' +
            '<span class="align-self-center px-1">Page ' + data.page + ' of ' + pages + '</span>' +
            '<button type="button" class="btn hsa-btn hsa-btn-light" data-page="' + (data.page + 1) + '"' + (data.page >= pages ? ' disabled' : '') + '>Next <i class="bi bi-chevron-right"></i></button>' +
            '</div>');

        $el.find('button').on('click', function () { onPage(Number($(this).data('page'))); });
    }

    // ------------------------------------------------------------------
    // Tabs
    // ------------------------------------------------------------------
    function showTab(name) {
        $('.hsa-tab').removeClass('active').filter('[data-hsa-tab="' + name + '"]').addClass('active');
        $('.hsa-panel').removeClass('active').filter('[data-hsa-panel="' + name + '"]').addClass('active');

        if (name === 'dashboard') loadStats();
        if (name === 'publications') loadPublications();
        if (name === 'master' && !state.loaded.master) loadMaster();
        if (name === 'reports') loadReports();
    }

    // ------------------------------------------------------------------
    // Dashboard
    // ------------------------------------------------------------------
    function statCard(tone, icon, value, label, filter) {
        var tag = filter !== undefined ? 'button type="button" data-filter="' + filter + '"' : 'div';
        return '<' + tag + ' class="hsa-stat">' +
            '<span class="hsa-stat-icon hsa-tone-' + tone + '"><i class="bi ' + icon + '"></i></span>' +
            '<span><span class="hsa-stat-value d-block">' + esc(value) + '</span>' +
            '<span class="hsa-stat-label">' + esc(label) + '</span></span></' + tag.split(' ')[0] + '>';
    }

    function loadStats() {
        api('GET', 'stats').then(function (s) {
            $('#hsaBusinessDate').text('Today is ' + s.businessDate + '.');

            $('#hsaStats').html(
                statCard('purple', 'bi-file-earmark-pdf', s.total, 'Total publications', '') +
                statCard('slate', 'bi-pencil-square', s.draft + s.generated, 'Drafts (' + s.generated + ' with PDF)', 'Draft') +
                statCard('green', 'bi-check2-circle', s.published, 'Published', 'Published') +
                statCard('blue', 'bi-calendar-event', s.scheduled, 'Scheduled / upcoming', 'Scheduled') +
                statCard('amber', 'bi-question-circle', s.totalQuestions, 'Quiz questions') +
                statCard('pink', 'bi-clipboard-check', s.attempts, 'Quiz attempts') +
                statCard('teal', 'bi-people', s.participants, 'Participating users') +
                statCard('red', 'bi-graph-up-arrow', s.averagePercent + '%', 'Average quiz score'));

            $('#hsaStats button').on('click', function () {
                var filter = $(this).data('filter');
                // "Drafts" covers Draft + Generated, so show all active and let the badges tell.
                $('#hsaStatus').val(filter === 'Draft' ? '' : filter);
                state.pubPage = 1;
                showTab('publications');
            });

            var t = s.today;
            if (!t) {
                $('#hsaToday').removeClass('hsa-loading').html(
                    '<div class="hsa-today"><div><div class="hsa-today-title">No publication for today yet</div>' +
                    '<div class="hsa-today-sub">Users currently see "Today\'s publication is not available yet."</div></div>' +
                    '<button type="button" class="btn hsa-btn hsa-btn-primary" id="hsaTodayCreate"><i class="bi bi-plus-lg"></i> Create today\'s publication</button></div>');
                $('#hsaTodayCreate').on('click', function () { openEditor(0, true); });
                return;
            }

            var live = t.status === 'Published';
            $('#hsaToday').removeClass('hsa-loading').html(
                '<div class="hsa-today"><div>' +
                '<div class="hsa-today-title">' + esc(t.title) + '</div>' +
                '<div class="mt-1">' + badges({ status: t.status, hasUnpublishedChanges: t.hasUnpublishedChanges }) + '</div>' +
                '<div class="hsa-today-sub mt-1">' + (live
                    ? 'Live for users · ' + t.attempts + ' quiz attempt(s) so far'
                    : 'Not visible to users until it is published') + '</div></div>' +
                '<div class="d-flex gap-2 flex-wrap">' +
                '<button type="button" class="btn hsa-btn hsa-btn-light" data-act="preview"><i class="bi bi-eye"></i> Preview</button>' +
                '<button type="button" class="btn hsa-btn hsa-btn-primary" data-act="edit"><i class="bi bi-pencil"></i> Edit</button></div></div>');

            $('#hsaToday [data-act="preview"]').on('click', function () { openPreview(t.id); });
            $('#hsaToday [data-act="edit"]').on('click', function () { openEditor(t.id); });
        }, function (message) {
            $('#hsaToday').removeClass('hsa-loading').html('<div class="hsa-sub">' + esc(message) + '</div>');
        });
    }

    // ------------------------------------------------------------------
    // Publications list
    // ------------------------------------------------------------------
    function loadPublications() {
        var query = $.param({
            status: $('#hsaStatus').val(), from: $('#hsaFrom').val(), to: $('#hsaTo').val(),
            search: $('#hsaSearch').val(), page: state.pubPage, pageSize: state.pubPageSize
        });

        $('#hsaPubRows').html('<tr><td colspan="7" class="hsa-loading"><span class="hsa-spin"></span></td></tr>');

        api('GET', 'publications?' + query).then(function (data) {
            if (!data.items.length) {
                $('#hsaPubRows').html('<tr><td colspan="7"><div class="hsa-empty"><i class="bi bi-inbox"></i>No publications match these filters.</div></td></tr>');
                $('#hsaPubPager').empty();
                return;
            }

            $('#hsaPubRows').html(data.items.map(function (p) {
                var archived = p.status === 'Archived';
                var published = p.status === 'Published';
                var canPublish = !archived && p.hasPdf && !p.pdfOutdated && (!published || p.hasUnpublishedChanges);

                var menu = '';
                if (!archived) {
                    menu += '<li><button class="dropdown-item" data-act="generate"><i class="bi bi-arrow-repeat"></i> ' + (p.hasPdf ? 'Regenerate PDF' : 'Generate PDF') + '</button></li>';
                    if (p.hasPdf) menu += '<li><button class="dropdown-item" data-act="download"><i class="bi bi-download"></i> Download PDF</button></li>';
                    if (published) menu += '<li><button class="dropdown-item" data-act="unpublish"><i class="bi bi-eye-slash"></i> Unpublish</button></li>';
                    menu += '<li><hr class="dropdown-divider"></li><li><button class="dropdown-item text-danger" data-act="archive"><i class="bi bi-archive"></i> Archive</button></li>';
                } else {
                    menu += '<li><button class="dropdown-item" data-act="restore"><i class="bi bi-arrow-counterclockwise"></i> Restore</button></li>';
                }

                return '<tr data-id="' + p.id + '" data-title="' + esc(p.title) + '">' +
                    '<td><div class="hsa-title-cell">' + esc(p.title) + '</div><div class="hsa-sub">Issue ' + esc(p.issueNumber) + '</div></td>' +
                    '<td class="text-nowrap">' + esc(p.entryDateDisplay) + '<div class="hsa-sub">' + esc(p.dayHindi || '') + '</div></td>' +
                    '<td>' + badges(p) + '</td>' +
                    '<td class="text-nowrap">' + (p.pdfGeneratedAt ? esc(p.pdfGeneratedAt) : '<span class="hsa-sub">Not generated</span>') + '</td>' +
                    '<td class="text-nowrap">' + (p.publishedAt && published ? esc(p.publishedAt) : '<span class="hsa-sub">—</span>') + '</td>' +
                    '<td class="text-center text-nowrap">' + p.questions + ' Q<div class="hsa-sub">' + p.attempts + ' attempt(s)</div></td>' +
                    '<td><div class="hsa-actions">' +
                    (p.hasPdf ? '<button type="button" class="hsa-icon-btn" data-act="preview" title="Preview PDF" aria-label="Preview PDF"><i class="bi bi-eye"></i></button>' : '') +
                    (!archived ? '<button type="button" class="hsa-icon-btn" data-act="edit" title="Edit" aria-label="Edit"><i class="bi bi-pencil"></i></button>' : '') +
                    (canPublish ? '<button type="button" class="hsa-icon-btn is-publish" data-act="publish" title="' + (published ? 'Republish' : 'Publish') + '" aria-label="Publish"><i class="bi bi-send-check"></i></button>' : '') +
                    '<div class="dropdown"><button type="button" class="hsa-icon-btn" data-bs-toggle="dropdown" data-bs-boundary="viewport" aria-expanded="false" aria-label="More actions"><i class="bi bi-three-dots-vertical"></i></button>' +
                    '<ul class="dropdown-menu dropdown-menu-end">' + menu + '</ul></div>' +
                    '</div></td></tr>';
            }).join(''));

            pager($('#hsaPubPager'), data, function (page) { state.pubPage = page; loadPublications(); });
        }, function (message) {
            $('#hsaPubRows').html('<tr><td colspan="7"><div class="hsa-empty">' + esc(message) + '</div></td></tr>');
        });
    }

    function refreshLists() {
        if ($('[data-hsa-panel="publications"]').hasClass('active')) loadPublications();
        if ($('[data-hsa-panel="dashboard"]').hasClass('active')) loadStats();
    }

    // Workflow actions shared by the list, the editor and the preview.
    function workflow(action, id, title) {
        var confirms = {
            publish: ['Publish this publication?', 'Users will see this PDF and quiz on its publication date. If it is already published, the live version is replaced.', 'Publish'],
            unpublish: ['Unpublish?', 'Users will no longer see this publication. Quiz results already recorded are kept.', 'Unpublish', 'warning'],
            archive: ['Archive "' + title + '"?', 'It is removed from users and from the active list. You can restore it later.', 'Archive', 'warning'],
            restore: ['Restore this publication?', 'It comes back as a draft and must be published again.', 'Restore']
        };

        var ask = confirms[action]
            ? confirmAction.apply(null, confirms[action])
            : $.Deferred().resolve(true).promise();

        return $.when(ask).then(function (yes) {
            if (!yes) return $.Deferred().reject();

            if (action === 'generate') {
                Swal.fire({ title: 'Generating PDF…', text: 'This usually takes a few seconds.', allowOutsideClick: false, didOpen: function () { Swal.showLoading(); } });
            }

            return api('POST', 'publications/' + id + '/' + action).then(function (res) {
                Swal.close();
                toast('success', res.message);
                refreshLists();
                return res.publication;
            }, function (message) {
                Swal.close();
                showError(message);
                return $.Deferred().reject();
            });
        });
    }

    $(document).on('click', '#hsaPubRows [data-act]', function () {
        var $row = $(this).closest('tr');
        var id = Number($row.data('id'));
        var action = $(this).data('act');

        if (action === 'preview') return openPreview(id);
        if (action === 'edit') return openEditor(id);
        if (action === 'download') {
            window.location.href = API + '/publications/' + id + '/pdf?version=draft&download=true';
            return;
        }

        workflow(action, id, $row.data('title')).then(function (publication) {
            if (action === 'generate') openPreview(publication.id);
        });
    });

    // ------------------------------------------------------------------
    // Editor
    // ------------------------------------------------------------------
    var editorModal, previewModal;

    function questionCard(q) {
        q = q || { id: 0, isActive: true, correctOption: 1 };
        var uid = 'q' + Math.random().toString(36).slice(2, 9);
        var letters = ['A', 'B', 'C', 'D'];

        var options = letters.map(function (letter, i) {
            var n = i + 1;
            return '<div class="hsa-opt">' +
                '<input type="radio" name="' + uid + '" value="' + n + '"' + (Number(q.correctOption) === n ? ' checked' : '') + ' title="Mark option ' + letter + ' as correct" aria-label="Option ' + letter + ' is correct">' +
                '<span class="hsa-opt-letter">' + letter + '</span>' +
                '<input type="text" class="form-control form-control-sm" data-f="option' + n + '" placeholder="Option ' + letter + (n > 2 ? ' (optional)' : '') + '" value="' + esc(q['option' + n]) + '">' +
                '</div>';
        }).join('');

        return '<div class="hsa-q' + (q.isActive === false ? ' is-off' : '') + '" data-qid="' + (q.id || 0) + '">' +
            '<div class="hsa-q-head">' +
            '<span class="hsa-q-num"></span>' +
            '<div class="form-check form-switch"><input class="form-check-input" type="checkbox" data-f="isActive" id="' + uid + 'a"' + (q.isActive === false ? '' : ' checked') + '>' +
            '<label class="form-check-label mb-0" for="' + uid + 'a">Enabled</label></div>' +
            '<button type="button" class="hsa-icon-btn" data-q="up" title="Move up" aria-label="Move up"><i class="bi bi-arrow-up"></i></button>' +
            '<button type="button" class="hsa-icon-btn" data-q="down" title="Move down" aria-label="Move down"><i class="bi bi-arrow-down"></i></button>' +
            '<button type="button" class="hsa-icon-btn text-danger" data-q="remove" title="Delete question" aria-label="Delete question"><i class="bi bi-trash"></i></button>' +
            '</div>' +
            '<input type="text" class="form-control" data-f="questionText" placeholder="Question text" value="' + esc(q.questionText) + '">' +
            '<div class="hsa-opts">' + options + '</div>' +
            '<input type="text" class="form-control form-control-sm" data-f="explanation" placeholder="Explanation shown to users after they submit (optional)" value="' + esc(q.explanation) + '">' +
            '</div>';
    }

    function renumberQuestions() {
        var $cards = $('#hsaQuestions .hsa-q');
        $cards.each(function (i) { $(this).find('.hsa-q-num').text(i + 1); });
        $('#hsaQCount').text('(' + $cards.length + ')');
    }

    function collectForm() {
        return {
            id: state.editing ? state.editing.id : 0,
            title: $('#hsaTitle').val(),
            entryDate: $('#hsaDate').val(),
            issueNumber: parseInt($('#hsaIssue').val(), 10) || 0,
            dayHindi: $('#hsaDayHi').val(),
            vikramSamvat: $('#hsaSamvat').val(),
            celebrationHeadingEn: $('#hsaHeadEn').val(),
            celebrationHeadingHi: $('#hsaHeadHi').val(),
            celebrationContentEn: $('#hsaContentEn').val(),
            celebrationContentHi: $('#hsaContentHi').val(),
            morningMantraEn: $('#hsaMantraEn').val(),
            morningMantraHi: $('#hsaMantraHi').val(),
            valuesInFocusEn: $('#hsaValuesEn').val(),
            valuesInFocusHi: $('#hsaValuesHi').val(),
            questions: $('#hsaQuestions .hsa-q').map(function () {
                var $q = $(this);

                // A card that was added but left completely empty is ignored.
                var blank = !Number($q.data('qid')) && !$q.find('input[type=text]').filter(function () {
                    return $.trim(this.value);
                }).length;
                if (blank) return null;

                return {
                    id: Number($q.data('qid')) || 0,
                    questionText: $q.find('[data-f="questionText"]').val(),
                    option1: $q.find('[data-f="option1"]').val(),
                    option2: $q.find('[data-f="option2"]').val(),
                    option3: $q.find('[data-f="option3"]').val(),
                    option4: $q.find('[data-f="option4"]').val(),
                    correctOption: Number($q.find('input[type=radio]:checked').val()) || 0,
                    explanation: $q.find('[data-f="explanation"]').val(),
                    isActive: $q.find('[data-f="isActive"]').is(':checked')
                };
            }).get()
        };
    }

    function isDirty() {
        return JSON.stringify(collectForm()) !== state.cleanForm;
    }

    function stateBanner(p, target) {
        if (!p || !p.id) {
            $(target).html('<div class="hsa-alert hsa-alert-info"><i class="bi bi-info-circle"></i><div>New publications are saved as a <b>draft</b>. Users cannot see them until you publish.</div></div>');
            return;
        }

        var html = '<div class="hsa-state">' + badges(p) +
            '<span><i class="bi bi-file-earmark-pdf"></i> ' + (p.pdfGeneratedAt ? 'PDF generated ' + esc(p.pdfGeneratedAt) : 'PDF not generated yet') + '</span>' +
            (p.status === 'Published' && p.publishedAt ? '<span><i class="bi bi-send-check"></i> Published ' + esc(p.publishedAt) + '</span>' : '') +
            (p.attempts ? '<span><i class="bi bi-people"></i> ' + p.attempts + ' quiz attempt(s)</span>' : '') +
            '</div>';

        if (p.pdfOutdated) {
            html += '<div class="hsa-alert hsa-alert-warn"><i class="bi bi-exclamation-triangle"></i><div><b>The PDF is outdated.</b> The content or master settings changed after it was generated. Regenerate the PDF, review it' +
                (p.status === 'Published' ? ', then republish. Users still see the previously published version.' : ', then publish.') + '</div></div>';
        } else if (p.hasUnpublishedChanges) {
            html += '<div class="hsa-alert hsa-alert-warn"><i class="bi bi-exclamation-triangle"></i><div><b>A newer PDF is waiting.</b> Users still see the previously published version. Review the new PDF and click Republish to make it live.</div></div>';
        }

        if (p.status === 'Published' && p.attempts > 0) {
            html += '<div class="hsa-alert hsa-alert-info"><i class="bi bi-info-circle"></i><div>' + p.attempts + ' user(s) already took this quiz. Their saved scores do not change if you edit the questions.</div></div>';
        }

        $(target).html(html);
    }

    function updateEditorButtons() {
        var p = state.editing;
        var published = p && p.status === 'Published';

        $('#hsaPreviewFromEditor').prop('disabled', !(p && p.hasPdf));
        $('#hsaSaveGenerate').html('<i class="bi bi-file-earmark-pdf"></i> ' + (p && p.hasPdf ? 'Save &amp; Regenerate PDF' : 'Save &amp; Generate PDF'));
        $('#hsaPublishFromEditor')
            .html('<i class="bi bi-send-check"></i> ' + (published ? 'Republish' : 'Publish'))
            .prop('disabled', !(p && p.hasPdf) || (published && !p.hasUnpublishedChanges));
    }

    function fillEditor(p) {
        state.editing = p && p.id ? p : null;

        $('#hsaEditorTitle').text(p && p.id ? 'Edit Publication' : 'New Publication');
        $('#hsaTitle').val(p.title || '');
        $('#hsaDate').val(p.entryDate || '').prop('disabled', p.status === 'Published');
        $('#hsaIssue').val(p.issueNumber != null ? p.issueNumber : '');
        $('#hsaDayHi').val(p.dayHindi || '');
        $('#hsaSamvat').val(p.vikramSamvat || '');
        $('#hsaHeadEn').val(p.celebrationHeadingEn || '');
        $('#hsaHeadHi').val(p.celebrationHeadingHi || '');
        $('#hsaContentEn').val(p.celebrationContentEn || '');
        $('#hsaContentHi').val(p.celebrationContentHi || '');
        $('#hsaMantraEn').val(p.morningMantraEn || '');
        $('#hsaMantraHi').val(p.morningMantraHi || '');
        $('#hsaValuesEn').val(p.valuesInFocusEn || '');
        $('#hsaValuesHi').val(p.valuesInFocusHi || '');

        $('#hsaQuestions').html((p.questions || []).map(questionCard).join(''));
        renumberQuestions();

        stateBanner(p, '#hsaEditorState');
        updateEditorButtons();
        state.cleanForm = JSON.stringify(collectForm());
    }

    function showSubTab(name) {
        $('.hsa-subtab').removeClass('active').filter('[data-hsa-sub="' + name + '"]').addClass('active');
        $('.hsa-subpanel').removeClass('active').filter('[data-hsa-subpanel="' + name + '"]').addClass('active');
    }

    function openEditor(id, forToday) {
        showSubTab('content');

        var load = id
            ? api('GET', 'publications/' + id).then(function (res) { return res.publication; })
            : api('GET', 'publications/defaults').then(function (d) {
                return {
                    id: 0, entryDate: forToday ? d.today : d.entryDate, issueNumber: d.issueNumber, vikramSamvat: d.vikramSamvat,
                    dayHindi: '', questions: [{ id: 0, isActive: true, correctOption: 1 }]
                };
            });

        load.then(function (p) {
            fillEditor(p);
            if (previewModal) previewModal.hide();
            editorModal.show();
        }, showError);
    }

    function validateForm(form) {
        if (!form.entryDate) return ['content', '#hsaDate', 'Please choose the publication date.'];
        if (!$.trim(form.celebrationHeadingEn)) return ['content', '#hsaHeadEn', 'The English heading is required.'];
        if (!$.trim(form.celebrationContentEn)) return ['content', '#hsaContentEn', 'The English content is required.'];

        for (var i = 0; i < form.questions.length; i++) {
            var q = form.questions[i];
            var label = 'Question ' + (i + 1) + ': ';
            var options = [q.option1, q.option2, q.option3, q.option4].map(function (o) { return $.trim(o); });
            var filled = 0;
            while (filled < 4 && options[filled]) filled++;

            if (!$.trim(q.questionText)) return ['quiz', null, label + 'enter the question text.'];
            if (filled < 2) return ['quiz', null, label + 'enter at least Option A and Option B.'];
            if (options.slice(filled).some(Boolean)) return ['quiz', null, label + 'fill the options in order without leaving a gap.'];
            if (q.correctOption < 1 || q.correctOption > filled) return ['quiz', null, label + 'select the correct answer from the options you entered.'];
        }
        return null;
    }

    function saveEditor(generatePdf, $button) {
        var form = collectForm();
        var problem = validateForm(form);

        if (problem) {
            showSubTab(problem[0]);
            if (problem[1]) $(problem[1]).trigger('focus');
            toast('warning', problem[2]);
            return $.Deferred().reject().promise();
        }

        form.generatePdf = generatePdf;
        busy($button, true);

        return api('POST', 'publications', form).then(function (res) {
            busy($button, false);
            fillEditor(res.publication);
            refreshLists();

            if (res.pdfFailed) showError(res.message);
            else toast('success', res.message);

            if (res.notice) Swal.fire({ icon: 'info', title: 'Please note', text: res.notice, confirmButtonColor: '#5b3fd6' });
            return res;
        }, function (message) {
            busy($button, false);
            showError(message);
            return $.Deferred().reject();
        });
    }

    // ------------------------------------------------------------------
    // PDF preview
    // ------------------------------------------------------------------
    function renderPreview() {
        var p = state.preview;
        var hasPublished = !!p.pdf.published;
        var bothDiffer = hasPublished && p.hasUnpublishedChanges && p.pdf.draft;

        if (state.previewVersion === 'published' && !hasPublished) state.previewVersion = 'draft';
        var url = state.previewVersion === 'published' ? p.pdf.published : p.pdf.draft;

        $('#hsaPreviewTitle').text(p.displayTitle + ' · ' + p.entryDate);
        stateBanner(p, '#hsaPreviewState');

        $('#hsaPreviewVersions').html(bothDiffer
            ? '<button type="button" data-v="draft" class="' + (state.previewVersion === 'draft' ? 'active' : '') + '">New (not live)</button>' +
              '<button type="button" data-v="published" class="' + (state.previewVersion === 'published' ? 'active' : '') + '">Live version</button>'
            : '');

        // Phones and small tablets cannot show a PDF inside the page, so they
        // get the page image (same layout) plus a link to the PDF itself.
        var small = window.matchMedia('(max-width: 767.98px)').matches;
        var body;

        if (!url) {
            body = '<div class="hsa-empty text-white"><i class="bi bi-file-earmark-x"></i>No PDF has been generated yet.</div>';
        } else if (small) {
            body = '<div class="hsa-preview-image"><img src="' + esc(url.replace('/pdf?', '/image?')) + '" alt="PDF page preview">' +
                '<a class="btn hsa-btn hsa-btn-light" href="' + esc(url) + '" target="_blank" rel="noopener"><i class="bi bi-box-arrow-up-right"></i> Open the PDF</a></div>';
        } else {
            body = '<iframe src="' + esc(url) + '#view=FitH" title="PDF preview"></iframe>';
        }

        $('#hsaPreviewBody').html(body);
        $('#hsaPreviewBody img').on('error', function () { $(this).remove(); });

        $('#hsaPreviewDownload').attr('href', url ? url + '&download=true' : '#').toggleClass('disabled', !url);

        var archived = p.status === 'Archived';
        var published = p.status === 'Published';
        $('#hsaPreviewEdit, #hsaPreviewRegenerate').prop('disabled', archived);
        $('#hsaPreviewPublish')
            .html('<i class="bi bi-send-check"></i> ' + (published ? 'Republish' : 'Publish'))
            .prop('disabled', archived || !p.hasPdf || p.pdfOutdated || (published && !p.hasUnpublishedChanges));
    }

    function openPreview(id) {
        api('GET', 'publications/' + id).then(function (res) {
            state.preview = res.publication;
            state.previewVersion = 'draft';
            renderPreview();
            previewModal.show();
        }, showError);
    }

    // ------------------------------------------------------------------
    // Master settings
    // ------------------------------------------------------------------
    function loadMaster() {
        api('GET', '/api/hem-sampark/master').then(function (m) {
            state.loaded.master = true;
            m = m || {};

            $('#hsaMMantraEn').val(m.morningMantraEn || '');
            $('#hsaMMantraHi').val(m.morningMantraHi || '');
            $('#hsaMValuesEn').val(m.valuesInFocusEn || '');
            $('#hsaMValuesHi').val(m.valuesInFocusHi || '');
            $('#hsaMFooterText').val(m.footerText || '');
            $('#hsaMFooterLink').val(m.footerLink || '');
            $('#hsaMasterUpdated').text(m.updatedAt ? 'Last saved ' + m.updatedAt : '');

            [m.logo1Url, m.logo2Url, m.logo3Url].forEach(function (url, i) {
                $('[data-hsa-logo="' + (i + 1) + '"]').html(url
                    ? '<img src="' + esc(url) + '" alt="Current logo">'
                    : '<div class="hsa-logo-none">No logo uploaded</div>');
            });
            $('#hsaLogo1, #hsaLogo2, #hsaLogo3').val('');
        }, showError);
    }

    function saveMaster(e) {
        e.preventDefault();

        var data = new FormData();
        ['1', '2', '3'].forEach(function (n) {
            var file = $('#hsaLogo' + n)[0].files[0];
            if (file) data.append('logo' + n + 'File', file);
        });
        data.append('morningMantraEn', $('#hsaMMantraEn').val());
        data.append('morningMantraHi', $('#hsaMMantraHi').val());
        data.append('valuesInFocusEn', $('#hsaMValuesEn').val());
        data.append('valuesInFocusHi', $('#hsaMValuesHi').val());
        data.append('footerText', $('#hsaMFooterText').val());
        data.append('footerLink', $('#hsaMFooterLink').val());

        var $button = $('#hsaMasterSave');
        busy($button, true);

        api('POST', '/api/hem-sampark/master', data).then(function (res) {
            busy($button, false);
            toast('success', res.message);
            loadMaster();
        }, function (message) {
            busy($button, false);
            showError(message);
        });
    }

    // ------------------------------------------------------------------
    // Reports
    // ------------------------------------------------------------------
    function reportQuery() {
        return { search: $('#hsaRSearch').val(), from: $('#hsaRFrom').val(), to: $('#hsaRTo').val() };
    }

    function loadReports() {
        var query = $.param($.extend(reportQuery(), { page: state.repPage, pageSize: state.repPageSize }));

        $('#hsaRRows').html('<tr><td colspan="10" class="hsa-loading"><span class="hsa-spin"></span></td></tr>');

        api('GET', 'reports/attempts?' + query).then(function (data) {
            $('#hsaRSummary').html(
                '<span class="hsa-chip">Quiz attempts <b>' + data.total + '</b></span>' +
                '<span class="hsa-chip">Participating users <b>' + data.participants + '</b></span>' +
                '<span class="hsa-chip">Average score <b>' + data.averagePercent + '%</b></span>');

            if (!data.items.length) {
                $('#hsaRRows').html('<tr><td colspan="10"><div class="hsa-empty"><i class="bi bi-clipboard-x"></i>No quiz attempts found.</div></td></tr>');
                $('#hsaRPager').empty();
                return;
            }

            $('#hsaRRows').html(data.items.map(function (r) {
                var tone = r.status === 'Needs Improvement' ? 'warn' : 'Published';
                return '<tr>' +
                    '<td><div class="fw-semibold text-nowrap">' + esc(r.userName) + '</div><div class="hsa-sub">' + esc(r.mobile || '') + '</div></td>' +
                    '<td>' + esc(r.userCode || r.userId) + (r.userCode ? '<div class="hsa-sub">ID ' + esc(r.userId) + '</div>' : '') + '</td>' +
                    '<td><div class="text-nowrap">' + esc(r.publicationDate) + '</div><div class="hsa-sub" style="max-width:220px;overflow-wrap:anywhere">' + esc(r.publicationTitle) + '</div></td>' +
                    '<td class="text-center">' + r.attempts + '</td>' +
                    '<td class="text-center text-success fw-semibold">' + r.correct + '</td>' +
                    '<td class="text-center text-danger fw-semibold">' + r.incorrect + '</td>' +
                    '<td class="text-center">' + r.unanswered + '</td>' +
                    '<td class="text-nowrap"><span class="hsa-score">' + esc(r.score) + '</span> · ' + r.percentage + '%<span class="hsa-bar"><span style="width:' + Math.min(100, r.percentage) + '%"></span></span></td>' +
                    '<td><span class="hsa-badge hsa-badge-' + tone + '">' + esc(r.status) + '</span></td>' +
                    '<td class="text-nowrap">' + esc(r.completedAt) + '</td></tr>';
            }).join(''));

            pager($('#hsaRPager'), data, function (page) { state.repPage = page; loadReports(); });
        }, function (message) {
            $('#hsaRRows').html('<tr><td colspan="10"><div class="hsa-empty">' + esc(message) + '</div></td></tr>');
        });
    }

    // ------------------------------------------------------------------
    // Wiring
    // ------------------------------------------------------------------
    $(function () {
        if (!document.getElementById('hsaRoot')) return;

        editorModal = new bootstrap.Modal(document.getElementById('hsaEditor'), { keyboard: false });
        previewModal = new bootstrap.Modal(document.getElementById('hsaPreview'));

        $('.hsa-tab').on('click', function () { showTab($(this).data('hsa-tab')); });
        $('.hsa-subtab').on('click', function () { showSubTab($(this).data('hsa-sub')); });
        $('#hsaNewBtn').on('click', function () { openEditor(0); });

        // Publication filters
        var reloadPubs = function () { state.pubPage = 1; loadPublications(); };
        $('#hsaStatus, #hsaFrom, #hsaTo').on('change', reloadPubs);
        $('#hsaSearch').on('input', debounce(reloadPubs, 350));
        $('#hsaReset').on('click', function () {
            $('#hsaSearch, #hsaStatus, #hsaFrom, #hsaTo').val('');
            reloadPubs();
        });

        // Report filters
        var reloadReports = function () { state.repPage = 1; loadReports(); };
        $('#hsaRFrom, #hsaRTo').on('change', reloadReports);
        $('#hsaRSearch').on('input', debounce(reloadReports, 350));
        $('#hsaRReset').on('click', function () {
            $('#hsaRSearch, #hsaRFrom, #hsaRTo').val('');
            reloadReports();
        });
        $('#hsaRExport').on('click', function () {
            window.location.href = API + '/reports/attempts/export?' + $.param(reportQuery());
        });

        $('#hsaMasterForm').on('submit', saveMaster);

        // Quiz editor
        $('#hsaAddQ').on('click', function () {
            $('#hsaQuestions').append(questionCard());
            renumberQuestions();
            $('#hsaQuestions .hsa-q:last [data-f="questionText"]').trigger('focus');
        });

        $('#hsaQuestions').on('click', '[data-q]', function () {
            var $card = $(this).closest('.hsa-q');
            var action = $(this).data('q');

            if (action === 'up') $card.prev('.hsa-q').before($card);
            if (action === 'down') $card.next('.hsa-q').after($card);
            if (action === 'remove') {
                var hasText = $.trim($card.find('[data-f="questionText"]').val());
                if (!hasText) { $card.remove(); renumberQuestions(); return; }

                confirmAction('Delete this question?', 'It is removed when you save the publication.', 'Delete', 'warning')
                    .then(function (yes) { if (yes) { $card.remove(); renumberQuestions(); } });
                return;
            }
            renumberQuestions();
        });

        $('#hsaQuestions').on('change', '[data-f="isActive"]', function () {
            $(this).closest('.hsa-q').toggleClass('is-off', !this.checked);
        });

        // Editor actions
        $('#hsaSaveDraft').on('click', function () { saveEditor(false, $(this)); });
        $('#hsaSaveGenerate').on('click', function () {
            saveEditor(true, $(this)).then(function (res) {
                if (!res.pdfFailed) { editorModal.hide(); openPreview(res.publication.id); }
            });
        });

        $('#hsaPreviewFromEditor').on('click', function () {
            if (!state.editing) return;
            var id = state.editing.id;

            if (!isDirty()) { editorModal.hide(); openPreview(id); return; }

            confirmAction('Save your changes first?', 'The preview shows the last generated PDF. Save and regenerate to see your latest changes.', 'Save & regenerate')
                .then(function (yes) { if (yes) $('#hsaSaveGenerate').trigger('click'); });
        });

        $('#hsaPublishFromEditor').on('click', function () {
            if (!state.editing) return;

            if (isDirty() || state.editing.pdfOutdated) {
                toast('warning', 'Save and regenerate the PDF first, then review it before publishing.');
                return;
            }

            workflow('publish', state.editing.id).then(function (publication) { fillEditor(publication); });
        });

        $('#hsaEditorClose').on('click', function () {
            if (!isDirty()) { editorModal.hide(); return; }

            confirmAction('Discard unsaved changes?', 'Your changes to this publication have not been saved.', 'Discard', 'warning')
                .then(function (yes) { if (yes) editorModal.hide(); });
        });

        // Preview actions
        $('#hsaPreviewVersions').on('click', 'button', function () {
            state.previewVersion = $(this).data('v');
            renderPreview();
        });
        $('#hsaPreviewEdit').on('click', function () { openEditor(state.preview.id); });
        $('#hsaPreviewRegenerate').on('click', function () {
            workflow('generate', state.preview.id).then(function (publication) {
                state.preview = publication;
                state.previewVersion = 'draft';
                renderPreview();
            });
        });
        $('#hsaPreviewPublish').on('click', function () {
            workflow('publish', state.preview.id).then(function (publication) {
                state.preview = publication;
                renderPreview();
            });
        });
        $('#hsaPreview').on('hidden.bs.modal', function () { $('#hsaPreviewBody').empty(); });

        loadStats();
    });
})(jQuery);
