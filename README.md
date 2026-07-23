# Win-Optimicer-Cchay

Herramienta gratuita y de codigo abierto para la optimizacion de rendimiento en sistemas operativos Windows, desarrollada en C# utilizando WPF y WPF-UI.

## Vision del Proyecto
Win-Optimicer-Cchay ha sido creada como una contribucion de codigo abierto para la comunidad, ofreciendo diagnostico de hardware, mantenimiento profundo del sistema y ajuste de parametros de rendimiento sin publicidad ni software no deseado.

## Caracteristicas Tecnicas

- **Optimizador de Memoria RAM:** Limpieza de Working Set, Standby List, System File Cache, Modified Page List y Combined Page List.
- **Limpieza de Almacenamiento:** Eliminacion de archivos temporales, registros de auditoria, componentes obsoletos de Windows Update (DISM) y cache de navegadores (Chrome, Edge, Firefox, Brave).
- **Ajustes del Sistema (Tweaks):** Optimizacion de prioridad de CPU, latencia de red TCP/IP, perfiles de rendimiento para juegos y desactivacion de servicios redundantes de telemetria.
- **Gestion de Bloatware:** Desinstalacion profunda de aplicaciones nativas innecesarias.
- **Optimizacion de Red:** Configuracion rapida de servidores DNS (Cloudflare, Google, OpenDNS, AdGuard, Quad9) con verificacion de latencia en tiempo real.
- **Administrador de Inicio:** Control de programas de arranque automatico con el sistema operativo.
- **Respaldo:** Creacion y gestion de Puntos de Restauracion del Sistema nativos.

## Requisitos del Sistema

- Windows 10 (Build 19041 o superior) o Windows 11.
- .NET 8.0 Runtime / SDK.
- Privilegios de Administrador.

## Compilacion e Instalacion

1. Clonar el repositorio:
   `ash
   git clone https://github.com/dzenwertz/Win-Optimicer-Cchay.git
   `
2. Restaurar dependencias:
   `ash
   dotnet restore
   `
3. Compilar en modo Release:
   `ash
   dotnet build -c Release
   `

## Licencia
Este proyecto se distribuye como software libre y gratuito para la comunidad bajo la licencia MIT.
