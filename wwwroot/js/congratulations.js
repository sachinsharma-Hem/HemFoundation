/* Congratulations: birthday / anniversary cards (Views/Congratulations/Index.cshtml). */
(function () {
    'use strict';

    var root = document.getElementById('cgRoot');
    if (!root) return;

    var API = '/api/congratulations';

    // Fixed designs. Positions are in the template's own pixels.
    var TEMPLATES = {
        Birthday: {
            src: '/images/congratulations/birthday-template.jpg',
            star: { cx: 630, cy: 304, h: 120 },
            name: { cy: 447, maxWidth: 920, size: 75 }
        },
        Anniversary: {
            src: '/images/congratulations/anniversary-template.jpg',
            star: { cx: 1015, cy: 491, h: 198 },
            name: { cy: 745, maxWidth: 1480, size: 121 }
        }
    };

    var NAME_COLOR = '#c7040a';
    var DATE_COLOR = '#fff200';
    var NAME_FONT = "'Mukta', 'Noto Sans Devanagari', 'Mangal', sans-serif";
    var DATE_FONT = "'Poppins', 'Arial Black', Arial, sans-serif";
    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

    var images = {};
    var occasion = 'Birthday';
    var photoWish = null;
    var photoFile = null;
    var pages = { contacts: 1, pending: 1, given: 1 };

    var $ = function (id) { return document.getElementById(id); };
    var esc = function (v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    };

    // ------------------------------------------------------------------
    // API
    // ------------------------------------------------------------------
    function api(method, path, body) {
        var options = { method: method, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } };

        if (body instanceof FormData) options.body = body;
        else if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }

        return fetch(API + path, options).then(function (r) {
            if (r.status === 401) { window.location.replace('/'); throw new Error('Session expired'); }
            return r.json().catch(function () { return {}; }).then(function (j) {
                if (!r.ok || j.success === false) throw new Error(j.message || 'Something went wrong. Please try again.');
                return j;
            });
        });
    }

    function toast(icon, text) {
        if (window.Swal) Swal.fire({ toast: true, position: 'top-end', icon: icon, title: text, showConfirmButton: false, timer: 3000 });
    }

    function showError(id, message) {
        $(id).textContent = message || '';
        $(id).hidden = !message;
    }

    // ------------------------------------------------------------------
    // Card drawing
    // ------------------------------------------------------------------
    function loadImage(src) {
        if (images[src]) return images[src];
        images[src] = new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { resolve(img); };
            img.onerror = reject;
            img.src = src;
        });
        return images[src];
    }

    var fontsReady = (document.fonts && document.fonts.load)
        ? Promise.all([
            document.fonts.load("800 60px 'Mukta'", 'प्रिय डॉ. चेतना'),
            document.fonts.load("700 60px 'Poppins'", '27 Nov')
        ]).catch(function () { })
        : Promise.resolve();

    function nameOnCard() {
        var name = $('cgName').value.trim();
        if (!name) return '';
        if ($('cgPriya').checked && name.indexOf('प्रिय') !== 0) name = 'प्रिय ' + name;
        return name;
    }

    function dateParts() {
        var v = $('cgDate').value;   // yyyy-MM-dd
        if (!/^\d{4}-\d{2}-\d{2}$/.test(v)) return null;
        return { day: Number(v.slice(8, 10)), month: MONTHS[Number(v.slice(5, 7)) - 1] };
    }

    function draw() {
        var t = TEMPLATES[occasion];
        var canvas = $('cgCanvas');

        return Promise.all([loadImage(t.src), fontsReady]).then(function (res) {
            var img = res[0];
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;

            var ctx = canvas.getContext('2d');
            ctx.drawImage(img, 0, 0);

            // Date inside the star
            var d = dateParts();
            if (d) {
                ctx.fillStyle = DATE_COLOR;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.font = '700 ' + Math.round(t.star.h * 0.5) + 'px ' + DATE_FONT;
                ctx.fillText(String(d.day), t.star.cx, t.star.cy - t.star.h * 0.07);
                ctx.font = '700 ' + Math.round(t.star.h * 0.245) + 'px ' + DATE_FONT;
                ctx.fillText(d.month, t.star.cx, t.star.cy + t.star.h * 0.225);
            }

            // Name line (shrinks to fit)
            var name = nameOnCard();
            if (name) {
                var size = t.name.size;
                ctx.font = '800 ' + size + 'px ' + NAME_FONT;
                while (ctx.measureText(name).width > t.name.maxWidth && size > 20) {
                    size -= 2;
                    ctx.font = '800 ' + size + 'px ' + NAME_FONT;
                }
                ctx.fillStyle = NAME_COLOR;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(name, canvas.width / 2, t.name.cy);
            }
        }).catch(function () {
            showError('cgFormError', 'The card design could not be loaded. Please refresh the page.');
        });
    }

    function validateCard() {
        if (!$('cgName').value.trim()) return 'Please enter the name.';
        if (!dateParts()) return 'Please choose the date.';
        return null;
    }

    // File names stay in plain English letters: some browsers drop names
    // written in other scripts. A Hindi name gives "Birthday-2026-11-27.jpg".
    function fileBase() {
        var latin = $('cgName').value.trim().replace(/[^A-Za-z0-9 ]/g, '').trim().replace(/\s+/g, '-');
        return occasion + '-' + $('cgDate').value + (latin ? '-' + latin : '');
    }

    // Records the card (and the contact) on the server before downloading.
    function recordCard() {
        return api('POST', '/wishes', {
            contactId: Number($('cgContactId').value) || null,
            name: $('cgName').value.trim(),
            occasion: occasion,
            date: $('cgDate').value,
            mobile: $('cgMobile').value.trim(),
            saveContact: $('cgSave').checked
        });
    }

    function saveBlob(blob, name) {
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 3000);
    }

    function cardJpeg() {
        return draw().then(function () {
            return new Promise(function (resolve) { $('cgCanvas').toBlob(resolve, 'image/jpeg', 0.93); });
        });
    }

    function download(kind, button) {
        var problem = validateCard();
        showError('cgFormError', problem);
        if (problem) return;

        button.disabled = true;

        recordCard()
            .then(function (res) {
                if (res.contact) $('cgContactId').value = res.contact.id;
                return cardJpeg();
            })
            .then(function (blob) {
                if (kind === 'jpg') {
                    saveBlob(blob, fileBase() + '.jpg');
                    return;
                }
                var form = new FormData();
                form.append('image', blob, 'card.jpg');
                form.append('fileName', fileBase());
                return fetch(API + '/card-pdf', {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }, body: form
                }).then(function (r) {
                    if (!r.ok) throw new Error('The PDF could not be made.');
                    return r.blob();
                }).then(function (pdf) { saveBlob(pdf, fileBase() + '.pdf'); });
            })
            .then(function () {
                toast('success', 'Card downloaded.');
                refreshAll();
            })
            .catch(function (e) { showError('cgFormError', e.message); })
            .then(function () { button.disabled = false; });
    }

    function setOccasion(value) {
        occasion = value === 'Anniversary' ? 'Anniversary' : 'Birthday';
        root.querySelectorAll('.cg-toggle button').forEach(function (b) {
            b.classList.toggle('active', b.getAttribute('data-occasion') === occasion);
        });
        draw();
    }

    function fillMaker(c, date) {
        $('cgContactId').value = c.id || '';
        $('cgName').value = c.name || '';
        $('cgDate').value = date || c.nextDate || '';
        $('cgMobile').value = c.mobile || '';
        $('cgPriya').checked = (c.name || '').indexOf('प्रिय') !== 0;
        setOccasion(c.occasion);
        showError('cgFormError', '');
        $('cgMaker').scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    // ------------------------------------------------------------------
    // Summary and upcoming
    // ------------------------------------------------------------------
    function loadSummary() {
        api('GET', '/summary').then(function (s) {
            $('cgSummary').innerHTML =
                '<span class="cg-chip is-hot">Today <b>' + s.today + '</b></span>' +
                '<span class="cg-chip">Next 7 days <b>' + s.next7 + '</b></span>' +
                '<span class="cg-chip">Contacts <b>' + s.contacts + '</b> (' + s.birthdays + ' birthdays, ' + s.anniversaries + ' anniversaries)</span>' +
                '<span class="cg-chip">Given <b>' + s.delivered + '</b></span>' +
                '<span class="cg-chip">Photo pending <b>' + s.pending + '</b></span>';
        }).catch(function () { });
    }

    function statusBadge(wish) {
        if (!wish) return '<span class="cg-badge is-new">Card not made</span>';
        return wish.status === 'Delivered'
            ? '<span class="cg-badge is-given"><i class="bi bi-check2"></i> Given</span>'
            : '<span class="cg-badge is-made">Card made · photo pending</span>';
    }

    var upcomingItems = [];

    function loadUpcoming() {
        api('GET', '/upcoming?days=7').then(function (res) {
            $('cgToday').textContent = '· from ' + res.todayLabel;
            upcomingItems = res.items;

            $('cgUpcoming').innerHTML = res.items.length ? res.items.map(function (x, i) {
                var c = x.contact, w = x.wish;
                var when = c.daysLeft === 0 ? '<span class="cg-badge is-today">Today</span>'
                    : c.daysLeft === 1 ? 'Tomorrow' : 'In ' + c.daysLeft + ' days';

                return '<div class="cg-up' + (c.daysLeft === 0 ? ' is-today' : '') + '">' +
                    (w && w.photoUrl ? '<img class="cg-up-photo" src="' + esc(w.photoUrl) + '" alt="Photo when the card was given">' : '') +
                    '<div class="cg-up-top"><span class="cg-up-ico' + (c.occasion === 'Anniversary' ? ' is-anniv' : '') + '"><i class="bi ' +
                    (c.occasion === 'Anniversary' ? 'bi-heart-fill' : 'bi-cake2-fill') + '"></i></span>' +
                    '<div><button type="button" class="cg-up-name" data-up-photo="' + i + '" title="Add the photo when the card is given">' + esc(c.name) + '</button>' +
                    '<div class="cg-up-meta">' + esc(c.occasion) + ' · ' + esc(c.nextLabel) + ' · ' + when + '</div></div></div>' +
                    '<div>' + statusBadge(w) + '</div>' +
                    '<div class="cg-up-actions">' +
                    '<button type="button" class="cg-btn is-sm is-primary" data-up-make="' + i + '"><i class="bi bi-magic"></i> Make card</button>' +
                    '<button type="button" class="cg-btn is-sm" data-up-photo="' + i + '"><i class="bi bi-camera"></i> ' + (w && w.photoUrl ? 'Change photo' : 'Add photo') + '</button>' +
                    '</div></div>';
            }).join('') : '<div class="cg-empty">No birthdays or anniversaries in the next 7 days.</div>';
        }).catch(function (e) {
            $('cgUpcoming').innerHTML = '<div class="cg-empty">' + esc(e.message) + '</div>';
        });
    }

    // ------------------------------------------------------------------
    // Contact list
    // ------------------------------------------------------------------
    var contactItems = [];

    function pager(el, data, key, reload) {
        var pagesCount = Math.max(1, Math.ceil(data.total / data.pageSize));
        el.innerHTML = '<span>' + data.total + ' record' + (data.total === 1 ? '' : 's') + ' · Page ' + data.page + ' of ' + pagesCount + '</span>' +
            '<div><button type="button" class="cg-btn is-sm" data-p="-1"' + (data.page <= 1 ? ' disabled' : '') + '>Previous</button>' +
            '<button type="button" class="cg-btn is-sm" data-p="1"' + (data.page >= pagesCount ? ' disabled' : '') + '>Next</button></div>';
        el.querySelectorAll('[data-p]').forEach(function (b) {
            b.addEventListener('click', function () { pages[key] += Number(b.getAttribute('data-p')); reload(); });
        });
    }

    function loadContacts() {
        var q = new URLSearchParams({
            search: $('cgSearch').value.trim(), occasion: $('cgFilterType').value,
            month: $('cgFilterMonth').value, page: pages.contacts, pageSize: 25
        });

        api('GET', '/contacts?' + q).then(function (res) {
            contactItems = res.items;
            $('cgContacts').innerHTML = res.items.length ? res.items.map(function (c, i) {
                return '<tr>' +
                    '<td data-label="Name"><b>' + esc(c.name) + '</b>' + (c.notes ? '<div class="cg-up-meta">' + esc(c.notes) + '</div>' : '') + '</td>' +
                    '<td data-label="Occasion">' + esc(c.occasion) + '</td>' +
                    '<td data-label="Date">' + esc(c.dayMonth) + (c.year ? ' ' + c.year : '') + '</td>' +
                    '<td data-label="Next">' + esc(c.nextLabel) + (c.daysLeft === 0 ? ' <span class="cg-badge is-today">Today</span>' : ' <span class="cg-up-meta">(' + c.daysLeft + ' days)</span>') + '</td>' +
                    '<td data-label="Mobile">' + esc(c.mobile || '—') + '</td>' +
                    '<td class="is-actions"><div class="cg-row-actions">' +
                    '<button type="button" class="cg-btn is-sm is-primary" data-c-make="' + i + '"><i class="bi bi-magic"></i> Card</button>' +
                    '<button type="button" class="cg-btn is-sm" data-c-edit="' + i + '"><i class="bi bi-pencil"></i></button>' +
                    '<button type="button" class="cg-btn is-sm" data-c-del="' + i + '" aria-label="Remove"><i class="bi bi-trash"></i></button>' +
                    '</div></td></tr>';
            }).join('') : '<tr><td colspan="6" class="cg-empty">No contacts yet. Add one, or make a card – it is saved here automatically.</td></tr>';

            pager($('cgContactsPager'), res, 'contacts', loadContacts);
        }).catch(function (e) {
            $('cgContacts').innerHTML = '<tr><td colspan="6" class="cg-empty">' + esc(e.message) + '</td></tr>';
        });
    }

    function openContact(c) {
        showError('cgContactError', '');
        $('cgContactTitle').textContent = c ? 'Edit contact' : 'Add contact';
        $('cgCId').value = c ? c.id : '';
        $('cgCName').value = c ? c.name : '';
        $('cgCOccasion').value = c ? c.occasion : 'Birthday';
        $('cgCYearKnown').checked = !c || !!c.year;
        $('cgCDate').value = c ? (c.date || ('2000-' + String(c.month).padStart(2, '0') + '-' + String(c.day).padStart(2, '0'))) : '';
        $('cgCMobile').value = c && c.mobile ? c.mobile : '';
        $('cgCNotes').value = c && c.notes ? c.notes : '';
        bootstrap.Modal.getOrCreateInstance($('cgContactModal')).show();
    }

    // ------------------------------------------------------------------
    // Cards made / given
    // ------------------------------------------------------------------
    var wishItems = { pending: [], given: [] };

    function loadWishes(kind) {
        var q = new URLSearchParams({
            status: kind === 'given' ? 'Delivered' : 'Generated',
            search: kind === 'given' ? $('cgGivenSearch').value.trim() : '',
            page: pages[kind], pageSize: 12
        });
        var box = kind === 'given' ? $('cgGiven') : $('cgPending');

        api('GET', '/wishes?' + q).then(function (res) {
            wishItems[kind] = res.items;
            box.innerHTML = res.items.length ? res.items.map(function (w, i) {
                return '<div class="cg-given">' +
                    (w.photoUrl ? '<img src="' + esc(w.photoUrl) + '" alt="Photo when the card was given" loading="lazy">'
                        : '<div class="cg-given-noimg"><i class="bi bi-camera"></i></div>') +
                    '<div class="cg-given-body"><b>' + esc(w.name) + '</b>' +
                    '<span>' + esc(w.occasion) + ' · ' + esc(w.dateLabel) + '</span>' +
                    '<span>' + (w.deliveredAt ? 'Given ' + esc(w.deliveredAt) : 'Card made ' + esc(w.generatedAt)) + '</span>' +
                    '<button type="button" class="cg-btn is-sm' + (w.photoUrl ? '' : ' is-primary') + '" data-w-photo="' + kind + ':' + i + '"><i class="bi bi-camera"></i> ' +
                    (w.photoUrl ? 'Change photo' : 'Add photo') + '</button></div></div>';
            }).join('') : '<div class="cg-empty">' + (kind === 'given' ? 'No cards given yet.' : 'Every card made has a photo.') + '</div>';

            pager(kind === 'given' ? $('cgGivenPager') : $('cgPendingPager'), res, kind, function () { loadWishes(kind); });
        }).catch(function (e) { box.innerHTML = '<div class="cg-empty">' + esc(e.message) + '</div>'; });
    }

    // ------------------------------------------------------------------
    // Photo when the card is given
    // ------------------------------------------------------------------
    function openPhoto(wish) {
        photoWish = wish;
        photoFile = null;
        showError('cgPhotoError', '');
        $('cgPhotoWho').textContent = wish.name + ' · ' + wish.occasion + ' · ' + wish.dateLabel;
        $('cgPhotoPreview').innerHTML = wish.photoUrl
            ? '<img src="' + esc(wish.photoUrl) + '" alt="Current photo">'
            : '<i class="bi bi-image"></i><span>No photo yet</span>';
        $('cgPhotoSave').disabled = true;
        $('cgPhotoCamera').value = '';
        $('cgPhotoGallery').value = '';
        bootstrap.Modal.getOrCreateInstance($('cgPhotoModal')).show();
    }

    // From the upcoming list: make sure there is a card record first.
    function openPhotoForUpcoming(item) {
        if (item.wish) { openPhoto(item.wish); return; }

        api('POST', '/wishes', {
            contactId: item.contact.id, name: item.contact.name, occasion: item.contact.occasion,
            date: item.contact.nextDate, mobile: item.contact.mobile, saveContact: true
        }).then(function (res) {
            item.wish = res.wish;
            openPhoto(res.wish);
        }).catch(function (e) { toast('error', e.message); });
    }

    function choosePhoto(input) {
        var file = input.files && input.files[0];
        if (!file) return;
        if (file.size > 12 * 1024 * 1024) { showError('cgPhotoError', 'The photo must be 12 MB or smaller.'); return; }
        photoFile = file;
        showError('cgPhotoError', '');
        $('cgPhotoPreview').innerHTML = '<img src="' + URL.createObjectURL(file) + '" alt="Selected photo">';
        $('cgPhotoSave').disabled = false;
    }

    function refreshAll() {
        loadSummary();
        loadUpcoming();
        loadContacts();
        loadWishes('pending');
        loadWishes('given');
    }

    // ------------------------------------------------------------------
    // Wiring
    // ------------------------------------------------------------------
    root.querySelectorAll('.cg-toggle button').forEach(function (b) {
        b.addEventListener('click', function () { setOccasion(b.getAttribute('data-occasion')); });
    });
    ['cgName', 'cgDate'].forEach(function (id) { $(id).addEventListener('input', draw); });
    $('cgPriya').addEventListener('change', draw);
    $('cgDownloadJpg').addEventListener('click', function () { download('jpg', this); });
    $('cgDownloadPdf').addEventListener('click', function () { download('pdf', this); });
    $('cgClear').addEventListener('click', function () {
        $('cgForm').reset();
        $('cgContactId').value = '';
        showError('cgFormError', '');
        draw();
    });
    $('cgName').addEventListener('input', function () { $('cgContactId').value = ''; });

    $('cgUpcoming').addEventListener('click', function (e) {
        var make = e.target.closest('[data-up-make]');
        var photo = e.target.closest('[data-up-photo]');
        if (make) { var it = upcomingItems[Number(make.getAttribute('data-up-make'))]; fillMaker(it.contact, it.contact.nextDate); }
        else if (photo) openPhotoForUpcoming(upcomingItems[Number(photo.getAttribute('data-up-photo'))]);
    });

    root.querySelectorAll('.cg-tabs button').forEach(function (b) {
        b.addEventListener('click', function () {
            var tab = b.getAttribute('data-tab');
            root.querySelectorAll('.cg-tabs button').forEach(function (x) { x.classList.toggle('active', x === b); });
            root.querySelectorAll('.cg-tab').forEach(function (p) { p.classList.toggle('active', p.getAttribute('data-tab-panel') === tab); });
        });
    });

    var searchTimer;
    $('cgSearch').addEventListener('input', function () { clearTimeout(searchTimer); searchTimer = setTimeout(function () { pages.contacts = 1; loadContacts(); }, 300); });
    $('cgFilterType').addEventListener('change', function () { pages.contacts = 1; loadContacts(); });
    $('cgFilterMonth').addEventListener('change', function () { pages.contacts = 1; loadContacts(); });
    $('cgGivenSearch').addEventListener('input', function () { clearTimeout(searchTimer); searchTimer = setTimeout(function () { pages.given = 1; loadWishes('given'); }, 300); });
    $('cgAddContact').addEventListener('click', function () { openContact(null); });

    $('cgContacts').addEventListener('click', function (e) {
        var b = e.target.closest('button');
        if (!b) return;
        if (b.hasAttribute('data-c-make')) fillMaker(contactItems[Number(b.getAttribute('data-c-make'))]);
        if (b.hasAttribute('data-c-edit')) openContact(contactItems[Number(b.getAttribute('data-c-edit'))]);
        if (b.hasAttribute('data-c-del')) {
            var c = contactItems[Number(b.getAttribute('data-c-del'))];
            var go = function () {
                api('DELETE', '/contacts/' + c.id).then(function (res) { toast('success', res.message); refreshAll(); })
                    .catch(function (err) { toast('error', err.message); });
            };
            if (window.Swal) Swal.fire({ icon: 'warning', title: 'Remove ' + c.name + '?', text: 'Cards already given stay in the history.', showCancelButton: true, confirmButtonText: 'Remove', confirmButtonColor: '#c7040a' })
                .then(function (r) { if (r.isConfirmed) go(); });
            else if (window.confirm('Remove ' + c.name + '?')) go();
        }
    });

    $('cgContactForm').addEventListener('submit', function (e) {
        e.preventDefault();
        var known = $('cgCYearKnown').checked;
        var date = $('cgCDate').value;
        if (date && !known) date = '2000' + date.slice(4);

        var button = $('cgContactSave');
        button.disabled = true;

        api('POST', '/contacts', {
            id: Number($('cgCId').value) || 0, name: $('cgCName').value, occasion: $('cgCOccasion').value,
            date: date, yearKnown: known, mobile: $('cgCMobile').value, notes: $('cgCNotes').value
        }).then(function (res) {
            bootstrap.Modal.getOrCreateInstance($('cgContactModal')).hide();
            toast('success', res.message);
            refreshAll();
        }).catch(function (err) { showError('cgContactError', err.message); })
          .then(function () { button.disabled = false; });
    });

    root.addEventListener('click', function (e) {
        var b = e.target.closest('[data-w-photo]');
        if (!b) return;
        var parts = b.getAttribute('data-w-photo').split(':');
        openPhoto(wishItems[parts[0]][Number(parts[1])]);
    });

    $('cgPhotoCamera').addEventListener('change', function () { choosePhoto(this); });
    $('cgPhotoGallery').addEventListener('change', function () { choosePhoto(this); });

    $('cgPhotoSave').addEventListener('click', function () {
        if (!photoWish || !photoFile) return;
        var button = this;
        button.disabled = true;

        var form = new FormData();
        form.append('photo', photoFile, photoFile.name || 'photo.jpg');

        api('POST', '/wishes/' + photoWish.id + '/photo', form).then(function (res) {
            bootstrap.Modal.getOrCreateInstance($('cgPhotoModal')).hide();
            toast('success', res.message);
            refreshAll();
        }).catch(function (err) {
            showError('cgPhotoError', err.message);
            button.disabled = false;
        });
    });

    draw();
    refreshAll();
})();
