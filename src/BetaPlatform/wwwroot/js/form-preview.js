/* Beta Platform — creation-page live preview + submit guard (002-ui-modernization).
   Progressive enhancement: forms work fully without this script; it only mirrors the
   current field values into a preview panel and guards against duplicate submits. */
(function () {
    'use strict';

    // ---- Live preview -------------------------------------------------------
    // A preview target element declares: data-preview-for="<input id>"
    //   data-preview="text"        -> textContent = field value (or data-empty)
    //   data-preview="option"      -> textContent = selected <option> text
    //   data-preview="badge"       -> checkbox: toggles data-on/data-off text + class
    // Optional: data-empty="placeholder shown when blank"
    function textForField(el) {
        if (!el) { return ''; }
        if (el.tagName === 'SELECT') {
            var opt = el.options[el.selectedIndex];
            var val = opt ? (opt.value || '') : '';
            return val ? opt.text.trim() : '';
        }
        return (el.value || '').trim();
    }

    function renderTarget(target) {
        var srcId = target.getAttribute('data-preview-for');
        var src = document.getElementById(srcId);
        if (!src) { return; }
        var mode = target.getAttribute('data-preview') || 'text';
        var empty = target.getAttribute('data-empty') || '';

        if (mode === 'badge') {
            var on = src.checked;
            target.textContent = target.getAttribute(on ? 'data-on' : 'data-off') || '';
            target.classList.toggle('status-running', on);
            target.classList.toggle('status-stopped', !on);
            return;
        }

        var value = mode === 'option' ? textForField(src) : textForField(src);
        if (value) {
            target.textContent = value;
            target.classList.remove('preview-empty');
        } else {
            target.textContent = empty;
            target.classList.add('preview-empty');
        }
    }

    function initPreview(form) {
        var targets = form.querySelectorAll('[data-preview-for]');
        if (!targets.length) { return; }
        function refresh() { targets.forEach(renderTarget); }
        form.addEventListener('input', refresh);
        form.addEventListener('change', refresh);
        refresh(); // initial paint (e.g. Edit pages with existing values)
    }

    // ---- Submit guard (prevent duplicate submissions) -----------------------
    function initSubmitGuard(form) {
        form.addEventListener('submit', function () {
            // Skip the busy state when client-side validation would block the submit,
            // otherwise the button would spin forever on an invalid form.
            var $ = window.jQuery;
            if ($ && $.fn && $(form).data('validator') && !$(form).valid()) {
                return;
            }
            var btn = form.querySelector('[data-submit-btn]') || form.querySelector('button[type="submit"]');
            if (btn) {
                btn.classList.add('is-busy');
                btn.setAttribute('aria-busy', 'true');
            }
        });
    }

    function init() {
        document.querySelectorAll('form[data-live-form]').forEach(function (form) {
            initPreview(form);
            initSubmitGuard(form);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
