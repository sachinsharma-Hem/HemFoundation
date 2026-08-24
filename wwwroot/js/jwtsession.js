$(document).ready(function () {
    $('#ajaxLoginForm').on('submit', function (e) {
        e.preventDefault();

        const $submitBtn = $(this).find('button[type="submit"]');
        const originalBtnHtml = $submitBtn.html();
        const rememberMe = $('#rememberMe').is(':checked');

        $submitBtn.prop('disabled', true).html(`
            <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
            Signing in...
        `);

        const payload = {
            email: $('#loginEmail').val(),
            password: $('#loginPassword').val()
        };

        $.ajax({
            url: '/api/auth/login',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (response) {
                if (response.success) {
                    const storage = rememberMe ? localStorage : sessionStorage;

                    localStorage.clear();
                    sessionStorage.clear();

                    storage.setItem('jwt_token', response.token);
                    storage.setItem('user_session', JSON.stringify(response.user));
                    storage.setItem('user_menus', JSON.stringify(response.menus));
                    storage.setItem('user_rights', JSON.stringify(response.rights));
                    storage.setItem('token_expiry', response.expiration);
                    storage.setItem('user_role', response.user.roles ? response.user.roles[0] : '');

                    // Save Welcome Message temporarily
                    sessionStorage.setItem('login_success_toast', `Welcome back, ${response.user.fullName}!`);

                    // =========================================================================
                    // DYNAMIC REDIRECT: Redirects automatically based on user's role/portal
                    // =========================================================================
                    window.location.replace(response.redirectUrl);
                }
            },
            error: function (xhr) {
                $submitBtn.prop('disabled', false).html(originalBtnHtml);

                $('#liveToast').removeClass('bg-success').addClass('bg-danger');
                $('#toastMessage').html(`<i class="bi bi-exclamation-triangle-fill me-2"></i> ${xhr.responseJSON?.message || 'Invalid email or password.'}`);

                const toast = new bootstrap.Toast(document.getElementById('liveToast'));
                toast.show();
            }
        });
    });
});