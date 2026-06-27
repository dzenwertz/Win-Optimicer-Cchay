# Win-Optimicer-Cchay

Herramienta de optimizacion de rendimiento para Windows desarrollada en C# con WPF y WPF-UI.

## Creador
Desarrollado por [dzenwertz](https://github.com/dzenwertz).

## Caracteristicas
* **Optimizador de RAM:** Limpieza de Working Set, Standby List, System File Cache, Modified Page List y Combined Page List.
* **Limpiador de Disco:** Borrado de archivos temporales, logs de Windows, componentes antiguos de Windows Update (DISM), y cache de navegadores (Chrome, Edge, Firefox, Brave).
* **Ajustes de Sistema (Tweaks):** Configuraciones para mejorar prioridad de CPU, latencia de red TCP/IP, perfil de juegos, y desactivacion de telemetria, Cortana y Copilot.
* **Eliminador de Bloatware:** Desinstalacion profunda de aplicaciones preinstaladas (Xbox, MSN Clima, Mapas, OneDrive, Widgets).
* **Optimizador de Red:** Configuracion rapida de servidores DNS (Cloudflare, Google, OpenDNS, AdGuard, Quad9) con medicion de ping integrada.
* **Programas de Inicio:** Administrador para habilitar o deshabilitar programas que inician con el sistema.
* **Copia de Seguridad:** Creacion y gestion de Puntos de Restauracion del Sistema nativos con opcion de creacion automatica antes de aplicar cambios.

## Requisitos
* Windows 10 (Build 19041 o superior) o Windows 11.
* .NET 8.0 SDK.
* Privilegios de Administrador (requerido para modificaciones de registro y servicios del sistema).

## Compilacion y Ejecucion
1. Restaurar dependencias:
   ```bash
   dotnet restore
   ```
2. Compilar el proyecto:
   ```bash
   dotnet build -c Release
   ```
3. Ejecutar:
   ```bash
   dotnet run --project cchay-optimicer-cs/cchay-optimicer-cs.csproj
   ```
