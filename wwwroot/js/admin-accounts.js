/* User Management → Admin accounts popup (api/admin-accounts). */
(function () {
    'use strict';

    var API = '/api/admin-accounts';
    var modalEl = document.getElementById('adminAccountsModal');
    var openBtn = document.getElementById('btnAdminAccounts');
    if (!modalEl || !openBtn) return;

    var $ = function (id) { return document.getElementById(id); };
    var state = { items: [], roles: [], canManage: false, meId: 0, roleColumn: false };
    var editing = null;   // the admin being edited, or null when adding

    function esc(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function api(method, path, body) {
        var opts = { method: method, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } };
        if (body !== undefined) { opts.headers['Content-Type'] = 'application/json'; opts.body = JSON.stringify(body); }
        return fetch(API + path, opts).then(function (r) {
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
        return (name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(function (w) { return w[0].toUpperCase(); }).join('');
    }

    function roleOptions(selected) {
        return state.roles.map(function (r) {
            var disabled = r !== 'Admin' && !state.roleColumn;
            return '<option value="' + esc(r) + '"' + (r === selected ? ' selected' : '') + (disabled ? ' disabled' : '') + '>' + esc(r) + '</option>';
        }).join('');
    }

    function render() {
        var d = state;
        $('aaSummary').innerHTML =
            '<span class="badge rounded-pill text-bg-light border px-3 py-2">Admins <b>' + d.total + '</b></span>' +
            '<span class="badge rounded-pill text-bg-light border px-3 py-2">Active <b class="text-success">' + d.active + '</b></span>' +
            '<span class="badge rounded-pill text-bg-light border px-3 py-2">Super admins <b class="text-primary">' + d.superAdmins + '</b></span>';

        var notice = '';
        if (!d.canManage) notice += '<div class="alert alert-info py-2 small"><i class="bi bi-info-circle me-1"></i>Only a super admin can add admins or change their role.</div>';
        else if (!d.roleColumn) notice += '<div class="alert alert-warning py-2 small"><i class="bi bi-exclamation-triangle me-1"></i>To give roles other than <b>Admin</b>, run <code>Data/Scripts/RunOnce_Update_2026_11_25.sql</code> on the database once.</div>';
        $('aaNotice').innerHTML = notice;
        $('aaToggleForm').hidden = !d.canManage;

        $('aaRows').innerHTML = d.items.length ? d.items.map(function (a, i) {
            var me = a.id === d.meId;
            var roleCell;
            if (d.canManage) {
                roleCell = '<div class="d-flex flex-column gap-1">' +
                    '<select class="form-select form-select-sm" data-role="' + i + '"' + (a.isSuperAdmin ? ' disabled' : '') + '>' + roleOptions(a.isSuperAdmin ? 'Admin' : a.role) + '</select>' +
                    '<div class="form-check form-switch small mb-0"><input class="form-check-input" type="checkbox" role="switch" id="aaS' + i + '" data-super="' + i + '"' + (a.isSuperAdmin ? ' checked' : '') + (me ? ' disabled' : '') + '>' +
                    '<label class="form-check-label" for="aaS' + i + '">Super admin</label></div></div>';
            } else {
                roleCell = '<span class="badge rounded-pill ' + (a.isSuperAdmin ? 'text-bg-primary' : 'text-bg-secondary') + '">' + esc(a.role) + '</span>';
            }
            var statusCell = d.canManage
                ? '<div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" role="switch" id="aaA' + i + '" data-active="' + i + '"' + (a.isActive ? ' checked' : '') + (me ? ' disabled' : '') + '>' +
                  '<label class="form-check-label small" for="aaA' + i + '">' + (a.isActive ? 'Active' : 'Inactive') + '</label></div>'
                : '<span class="badge rounded-pill ' + (a.isActive ? 'text-bg-success' : 'text-bg-light border') + '">' + (a.isActive ? 'Active' : 'Inactive') + '</span>';

            return '<tr class="' + (a.isActive ? '' : 'text-muted') + '">' +
                '<td class="ps-3"><div class="d-flex align-items-center gap-2"><span class="rounded-circle d-inline-grid text-white fw-bold" style="width:36px;height:36px;place-items:center;background:' + (a.isSuperAdmin ? '#4B2E83' : '#6c757d') + '">' + esc(initials(a.name)) + '</span>' +
                '<div><div class="fw-semibold">' + esc(a.name) + (me ? ' <span class="badge text-bg-warning">You</span>' : '') + '</div></div></div></td>' +
                '<td><div>' + esc(a.email || '—') + '</div><div class="small text-muted">' + esc(a.mobile || '') + '</div></td>' +
                '<td>' + roleCell + '</td><td>' + statusCell + '</td><td class="small text-muted">' + esc(a.createdOn) + '</td>' +
                '<td class="pe-3 text-end">' + (d.canManage || me
                    ? '<button type="button" class="btn btn-sm btn-outline-primary rounded-pill" data-edit="' + i + '" title="Edit details or set a new password"><i class="bi bi-pencil-square"></i><span class="d-none d-lg-inline ms-1">Edit</span></button>'
                    : '') + '</td></tr>';
        }).join('') : '<tr><td colspan="6" class="text-center text-muted py-4">No admins yet.</td></tr>';

        $('aaRole').innerHTML = roleOptions('Admin');
    }

    function load() {
        $('aaRows').innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Loading…</td></tr>';
        return api('GET', '').then(function (j) {
            state = { items: j.items, roles: j.roles, canManage: j.canManage, meId: j.meId, roleColumn: j.roleColumn, total: j.total, active: j.active, superAdmins: j.superAdmins };
            render();
        }).catch(function (e) {
            $('aaRows').innerHTML = '<tr><td colspan="6" class="text-center text-danger py-4">' + esc(e.message) + '</td></tr>';
        });
    }

    function showForm(show, admin) {
        editing = show && admin ? admin : null;
        $('aaForm').hidden = !show;
        $('aaFormError').hidden = true;
        if (!show) return;
        $('aaForm').reset();
        var pw = $('aaPassword'); pw.type = 'password'; $('aaShowPw').innerHTML = '<i class="bi bi-eye"></i>';
        if (editing) {
            $('aaFormTitle').innerHTML = '<i class="bi bi-pencil-square text-primary me-1"></i> Edit admin · ' + esc(editing.name);
            $('aaFirst').value = editing.firstName || '';
            $('aaLast').value = editing.lastName || '';
            $('aaEmail').value = editing.email || '';
            $('aaMobile').value = editing.mobile || '';
            $('aaPwLabel').textContent = 'New password';
            $('aaPwReq').hidden = true;
            pw.required = false;
            pw.placeholder = 'Leave empty to keep the current password';
            $('aaPwHint').textContent = 'Only fill this to reset a forgotten password. Share the new password with the person safely.';
            $('aaRoleCol').hidden = true;   // role and super admin are changed in the list
            $('aaSave').innerHTML = '<i class="bi bi-check2 me-1"></i> Save changes';
        } else {
            $('aaFormTitle').innerHTML = '<i class="bi bi-person-plus text-primary me-1"></i> New admin';
            $('aaPwLabel').textContent = 'Password';
            $('aaPwReq').hidden = false;
            pw.required = true;
            pw.placeholder = 'At least 8 characters, letters and numbers';
            $('aaPwHint').textContent = 'Share it with the person safely; they log in with the email and this password.';
            $('aaRoleCol').hidden = false;
            $('aaRole').innerHTML = roleOptions('Admin'); $('aaRole').disabled = false;
            $('aaSave').innerHTML = '<i class="bi bi-check2 me-1"></i> Create admin';
        }
        $('aaForm').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        $('aaFirst').focus();
    }

    openBtn.addEventListener('click', function () {
        showForm(false);
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
        load();
    });
    $('aaToggleForm').addEventListener('click', function () { showForm($('aaForm').hidden || !!editing); });
    $('aaRows').addEventListener('click', function (e) {
        var b = e.target.closest('[data-edit]'); if (!b) return;
        showForm(true, state.items[Number(b.getAttribute('data-edit'))]);
    });
    $('aaCancel').addEventListener('click', function () { showForm(false); });
    $('aaSuper').addEventListener('change', function () { $('aaRole').disabled = this.checked; });
    $('aaMobile').addEventListener('input', function () { this.value = this.value.replace(/\D/g, '').slice(0, 10); });
    $('aaShowPw').addEventListener('click', function () {
        var pw = $('aaPassword'); pw.type = pw.type === 'password' ? 'text' : 'password';
        this.innerHTML = pw.type === 'password' ? '<i class="bi bi-eye"></i>' : '<i class="bi bi-eye-slash"></i>';
    });
    $('aaGenPw').addEventListener('click', function () {
        var chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789', out = '';
        var buf = new Uint32Array(12); (window.crypto || window.msCrypto).getRandomValues(buf);
        for (var i = 0; i < 12; i++) out += chars[buf[i] % chars.length];
        out = out.slice(0, 10) + (buf[0] % 10) + '@';
        var pw = $('aaPassword'); pw.value = out; pw.type = 'text';
        $('aaShowPw').innerHTML = '<i class="bi bi-eye-slash"></i>';
    });

    $('aaForm').addEventListener('submit', function (e) {
        e.preventDefault();
        var err = $('aaFormError'); err.hidden = true;
        var btn = $('aaSave'); btn.disabled = true;
        var call = editing
            ? api('PUT', '/' + editing.id, {
                firstName: $('aaFirst').value, lastName: $('aaLast').value, email: $('aaEmail').value, mobile: $('aaMobile').value,
                newPassword: $('aaPassword').value
            })
            : api('POST', '', {
                firstName: $('aaFirst').value, lastName: $('aaLast').value, email: $('aaEmail').value, mobile: $('aaMobile').value,
                password: $('aaPassword').value, role: $('aaRole').value, isSuperAdmin: $('aaSuper').checked
            });
        call.then(function (j) {
            toast('success', j.message); showForm(false); load();
        }).catch(function (ex) {
            err.textContent = ex.message; err.hidden = false;
        }).then(function () { btn.disabled = false; });
    });

    $('aaRows').addEventListener('change', function (e) {
        var t = e.target, i, a;
        if (t.hasAttribute('data-role') || t.hasAttribute('data-super')) {
            i = Number(t.getAttribute('data-role') || t.getAttribute('data-super')); a = state.items[i];
            var makeSuper = t.hasAttribute('data-super') ? t.checked : a.isSuperAdmin;
            var row = t.closest('tr'), sel = row.querySelector('[data-role]');
            api('PUT', '/' + a.id + '/role', { role: sel ? sel.value : 'Admin', isSuperAdmin: makeSuper })
                .then(function (j) { toast('success', j.message); load(); })
                .catch(function (ex) { toast('error', ex.message); load(); });
        } else if (t.hasAttribute('data-active')) {
            i = Number(t.getAttribute('data-active')); a = state.items[i];
            api('PUT', '/' + a.id + '/status', { isActive: t.checked })
                .then(function (j) { toast('success', j.message); load(); })
                .catch(function (ex) { toast('error', ex.message); load(); });
        }
    });
})();
