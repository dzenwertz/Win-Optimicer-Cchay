// ==========================================
// CCHAY WEB SIMULATOR CONTROLLER (app.js)
// ==========================================

document.addEventListener('DOMContentLoaded', () => {

    // --- 1. SIDEBAR PAGE ROUTING ---
    const sidebarItems = document.querySelectorAll('.sidebar-item');
    const appPages = document.querySelectorAll('.app-page');

    sidebarItems.forEach(item => {
        item.addEventListener('click', () => {
            // Remove active classes
            sidebarItems.forEach(i => i.classList.remove('active'));
            appPages.forEach(p => p.classList.remove('active'));

            // Set active class
            item.classList.add('active');
            const targetPage = item.getAttribute('data-target');
            document.getElementById(targetPage).classList.add('active');
        });
    });

    // --- 2. SIMULATION CONTROLLERS ---
    const btnCleanRam = document.getElementById('btn-clean-ram');
    const ramConsoleContent = document.getElementById('ram-console-content');
    const ramGaugeVal = document.getElementById('ram-gauge-val');
    const simRamTxt = document.getElementById('sim-ram-txt');
    const simRamProgress = document.getElementById('sim-ram-progress');

    const btnCleanDisk = document.getElementById('btn-clean-disk');
    const diskConsoleContent = document.getElementById('disk-console-content');

    const btnQuickClean = document.getElementById('btn-quick-clean');

    let ramCleaned = false;
    let diskCleaned = false;

    // --- RAM CLEANING ANIMATION ---
    async function simulateRamClean() {
        if (ramCleaned) {
            ramConsoleContent.innerHTML = "[Info] La memoria RAM ya se encuentra en niveles optimizados.\nNo se requiere limpieza adicional.";
            return;
        }

        btnCleanRam.disabled = true;
        btnCleanRam.innerText = "Limpiando...";
        ramConsoleContent.innerHTML = "";

        const logLines = [
            "[1/5] Vaciando conjuntos de trabajo (Working Sets)...",
            "[2/5] Purgando Standby List (Lista de Espera)...",
            "[3/5] Liberando System File Cache...",
            "[4/5] Liberando Modified Page List...",
            "[5/5] Combinando páginas redundantes...",
            "[EXITOSO] ¡Optimización de RAM finalizada con éxito!"
        ];

        // Output logs sequentially
        for (let i = 0; i < logLines.length; i++) {
            ramConsoleContent.innerHTML += logLines[i] + "\n";
            // Scroll down console
            document.getElementById('ram-console').scrollTop = document.getElementById('ram-console').scrollHeight;
            await new Promise(resolve => setTimeout(resolve, 400));
        }

        // Animate gauge down
        let currentPercent = 36;
        const targetPercent = 14;
        
        const interval = setInterval(() => {
            if (currentPercent > targetPercent) {
                currentPercent--;
                ramGaugeVal.innerText = `${currentPercent}%`;
                // Update Dashboard values
                const currentGB = (16.0 * (currentPercent / 100)).toFixed(1);
                simRamTxt.innerText = `${currentGB} GB / 16.0 GB`;
                simRamProgress.style.width = `${currentPercent}%`;
            } else {
                clearInterval(interval);
                btnCleanRam.disabled = false;
                btnCleanRam.innerText = "RAM Optimizada ✓";
                btnCleanRam.style.backgroundColor = "#2b8a3e";
                ramConsoleContent.innerHTML += `\n>> ¡Se liberaron 3,520 MB de memoria RAM!`;
                ramCleaned = true;
            }
        }, 30);
    }

    // --- DISK CLEANING ANIMATION ---
    async function simulateDiskClean() {
        if (diskCleaned) {
            diskConsoleContent.innerHTML = "[Info] La caché del disco y archivos temporales ya han sido eliminados.";
            return;
        }

        btnCleanDisk.disabled = true;
        btnCleanDisk.innerText = "Borrando...";
        diskConsoleContent.innerHTML = "";

        const logLines = [
            "[Escanéo] Localizando archivos obsoletos...",
            "[Borrando] Carpetas temporales de Windows Temp... (180 MB)",
            "[Borrando] Caché residual de Chrome & Edge... (740 MB)",
            "[Borrando] Papelera de Reciclaje de Windows... (512 MB)",
            "[EXITOSO] Borrado completo finalizado."
        ];

        for (let i = 0; i < logLines.length; i++) {
            diskConsoleContent.innerHTML += logLines[i] + "\n";
            await new Promise(resolve => setTimeout(resolve, 500));
        }

        btnCleanDisk.disabled = false;
        btnCleanDisk.innerText = "Disco Limpio ✓";
        btnCleanDisk.style.backgroundColor = "#2b8a3e";
        diskConsoleContent.innerHTML += `\n>> ¡Se liberaron 1.43 GB de espacio en disco!`;
        diskCleaned = true;
    }

    // --- QUICK CLEAN ANIMATION (DASHBOARD) ---
    async function simulateQuickClean() {
        btnQuickClean.disabled = true;
        btnQuickClean.innerText = "Optimizando...";

        // Navigate to RAM, run simulation, navigate to Disk, run simulation
        await new Promise(resolve => setTimeout(resolve, 300));
        
        // Go to RAM page
        document.querySelector('[data-target="page-ram"]').click();
        await new Promise(resolve => setTimeout(resolve, 300));
        await simulateRamClean();

        // Go to Disk page
        document.querySelector('[data-target="page-disk"]').click();
        await new Promise(resolve => setTimeout(resolve, 300));
        await simulateDiskClean();

        // Return to Dashboard page
        document.querySelector('[data-target="page-dashboard"]').click();
        
        btnQuickClean.innerText = "¡PC Optimizada!";
        btnQuickClean.style.backgroundColor = "#2b8a3e";
        
        // Update dashboard score and health text
        document.querySelector('.score-number').innerText = "100";
        document.querySelector('.health-info h4').innerText = "Salud del Sistema: ¡Máxima!";
        document.querySelector('.health-info p').innerText = "Todas las optimizaciones del simulador web han finalizado con éxito.";
    }

    // --- Attach event listeners ---
    btnCleanRam.addEventListener('click', simulateRamClean);
    btnCleanDisk.addEventListener('click', simulateDiskClean);
    btnQuickClean.addEventListener('click', simulateQuickClean);
});
