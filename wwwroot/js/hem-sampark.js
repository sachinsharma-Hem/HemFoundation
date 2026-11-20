/* HEM Sampark user dashboard (Views/HemSampark/Dashboard.cshtml).
   Flow: Today -> Read the publication -> Quiz -> Result, plus My Performance.
   Correct answers never reach the browser before the quiz is submitted; the
   server scores the quiz and returns the review afterwards. */
(function () {
    'use strict';

    var API = '/api/hem-sampark';

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
    var view = document.getElementById('hsView');
    if (!view) return;

    var state = {
        today: null,       // response of /today
        quiz: null,        // { publicationId, questions }
        answers: {},       // questionId -> selected option number
        index: 0,
        submitting: false,
        zoom: 100,
        historyPage: 1
    };

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    function esc(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function api(method, path, body) {
        var options = { method: method, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } };

        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }

        return fetch(API + path, options).then(function (response) {
            if (response.status === 401) {
                window.location.replace('/?samparkLogin=1');
                return new Promise(function () { });
            }

            return response.text().then(function (text) {
                var data = {};
                try { data = text ? JSON.parse(text) : {}; } catch (e) { data = {}; }

                if (!response.ok && !data.message) {
                    data.message = 'Something went wrong (error ' + response.status + '). Please try again.';
                }
                data.httpStatus = response.status;
                data.ok = response.ok;
                return data;
            });
        }, function () {
            return { ok: false, httpStatus: 0, message: 'Could not reach the server. Check your internet connection and try again.' };
        });
    }

    function render(html) {
        view.classList.remove('hs-view');
        void view.offsetWidth; // restart the fade-in
        view.classList.add('hs-view');
        view.innerHTML = html;
        window.scrollTo(0, 0);
    }

    function loading(text) {
        render('<div class="hs-loading"><div class="hs-spinner"></div><p>' + esc(text) + '</p></div>');
    }

    function errorCard(message, retry) {
        render('<div class="hs-card hs-empty"><div class="hs-empty-icon">😕</div><h2>Something went wrong</h2>' +
            '<p>' + esc(message) + '</p><button type="button" class="hs-btn hs-btn-primary" id="hsRetry"><i class="bi bi-arrow-clockwise"></i> Try again</button></div>');
        document.getElementById('hsRetry').addEventListener('click', retry);
    }

    function setNav(name) {
        document.querySelectorAll('[data-hs-nav]').forEach(function (button) {
            button.classList.toggle('active', button.getAttribute('data-hs-nav') === name);
        });
    }

    function on(id, handler) {
        var el = document.getElementById(id);
        if (el) el.addEventListener('click', handler);
    }

    function statusPill(status) {
        var tone = status === 'Excellent' ? 'green' : status === 'Passed' ? 'purple' : 'amber';
        return '<span class="hs-pill hs-pill-' + tone + '">' + esc(status) + '</span>';
    }

    function storageKey() {
        return 'hsQuiz_' + (state.quiz ? state.quiz.publicationId : 0);
    }

    function saveProgress() {
        try { sessionStorage.setItem(storageKey(), JSON.stringify(state.answers)); } catch (e) { /* private mode */ }
    }

    function clearProgress() {
        try { sessionStorage.removeItem(storageKey()); } catch (e) { /* private mode */ }
    }

    // ------------------------------------------------------------------
    // Home
    // ------------------------------------------------------------------
    function loadHome() {
        setNav('home');
        loading("Loading today's HEM Sampark…");

        api('GET', '/today').then(function (data) {
            if (!data.ok) return errorCard(data.message, loadHome);

            state.today = data;

            var initials = (data.userName || 'C').split(/\s+/).map(function (part) { return part.charAt(0); }).join('').slice(0, 2).toUpperCase();
            document.getElementById('hsAvatar').textContent = initials;
            document.getElementById('hsUserName').textContent = data.userName;

            renderHome();
            autoOpenOncePerDay();
        });
    }

    // The first time a user arrives on a day, open today's publication
    // straight away; "I have read it – Start Quiz" then leads to the quiz.
    function autoOpenOncePerDay() {
        var p = state.today && state.today.publication;
        if (!p || !p.pdfAvailable) return;

        var key = 'hsAutoRead_' + p.id + '_' + (state.today.userName || '');
        try {
            if (localStorage.getItem(key)) return;
            localStorage.setItem(key, '1');
        } catch (e) {
            return;   // storage blocked: do not keep opening it
        }

        openReader();
    }

    function weekStrip(perf) {
        var icons = { done: 'bi-check-lg', missed: 'bi-x-lg', today: 'bi-star-fill', none: 'bi-dash' };
        var titles = { done: 'Completed', missed: 'Missed', today: "Today's challenge is waiting", none: 'No publication' };

        return '<div class="hs-week">' + perf.week.map(function (day) {
            return '<div class="hs-day ' + day.state + '" title="' + esc(day.date + ' – ' + titles[day.state]) + '">' +
                '<div class="hs-day-dot"><i class="bi ' + icons[day.state] + '"></i></div>' + esc(day.label) + '</div>';
        }).join('') + '</div>';
    }

    function sidePanel(data) {
        var perf = data.performance;
        var streakText = perf.streak > 0
            ? '<b>You are on a ' + perf.streak + '-day value streak!</b><br>Come back tomorrow to keep it alive.'
            : "<b>Start your value streak today.</b><br>Complete today's challenge to light the first flame.";

        return '<div class="hs-card">' +
            '<h3 class="hs-h">🔥 7-Day Value Streak</h3>' + weekStrip(perf) +
            '<div class="hs-streak-note"><span class="hs-fire">🔥</span><span>' + streakText + '</span></div></div>' +

            '<div class="hs-card"><h3 class="hs-h">📊 My Quiz Performance</h3>' +
            '<div class="hs-tiles">' +
            '<div class="hs-tile"><b>' + perf.attempts + '</b><span>Quizzes taken</span></div>' +
            '<div class="hs-tile green"><b>' + perf.averagePercent + '%</b><span>Average score</span></div>' +
            '<div class="hs-tile amber"><b>' + perf.bestPercent + '%</b><span>Best score</span></div></div>' +
            '<div id="hsRecent" class="hs-recent" style="margin-top:14px"></div>' +
            '<button type="button" class="hs-btn hs-btn-ghost hs-btn-block hs-btn-sm" id="hsAllHistory" style="margin-top:12px">' +
            '<i class="bi bi-clock-history"></i> View my quiz history</button></div>';
    }

    function renderHome() {
        var data = state.today;
        var p = data.publication;
        var icon = data.greeting === 'Good Morning' ? '🌤️' : data.greeting === 'Good Afternoon' ? '☀️' : '🌙';

        var main = '<div class="hs-card">' +
            '<div class="hs-greet"><span class="hs-greet-icon">' + icon + '</span><div>' +
            '<h1>' + esc(data.greeting) + ', ' + esc(data.userName) + '!</h1>' +
            '<p>' + esc(data.today) + " · Let's make today a better day! 💙</p></div></div>";

        if (!p) {
            main += '<div class="hs-empty"><div class="hs-empty-icon">📭</div>' +
                "<h2>Today's publication is not available yet.</h2>" +
                '<p>Please check back a little later. Meanwhile you can look at your past quiz results.</p>' +
                '<button type="button" class="hs-btn hs-btn-ghost" id="hsRefresh"><i class="bi bi-arrow-clockwise"></i> Check again</button></div></div>';
        } else {
            var quiz = p.quiz;

            main += '<div class="hs-hero"><div class="hs-hero-top">' +
                "<span class=\"hs-eyebrow\">Today's publication</span>" +
                '<span class="hs-pill hs-pill-purple"><i class="bi bi-calendar3"></i> ' + esc(p.date) + '</span></div>' +
                (p.pictureUrl ? '<img class="hs-hero-pic" src="' + esc(p.pictureUrl) + '" alt="">' : '') +
                '<h2>' + esc(p.title) + '</h2>' +
                (p.headingHi ? '<p class="hs-hero-hi">' + esc(p.headingHi) + '</p>' : '') +
                '</div>' +

                // Today's three steps: I Morning Mantra, II the English paragraph, III Values in Focus.
                '<ol class="hs-steps">' +
                ((p.morningMantra || p.morningMantraHi)
                    ? '<li><span class="hs-step-no">I</span><div><b>Morning Mantra</b>' +
                      (p.morningMantra ? '<p>' + esc(p.morningMantra) + '</p>' : '') +
                      (p.morningMantraHi ? '<p class="hs-hi">' + esc(p.morningMantraHi) + '</p>' : '') + '</div></li>'
                    : '') +
                (p.summary
                    ? '<li><span class="hs-step-no">II</span><div><b>' + esc(p.headingEn || 'Today') + '</b>' +
                      '<p class="hs-step-text">' + esc(p.summary) + '</p></div></li>'
                    : '') +
                ((p.valuesInFocus || p.valuesInFocusHi)
                    ? '<li><span class="hs-step-no">III</span><div><b>Values in Focus</b>' +
                      (p.valuesInFocus ? '<p>' + esc(p.valuesInFocus) + '</p>' : '') +
                      (p.valuesInFocusHi ? '<p class="hs-hi">' + esc(p.valuesInFocusHi) + '</p>' : '') + '</div></li>'
                    : '') +
                '</ol>' +

                // Read first, then the quiz: both cards are buttons.
                '<div class="hs-avail">' +
                '<button type="button" class="hs-avail-item ' + (p.pdfAvailable ? 'hs-ok' : 'hs-wait') + '" id="hsCardPdf"' + (p.pdfAvailable ? '' : ' disabled') + '>' +
                '<span class="hs-avail-step">1</span><i class="bi bi-file-earmark-pdf-fill"></i><div><b>Read the PDF</b>' + (p.pdfAvailable ? 'Tap to open' : 'Not available') + '</div></button>' +
                '<button type="button" class="hs-avail-item ' + (quiz.available ? 'hs-ok' : 'hs-wait') + '" id="hsCardQuiz"' + (quiz.available ? '' : ' disabled') + '>' +
                '<span class="hs-avail-step">2</span><i class="bi bi-patch-question-fill"></i><div><b>Take the quiz</b>' +
                (!quiz.available ? 'No quiz today' : quiz.attempted ? 'Completed · see result' : quiz.totalQuestions + ' question' + (quiz.totalQuestions === 1 ? '' : 's')) + '</div></button></div>';

            if (quiz.attempted && quiz.result) {
                var r = quiz.result;
                main += '<div class="hs-done"><div class="hs-done-top"><div><b><i class="bi bi-patch-check-fill"></i> Today\'s challenge completed!</b>' +
                    '<div class="hs-small hs-muted">Score ' + esc(r.score) + ' · ' + r.percentage + '% · ' + esc(r.completedAt) + '</div></div>' +
                    statusPill(r.status) + '</div></div>';
            }

            main += '<div class="hs-actions">';
            if (p.pdfAvailable) {
                main += '<button type="button" class="hs-btn hs-btn-primary hs-btn-block" id="hsRead">' +
                    "<i class=\"bi bi-book-half\"></i> READ TODAY'S PUBLICATION</button>";
            }
            if (quiz.attempted) {
                main += '<button type="button" class="hs-btn hs-btn-ghost hs-btn-block" id="hsViewResult"><i class="bi bi-trophy"></i> View my result</button>';
            } else if (quiz.available) {
                main += "<button type=\"button\" class=\"hs-btn hs-btn-ghost hs-btn-block\" id=\"hsStart\"><i class=\"bi bi-play-circle-fill\"></i> START TODAY'S CHALLENGE</button>";
            }
            main += '</div></div>';
        }

        render('<div class="hs-grid"><div>' + main + '</div><div>' + sidePanel(data) + '</div></div>');

        on('hsRefresh', loadHome);
        on('hsRead', openReader);
        on('hsCardPdf', openReader);
        on('hsCardQuiz', function () {
            var q = state.today.publication.quiz;
            if (q.attempted) renderResult(q.result, true); else startQuiz();
        });
        on('hsStart', startQuiz);
        on('hsViewResult', function () { renderResult(state.today.publication.quiz.result, true); });
        on('hsAllHistory', function () { loadHistory(1); });

        loadRecent();
    }

    function loadRecent() {
        api('GET', '/history?page=1&pageSize=5').then(function (data) {
            var box = document.getElementById('hsRecent');
            if (!box || !data.ok) return;

            if (!data.items.length) {
                box.innerHTML = '<p class="hs-small hs-muted" style="margin:0;text-align:center">Your previous quiz attempts will appear here.</p>';
                return;
            }

            box.innerHTML = data.items.slice(0, 3).map(function (item) {
                return '<button type="button" data-result="' + item.submissionId + '">' +
                    '<span class="hs-recent-main"><b>' + esc(item.publicationTitle) + '</b><span>' + esc(item.publicationDate) + '</span></span>' +
                    '<span class="hs-pill ' + (item.passed ? 'hs-pill-green' : 'hs-pill-amber') + '">' + esc(item.score) + '</span>' +
                    '<i class="bi bi-chevron-right hs-muted"></i></button>';
            }).join('');

            box.querySelectorAll('[data-result]').forEach(function (button) {
                button.addEventListener('click', function () { openResult(button.getAttribute('data-result')); });
            });
        });
    }

    // ------------------------------------------------------------------
    // Reader
    // ------------------------------------------------------------------
    function openReader() {
        var p = state.today && state.today.publication;
        if (!p || !p.pdfAvailable) return;

        var quiz = p.quiz;
        state.zoom = 100;

        // Phones cannot display PDFs inline, so the page image is shown when
        // available; the PDF itself stays one tap away.
        var body = p.imageUrl
            ? '<img id="hsPage" src="' + esc(p.imageUrl) + '" alt="' + esc(p.title) + ' – publication page">'
            : '<iframe src="' + esc(p.pdfUrl) + '#view=FitH" title="' + esc(p.title) + '"></iframe>';

        var cta = quiz.attempted
            ? '<button type="button" class="hs-btn hs-btn-primary hs-btn-block" id="hsReaderResult"><i class="bi bi-trophy"></i> View my result</button>'
            : quiz.available
                ? '<button type="button" class="hs-btn hs-btn-primary hs-btn-block" id="hsReaderStart"><i class="bi bi-check2-circle"></i> I have read it – Start Quiz</button>'
                : '';

        render('<div class="hs-bar">' +
            '<button type="button" class="hs-back" id="hsBack" aria-label="Back"><i class="bi bi-arrow-left"></i></button>' +
            '<div class="hs-bar-title"><b>' + esc(p.title) + '</b><span>' + esc(p.date) + (p.issueNumber ? ' · Issue ' + esc(p.issueNumber) : '') + '</span></div>' +
            '<div class="hs-tools">' +
            (p.imageUrl ? '<button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" id="hsZoomOut" aria-label="Zoom out"><i class="bi bi-zoom-out"></i></button>' +
                '<button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" id="hsZoomIn" aria-label="Zoom in"><i class="bi bi-zoom-in"></i></button>' : '') +
            '<a class="hs-btn hs-btn-ghost hs-btn-sm" href="' + esc(p.pdfUrl) + '" target="_blank" rel="noopener" aria-label="Open PDF"><i class="bi bi-box-arrow-up-right"></i><span>Open PDF</span></a>' +
            '<a class="hs-btn hs-btn-ghost hs-btn-sm" href="' + esc(p.downloadUrl) + '" aria-label="Download PDF"><i class="bi bi-download"></i><span>Download</span></a>' +
            '</div></div>' +
            '<div class="hs-reader" id="hsReaderBox">' + body + '</div>' +
            (cta ? '<div class="hs-reader-cta">' + cta + '</div>' : ''));

        on('hsBack', renderHome);
        on('hsReaderStart', startQuiz);
        on('hsReaderResult', function () { renderResult(quiz.result, true); });

        function zoom(step) {
            state.zoom = Math.max(100, Math.min(300, state.zoom + step));
            var img = document.getElementById('hsPage');
            img.style.width = state.zoom + '%';
            img.style.maxWidth = state.zoom === 100 ? '' : 'none';
        }
        on('hsZoomIn', function () { zoom(50); });
        on('hsZoomOut', function () { zoom(-50); });

        var page = document.getElementById('hsPage');
        if (page) {
            page.addEventListener('error', function () {
                document.getElementById('hsReaderBox').innerHTML =
                    '<iframe src="' + esc(p.pdfUrl) + '#view=FitH" title="' + esc(p.title) + '"></iframe>';
            });
        }
    }

    // ------------------------------------------------------------------
    // Quiz
    // ------------------------------------------------------------------
    function startQuiz() {
        loading('Getting your quiz ready…');

        api('GET', '/quiz').then(function (data) {
            if (data.alreadySubmitted) return loadHome();
            if (!data.ok) return errorCard(data.message, loadHome);

            state.quiz = data;
            state.index = 0;
            state.answers = {};

            // Restore answers if the page was refreshed mid-quiz.
            try {
                var saved = JSON.parse(sessionStorage.getItem(storageKey()) || '{}');
                data.questions.forEach(function (q) {
                    var value = saved[q.id];
                    if (q.options.some(function (o) { return o.value === value; })) state.answers[q.id] = value;
                });
            } catch (e) { /* ignore */ }

            renderQuestion();
        });
    }

    function answeredCount() {
        return state.quiz.questions.filter(function (q) { return state.answers[q.id]; }).length;
    }

    function renderQuestion() {
        var questions = state.quiz.questions;
        var total = questions.length;
        var q = questions[state.index];
        var answered = answeredCount();
        var percent = Math.round((state.index + 1) * 100 / total);
        var last = state.index === total - 1;
        var letters = ['A', 'B', 'C', 'D'];

        render('<div class="hs-quiz">' +
            '<div class="hs-bar"><button type="button" class="hs-back" id="hsQuizExit" aria-label="Leave quiz"><i class="bi bi-arrow-left"></i></button>' +
            '<div class="hs-bar-title"><b>' + esc(state.quiz.title) + '</b><span>Day\'s Challenge</span></div></div>' +

            '<div class="hs-card">' +
            '<div class="hs-progress-head"><span>Question ' + (state.index + 1) + ' of ' + total + '</span><span>' + percent + '%</span></div>' +
            '<div class="hs-progress" role="progressbar" aria-valuemin="0" aria-valuemax="' + total + '" aria-valuenow="' + (state.index + 1) + '"><span style="width:' + percent + '%"></span></div>' +
            '<div class="hs-small hs-muted">Answered ' + answered + ' of ' + total + '</div>' +

            '<p class="hs-q-text">' + esc(q.text) + '</p>' +
            '<div class="hs-options" role="radiogroup" aria-label="Answer options">' +
            q.options.map(function (option, i) {
                var selected = state.answers[q.id] === option.value;
                return '<button type="button" class="hs-option' + (selected ? ' selected' : '') + '" role="radio" aria-checked="' + selected + '" data-value="' + option.value + '">' +
                    '<span class="hs-option-key">' + letters[i] + '</span><span>' + esc(option.text) + '</span></button>';
            }).join('') + '</div>' +

            '<div class="hs-quiz-nav">' +
            '<button type="button" class="hs-btn hs-btn-ghost" id="hsPrev"' + (state.index === 0 ? ' disabled' : '') + '><i class="bi bi-arrow-left"></i> Previous</button>' +
            (last
                ? '<button type="button" class="hs-btn hs-btn-green" id="hsSubmit"><i class="bi bi-check2"></i> Submit Quiz</button>'
                : '<button type="button" class="hs-btn hs-btn-primary" id="hsNext">Next <i class="bi bi-arrow-right"></i></button>') +
            '</div>' +

            '<div class="hs-dots" aria-label="Jump to question">' +
            questions.map(function (item, i) {
                return '<button type="button" class="hs-dot' + (state.answers[item.id] ? ' answered' : '') + (i === state.index ? ' current' : '') +
                    '" data-jump="' + i + '" aria-label="Question ' + (i + 1) + (state.answers[item.id] ? ', answered' : ', not answered') + '">' + (i + 1) + '</button>';
            }).join('') + '</div>' +
            (!last && answered > 0 ? '<div style="text-align:center;margin-top:14px"><button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" id="hsSubmitEarly"><i class="bi bi-check2"></i> Submit Quiz</button></div>' : '') +
            '</div></div>');

        view.querySelectorAll('.hs-option').forEach(function (button) {
            button.addEventListener('click', function () {
                state.answers[q.id] = Number(button.getAttribute('data-value'));
                saveProgress();
                renderQuestion();
            });
        });

        view.querySelectorAll('[data-jump]').forEach(function (button) {
            button.addEventListener('click', function () {
                state.index = Number(button.getAttribute('data-jump'));
                renderQuestion();
            });
        });

        on('hsPrev', function () { state.index--; renderQuestion(); });
        on('hsNext', function () { state.index++; renderQuestion(); });
        on('hsSubmit', confirmSubmit);
        on('hsSubmitEarly', confirmSubmit);
        on('hsQuizExit', function () {
            if (!answeredCount()) return renderHome();

            Swal.fire({
                icon: 'question', title: 'Leave the quiz?',
                text: 'Your answers so far are kept on this device until you submit.',
                showCancelButton: true, confirmButtonText: 'Leave', cancelButtonText: 'Keep going',
                confirmButtonColor: '#5b3fd6', reverseButtons: true
            }).then(function (result) { if (result.isConfirmed) renderHome(); });
        });
    }

    function confirmSubmit() {
        if (state.submitting) return;

        var total = state.quiz.questions.length;
        var answered = answeredCount();
        var missing = total - answered;

        Swal.fire({
            icon: missing ? 'warning' : 'question',
            title: 'Submit your quiz?',
            html: 'You answered <b>' + answered + ' of ' + total + '</b> questions.' +
                (missing ? '<br><b>' + missing + ' unanswered</b> question' + (missing === 1 ? '' : 's') + ' will be marked as not attempted.' : '') +
                '<br><br>You can submit only once.',
            showCancelButton: true,
            confirmButtonText: 'Yes, submit',
            cancelButtonText: missing ? 'Review answers' : 'Not yet',
            confirmButtonColor: '#1e9e5a',
            reverseButtons: true
        }).then(function (result) { if (result.isConfirmed) submitQuiz(); });
    }

    function submitQuiz() {
        if (state.submitting) return;
        state.submitting = true;

        var answers = state.quiz.questions
            .filter(function (q) { return state.answers[q.id]; })
            .map(function (q) { return { questionId: q.id, selectedOption: state.answers[q.id] }; });

        loading('Submitting your answers…');

        api('POST', '/submit-quiz', { dailyEntryId: state.quiz.publicationId, answers: answers }).then(function (data) {
            state.submitting = false;

            if (data.ok || data.alreadySubmitted) {
                clearProgress();
                if (state.today && state.today.publication) {
                    state.today.publication.quiz.attempted = true;
                    state.today.publication.quiz.result = data.result;
                }
                if (data.alreadySubmitted) {
                    Swal.fire({ icon: 'info', title: 'Already submitted', text: data.message, confirmButtonColor: '#5b3fd6' });
                }
                renderResult(data.result, true);

                // Refresh streak / performance figures in the background.
                api('GET', '/today').then(function (fresh) { if (fresh.ok) state.today = fresh; });
                return;
            }

            // Answers are still in memory, so the user can simply try again.
            Swal.fire({ icon: 'error', title: 'Submission failed', text: data.message, confirmButtonColor: '#5b3fd6' });
            renderQuestion();
        });
    }

    // ------------------------------------------------------------------
    // Result
    // ------------------------------------------------------------------
    function renderResult(r, fromToday, pdfUrl) {
        if (!r) return loadHome();

        var emoji = r.status === 'Excellent' ? '🏆' : r.passed ? '🎉' : '💪';
        var headline = r.status === 'Excellent' ? 'Congratulations!' : r.passed ? 'Well done!' : 'Keep going!';
        var letters = ['A', 'B', 'C', 'D'];

        var review = (r.review || []).map(function (item) {
            var skipped = !item.selected;
            var cls = skipped ? 'skipped' : item.isCorrect ? 'right' : 'wrong';
            var tag = skipped
                ? '<span class="hs-pill hs-pill-amber">Not answered</span>'
                : item.isCorrect ? '<span class="hs-pill hs-pill-green"><i class="bi bi-check-lg"></i> Correct</span>'
                    : '<span class="hs-pill hs-pill-red"><i class="bi bi-x-lg"></i> Incorrect</span>';

            var options = item.options.map(function (text, i) {
                if (!text) return '';
                var n = i + 1;
                var isCorrect = n === item.correct;
                var isWrongPick = n === item.selected && !isCorrect;

                return '<div class="hs-review-opt' + (isCorrect ? ' is-correct' : isWrongPick ? ' is-wrong' : '') + '">' +
                    '<i class="bi ' + (isCorrect ? 'bi-check-circle-fill' : isWrongPick ? 'bi-x-circle-fill' : 'bi-circle') + '"></i>' +
                    '<span>' + letters[i] + '. ' + esc(text) +
                    (n === item.selected ? ' <span class="hs-small">(your answer)</span>' : '') + '</span></div>';
            }).join('');

            return '<div class="hs-review-item ' + cls + '">' +
                '<div class="hs-review-q"><span>Q' + item.number + '. ' + esc(item.text) + '</span>' + tag + '</div>' + options +
                (item.explanation ? '<div class="hs-explain">💡 ' + esc(item.explanation) + '</div>' : '') + '</div>';
        }).join('');

        render('<div class="hs-result">' +
            '<div class="hs-bar"><button type="button" class="hs-back" id="hsResultBack" aria-label="Back"><i class="bi bi-arrow-left"></i></button>' +
            '<div class="hs-bar-title"><b>Quiz Result</b><span>' + esc(r.publicationTitle) + '</span></div></div>' +

            '<div class="hs-card"><div class="hs-result-head">' +
            '<div class="hs-result-emoji">' + emoji + '</div><h2>' + headline + '</h2>' +
            '<div class="hs-muted hs-small">Your score</div>' +
            '<div class="hs-result-score ' + (r.passed ? 'pass' : 'fail') + '">' + esc(r.score) + ' · ' + r.percentage + '%</div>' +
            statusPill(r.status) + '</div>' +

            '<div class="hs-result-tiles">' +
            '<div class="hs-tile"><b>' + r.totalQuestions + '</b><span>Total questions</span></div>' +
            '<div class="hs-tile green"><b>' + r.correct + '</b><span>Correct</span></div>' +
            '<div class="hs-tile red"><b>' + r.incorrect + '</b><span>Incorrect</span></div>' +
            '<div class="hs-tile amber"><b>' + r.unanswered + '</b><span>Unanswered</span></div></div>' +

            '<div class="hs-meta">' +
            '<div><span>Publication date</span><b>' + esc(r.publicationDate) + '</b></div>' +
            '<div><span>Result status</span><b>' + esc(r.status) + '</b></div>' +
            '<div><span>Completed on</span><b>' + esc(r.completedAt) + '</b></div></div></div>' +

            (review ? '<div class="hs-card"><h3 class="hs-h">📝 Answer review</h3>' + review + '</div>' : '') +

            '<div class="hs-actions" style="margin-top:16px">' +
            (pdfUrl ? '<a class="hs-btn hs-btn-ghost hs-btn-block" href="' + esc(pdfUrl) + '" target="_blank" rel="noopener"><i class="bi bi-file-earmark-pdf"></i> Open this publication</a>' : '') +
            '<button type="button" class="hs-btn hs-btn-primary hs-btn-block" id="hsResultHome"><i class="bi bi-house-heart"></i> Back to dashboard</button>' +
            '<button type="button" class="hs-btn hs-btn-ghost hs-btn-block" id="hsResultHistory"><i class="bi bi-clock-history"></i> My quiz history</button>' +
            '</div></div>');

        on('hsResultBack', fromToday ? loadHome : function () { loadHistory(state.historyPage); });
        on('hsResultHome', loadHome);
        on('hsResultHistory', function () { loadHistory(1); });
    }

    function openResult(submissionId) {
        loading('Loading your result…');

        api('GET', '/results/' + encodeURIComponent(submissionId)).then(function (data) {
            if (!data.ok) return errorCard(data.message, function () { loadHistory(state.historyPage); });
            renderResult(data.result, false, data.pdfUrl);
        });
    }

    // ------------------------------------------------------------------
    // History
    // ------------------------------------------------------------------
    function loadHistory(page) {
        setNav('history');
        state.historyPage = page || 1;
        loading('Loading your quiz history…');

        api('GET', '/history?page=' + state.historyPage + '&pageSize=10').then(function (data) {
            if (!data.ok) return errorCard(data.message, function () { loadHistory(state.historyPage); });

            var perf = state.today ? state.today.performance : null;

            var summary = perf
                ? '<div class="hs-card"><h3 class="hs-h">📊 My Performance</h3><div class="hs-tiles">' +
                  '<div class="hs-tile"><b>' + perf.attempts + '</b><span>Quizzes taken</span></div>' +
                  '<div class="hs-tile green"><b>' + perf.averagePercent + '%</b><span>Average score</span></div>' +
                  '<div class="hs-tile amber"><b>' + perf.streak + '</b><span>Day streak 🔥</span></div></div></div>'
                : '';

            var body;

            if (!data.items.length) {
                body = '<div class="hs-empty"><div class="hs-empty-icon">🗂️</div><h2>No quiz attempts yet</h2>' +
                    "<p>Read today's publication and take the quiz. Your results will be saved here.</p>" +
                    '<button type="button" class="hs-btn hs-btn-primary" id="hsHistoryHome"><i class="bi bi-house-heart"></i> Go to today</button></div>';
            } else {
                var pages = Math.max(1, Math.ceil(data.total / data.pageSize));

                body = '<div class="hs-table-wrap"><table class="hs-table"><thead><tr>' +
                    '<th>Publication</th><th>Publication date</th><th>Attempted on</th><th>Questions</th><th>Correct</th><th>Incorrect</th><th>Score</th><th>Result</th><th></th>' +
                    '</tr></thead><tbody>' +
                    data.items.map(function (item) {
                        return '<tr>' +
                            '<td data-label="Publication" class="hs-t-title">' + esc(item.publicationTitle) + '</td>' +
                            '<td data-label="Publication date">' + esc(item.publicationDate) + '</td>' +
                            '<td data-label="Attempted on">' + esc(item.attemptDate) + '</td>' +
                            '<td data-label="Questions">' + item.totalQuestions + '</td>' +
                            '<td data-label="Correct"><b style="color:var(--hs-green)">' + item.correct + '</b></td>' +
                            '<td data-label="Incorrect"><b style="color:var(--hs-red)">' + item.incorrect + '</b>' + (item.unanswered ? ' <span class="hs-small hs-muted">(+' + item.unanswered + ' skipped)</span>' : '') + '</td>' +
                            '<td data-label="Score"><b>' + esc(item.score) + '</b> · ' + item.percentage + '%</td>' +
                            '<td data-label="Result">' + statusPill(item.status) + '</td>' +
                            '<td class="hs-t-action"><button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" data-result="' + item.submissionId + '">View Result</button></td></tr>';
                    }).join('') + '</tbody></table></div>' +
                    '<div class="hs-pager"><span>' + data.total + ' attempt' + (data.total === 1 ? '' : 's') + ' · Page ' + data.page + ' of ' + pages + '</span><div>' +
                    '<button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" id="hsPagePrev"' + (data.page <= 1 ? ' disabled' : '') + '><i class="bi bi-chevron-left"></i> Previous</button>' +
                    '<button type="button" class="hs-btn hs-btn-ghost hs-btn-sm" id="hsPageNext"' + (data.page >= pages ? ' disabled' : '') + '>Next <i class="bi bi-chevron-right"></i></button></div></div>';
            }

            render(summary + '<div class="hs-card"><h3 class="hs-h">🕘 My Quiz History</h3>' + body + '</div>');

            on('hsHistoryHome', loadHome);
            on('hsPagePrev', function () { loadHistory(state.historyPage - 1); });
            on('hsPageNext', function () { loadHistory(state.historyPage + 1); });

            view.querySelectorAll('[data-result]').forEach(function (button) {
                button.addEventListener('click', function () { openResult(button.getAttribute('data-result')); });
            });
        });
    }

    // ------------------------------------------------------------------
    // Start
    // ------------------------------------------------------------------
    document.querySelectorAll('[data-hs-nav]').forEach(function (button) {
        button.addEventListener('click', function () {
            if (button.getAttribute('data-hs-nav') === 'history') {
                if (state.today) loadHistory(1);
                else api('GET', '/today').then(function (data) { if (data.ok) state.today = data; loadHistory(1); });
            } else {
                loadHome();
            }
        });
    });

    loadHome();
})();
