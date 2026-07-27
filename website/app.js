// ==========================================================================
// JOSEPH ICV PORTFOLIO CONTROLLER (app.js)
// ==========================================================================

document.addEventListener('DOMContentLoaded', () => {

    // --- 1. SECTIONS & TAB NAVIGATION (HASH-BASED) ---
    const sidebarItems = document.querySelectorAll('.sidebar-item');
    const sections = document.querySelectorAll('.content-section');
    const sidebar = document.getElementById('sidebar');

    // Function to navigate to a specific section
    function navigateToSection(sectionId) {
        let sectionExists = false;
        
        sections.forEach(section => {
            if (section.id === sectionId) {
                section.classList.add('active');
                sectionExists = true;
            } else {
                section.classList.remove('active');
            }
        });

        // If the section doesn't exist, default to 'inicio'
        if (!sectionExists) {
            navigateToSection('inicio');
            return;
        }

        // Update active class in sidebar items
        sidebarItems.forEach(item => {
            if (item.getAttribute('data-section') === sectionId) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });

        // Scroll content pane back to top on navigation
        window.scrollTo({ top: 0, behavior: 'smooth' });
        const contentPane = document.getElementById('content-pane');
        if (contentPane) {
            contentPane.scrollTop = 0;
        }
    }

    // Navigation item click handlers
    sidebarItems.forEach(item => {
        item.addEventListener('click', () => {
            const targetSection = item.getAttribute('data-section');
            window.location.hash = targetSection;
            
            // Close mobile sidebar on item click
            if (sidebar.classList.contains('open')) {
                sidebar.classList.remove('open');
            }
        });
    });

    // Handle hash change events
    window.addEventListener('hashchange', () => {
        const hash = window.location.hash.substring(1); // Remove '#'
        if (hash) {
            navigateToSection(hash);
        }
    });

    // Initial navigation based on URL hash or default
    const initialHash = window.location.hash.substring(1);
    navigateToSection(initialHash || 'inicio');


    // --- 2. PREMIUM LIGHT/DARK THEME SWITCHER ---
    const themeToggleBtn = document.getElementById('theme-toggle');

    // Function to apply theme attribute to document root
    function applyTheme(themeName) {
        document.documentElement.setAttribute('data-theme', themeName);
        localStorage.setItem('theme', themeName);
    }

    // Determine starting theme (Default to light)
    const savedTheme = localStorage.getItem('theme');
    
    if (savedTheme) {
        applyTheme(savedTheme);
    } else {
        // Always default to light theme
        applyTheme('light');
    }

    // Toggle theme button listener
    themeToggleBtn.addEventListener('click', () => {
        const currentTheme = document.documentElement.getAttribute('data-theme');
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        applyTheme(newTheme);
    });


    // --- 3. MOBILE SIDEBAR TOGGLE ---
    const menuToggle = document.getElementById('menu-toggle');

    if (menuToggle) {
        menuToggle.addEventListener('click', (e) => {
            e.stopPropagation(); // Avoid immediately triggering document click
            sidebar.classList.toggle('open');
        });
    }

    // Close sidebar when clicking outside on mobile devices
    document.addEventListener('click', (e) => {
        const width = window.innerWidth;
        if (width <= 992 && sidebar.classList.contains('open')) {
            // If click is not on sidebar and not on the menu button
            if (!sidebar.contains(e.target) && !menuToggle.contains(e.target)) {
                sidebar.classList.remove('open');
            }
        }
    });

    // --- 4. COPY COMMAND TO CLIPBOARD ---
    const copyBtn = document.getElementById('copy-command-btn');
    const copyLabel = document.getElementById('copy-label');
    const commandText = document.getElementById('command-to-copy');

    if (copyBtn && commandText && copyLabel) {
        copyBtn.addEventListener('click', async () => {
            try {
                await navigator.clipboard.writeText(commandText.innerText);
                const originalText = copyLabel.innerText;
                copyLabel.innerText = "¡Copiado al portapapeles!";
                copyLabel.style.color = "var(--color-primary)";
                
                setTimeout(() => {
                    copyLabel.innerText = originalText;
                    copyLabel.style.color = "";
                }, 2000);
            } catch (err) {
                console.error('Failed to copy command: ', err);
            }
        });
    }
});
