(() => {
    const dialog = document.getElementById('delete-dialog');
    if (!dialog || typeof dialog.showModal !== 'function') return;
    const form = document.getElementById('delete-form');
    const error = document.getElementById('delete-error');
    const confirm = document.getElementById('delete-confirm');
    const cancel = document.getElementById('delete-cancel');
    let busy = false;
    let trigger;
    let requestVersion = 0;
    const showError = message => { error.textContent = message; error.hidden = false; };
    cancel.addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); });
    dialog.addEventListener('close', () => { requestVersion++; trigger?.focus(); });
    document.addEventListener('click', async event => {
        const link = event.target.closest('a[data-delete]');
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        trigger = link;
        const version = ++requestVersion;
        error.hidden = true;
        document.getElementById('delete-title').textContent = 'Delete ' + link.dataset.delete + '?';
        document.getElementById('delete-description').textContent = 'You are about to delete “' + link.dataset.name + '”. This action cannot be undone.';
        confirm.disabled = true;
        confirm.textContent = 'Loading…';
        dialog.showModal();
        cancel.focus();
        try {
            const response = await fetch(link.href, { credentials: 'same-origin' });
            if (!response.ok) throw new Error('Unable to load this record. Refresh the page and try again.');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (version !== requestVersion || !dialog.open) return;
            const source = page.querySelector('main form');
            const token = source?.querySelector('[name="__RequestVerificationToken"]');
            const id = source?.querySelector('[name="Id"]');
            if (!token || !id) throw new Error('Unable to prepare deletion. Refresh the page and try again.');
            form.action = new URL(source.getAttribute('action') || link.href, location.origin).href;
            form.elements.Id.value = id.value;
            form.elements.__RequestVerificationToken.value = token.value;
            confirm.disabled = false;
        } catch (failure) { if (version === requestVersion) showError(failure.message); }
        finally { if (version === requestVersion) confirm.textContent = 'Delete'; }
    });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || confirm.disabled) return;
        busy = true;
        confirm.disabled = cancel.disabled = true;
        confirm.textContent = 'Deleting…';
        error.hidden = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' });
            if (response.redirected && response.ok) { location.assign(response.url); return; }
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const message = page.querySelector('main .validation-summary-errors, main .alert-danger')?.textContent.trim();
            showError(message || 'Deletion could not be completed. Refresh the page to check the record before trying again.');
        } catch { showError('Connection interrupted. Refresh the page to check whether the record was deleted.'); }
        finally { busy = false; confirm.disabled = cancel.disabled = false; confirm.textContent = 'Delete'; }
    });
})();
