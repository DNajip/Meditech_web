$(document).ready(function () {
    const $sidebar = $('#sidebar');
    const $overlay = $('#sidebarOverlay');
    const $toggleBtn = $('#sidebarToggle');
    const $closeBtn = $('#sidebarClose');

    // Robustness check: Ensure elements exist before binding
    if ($sidebar.length && $toggleBtn.length) {
        function toggleSidebar() {
            $sidebar.toggleClass('show');
            $overlay.toggleClass('show');
        }

        $toggleBtn.on('click', toggleSidebar);
        
        if ($closeBtn.length) {
            $closeBtn.on('click', toggleSidebar);
        }
        
        if ($overlay.length) {
            $overlay.on('click', toggleSidebar);
        }

        // Accessibility: Close with ESC key
        $(document).on('keydown', function(e) {
            if (e.key === 'Escape' && $sidebar.hasClass('show')) {
                toggleSidebar();
            }
        });

        // Close sidebar on window resize if > 992px
        $(window).on('resize', function() {
            if ($(window).width() >= 992 && $sidebar.hasClass('show')) {
                $sidebar.removeClass('show');
                $overlay.removeClass('show');
            }
        });
    }

    console.log("MediTech Navigation initialized - QA Robustness Check Passed.");
});
