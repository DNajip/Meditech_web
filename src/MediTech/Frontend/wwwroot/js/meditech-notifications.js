/**
 * MediTech Premium Notification System
 * =====================================
 * MediToast  — Non-intrusive floating notifications
 * MediConfirm — Promise-based confirmation dialogs
 *
 * Usage:
 *   MediToast.success('Guardado exitosamente');
 *   MediToast.error('Error al conectar con el servidor');
 *   MediToast.warning('Stock insuficiente');
 *   MediToast.info('Recuerde completar el formulario');
 *
 *   MediConfirm.show({
 *       title: '¿Cancelar esta cita?',
 *       message: 'Esta acción no se puede deshacer.',
 *       variant: 'danger',            // danger | warning | success | primary
 *       confirmText: 'Sí, cancelar',
 *       cancelText: 'No, volver',
 *       icon: 'bi-exclamation-triangle-fill'
 *   }).then(confirmed => {
 *       if (confirmed) { ... }
 *   });
 */

// ============================================
// MediToast
// ============================================
const MediToast = (() => {
    const ICONS = {
        success: 'fa-check-circle',
        error:   'fa-times-circle',
        warning: 'fa-exclamation-triangle',
        info:    'fa-info-circle'
    };

    const TITLES = {
        success: 'Éxito',
        error:   'Error',
        warning: 'Atención',
        info:    'Información'
    };

    const DURATIONS = {
        success: 3500,
        error:   6000,
        warning: 5000,
        info:    4000
    };

    function getContainer() {
        let container = document.getElementById('meditoast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'meditoast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    function show(message, type = 'info', options = {}) {
        const container = getContainer();
        const duration = options.duration || DURATIONS[type];
        const title = options.title || TITLES[type];
        const icon = options.icon || ICONS[type];

        const toast = document.createElement('div');
        toast.className = `meditoast meditoast--${type}`;
        toast.innerHTML = `
            <div class="meditoast-icon">
                <i class="fas ${icon}"></i>
            </div>
            <div class="meditoast-content">
                <div class="meditoast-title">${title}</div>
                <div class="meditoast-message">${message}</div>
            </div>
            <button class="meditoast-close" aria-label="Cerrar">
                <i class="fas fa-times"></i>
            </button>
            <div class="meditoast-progress" style="animation-duration: ${duration}ms;"></div>
        `;

        // Close on click
        const closeBtn = toast.querySelector('.meditoast-close');
        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            dismiss(toast);
        });

        // Click anywhere on toast to dismiss
        toast.addEventListener('click', () => dismiss(toast));

        container.appendChild(toast);

        // Auto-dismiss
        const timer = setTimeout(() => dismiss(toast), duration);

        // Pause on hover
        toast.addEventListener('mouseenter', () => clearTimeout(timer));
        toast.addEventListener('mouseleave', () => {
            // We won't restart the timer on leave to keep it simple;
            // the progress bar already paused visually via CSS.
            setTimeout(() => dismiss(toast), 2000);
        });

        // Limit max visible toasts
        const toasts = container.querySelectorAll('.meditoast:not(.removing)');
        if (toasts.length > 5) {
            dismiss(toasts[0]);
        }
    }

    function dismiss(toast) {
        if (toast.classList.contains('removing')) return;
        toast.classList.add('removing');
        toast.addEventListener('animationend', () => toast.remove());
    }

    return {
        success: (msg, opts) => show(msg, 'success', opts || {}),
        error:   (msg, opts) => show(msg, 'error', opts || {}),
        warning: (msg, opts) => show(msg, 'warning', opts || {}),
        info:    (msg, opts) => show(msg, 'info', opts || {})
    };
})();


// ============================================
// MediConfirm
// ============================================
const MediConfirm = (() => {
    const VARIANT_ICONS = {
        danger:  'fa-exclamation-triangle',
        warning: 'fa-question-circle',
        success: 'fa-check-circle',
        primary: 'fa-question-circle'
    };

    /**
     * Show a confirmation dialog.
     * @param {Object} options
     * @param {string} options.title        - Dialog title
     * @param {string} options.message      - Dialog body message
     * @param {string} [options.variant]    - 'danger' | 'warning' | 'success' | 'primary'
     * @param {string} [options.confirmText] - Text for the confirm button
     * @param {string} [options.cancelText]  - Text for the cancel button
     * @param {string} [options.icon]        - Bootstrap icon class override
     * @returns {Promise<boolean>}
     */
    function show(options = {}) {
        const {
            title       = '¿Está seguro?',
            message     = 'Esta acción no se puede deshacer.',
            variant     = 'warning',
            confirmText = 'Confirmar',
            cancelText  = 'Cancelar',
            icon        = VARIANT_ICONS[variant] || VARIANT_ICONS.warning
        } = options;

        return new Promise((resolve) => {
            const overlay = document.createElement('div');
            overlay.className = `mediconfirm-overlay mediconfirm--${variant}`;
            overlay.innerHTML = `
                <div class="mediconfirm-dialog">
                    <div class="mediconfirm-icon-wrap">
                        <i class="fas ${icon}"></i>
                    </div>
                    <div class="mediconfirm-title">${title}</div>
                    <div class="mediconfirm-message">${message}</div>
                    <div class="mediconfirm-actions">
                        <button class="mediconfirm-btn-cancel">${cancelText}</button>
                        <button class="mediconfirm-btn-confirm">${confirmText}</button>
                    </div>
                </div>
            `;

            const close = (result) => {
                overlay.classList.add('removing');
                overlay.addEventListener('animationend', () => {
                    overlay.remove();
                    resolve(result);
                });
            };

            // Cancel button
            overlay.querySelector('.mediconfirm-btn-cancel').addEventListener('click', () => close(false));

            // Confirm button
            overlay.querySelector('.mediconfirm-btn-confirm').addEventListener('click', () => close(true));

            // Click overlay backdrop to cancel
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) close(false);
            });

            // Escape key to cancel
            const onKey = (e) => {
                if (e.key === 'Escape') {
                    document.removeEventListener('keydown', onKey);
                    close(false);
                }
            };
            document.addEventListener('keydown', onKey);

            document.body.appendChild(overlay);

            // Focus the confirm button for accessibility
            setTimeout(() => {
                overlay.querySelector('.mediconfirm-btn-confirm')?.focus();
            }, 100);
        });
    }

    return { show };
})();
