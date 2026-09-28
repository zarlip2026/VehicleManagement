document.querySelectorAll('input[data-icon-max-bytes]').forEach(input => {
    function validateIcon() {
        const file = input.files[0];
        let message = '';
        if (file) {
            const extension = file.name.split('.').pop().toLowerCase();
            const allowedExtensions = input.accept.split(',').map(value => value.trim().replace(/^\./, ''));
            if (!file.name.includes('.') || !allowedExtensions.includes(extension)) {
                message = 'Choose a PNG, JPG, JPEG, WebP, GIF, BMP or AVIF image.';
            } else if (file.size === 0) {
                message = 'A category icon is required.';
            } else if (file.size > Number(input.dataset.iconMaxBytes)) {
                message = 'The category icon must be 1 MB (1,000,000 bytes) or smaller.';
            }
        }
        input.setCustomValidity(message);
        document.getElementById('icon-error').textContent = message;
        return !message;
    }
    input.addEventListener('change', validateIcon);
    input.form.addEventListener('submit', event => {
        if (!validateIcon()) {
            event.preventDefault();
            input.reportValidity();
        }
    });
});
