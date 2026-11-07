# RETH Optimicer · Entrega para la web de RETH

## Descripción

RETH Optimicer es la herramienta de diagnóstico y mantenimiento de RETH Tecnologías para equipos Windows y Linux. Muestra en una sola pantalla cómo están la memoria, el disco y el procesador, y ofrece un mantenimiento seguro que no toca tus archivos personales. Cada cambio en el sistema se puede deshacer: en Windows crea un punto de restauración antes de aplicarlo y avisa si no puede, y en Linux guarda el valor original para restaurarlo con un clic. Solo pide permisos de administrador después de explicar para qué los usa.

## Características clave

1. **Diagnóstico claro:** memoria disponible, disco libre, uso de CPU y qué aplicaciones consumen más.
2. **Cambios reversibles:** punto de restauración en Windows y botón "Restaurar" en Linux.
3. **Mantenimiento seguro:** limpia caché, miniaturas y registros antiguos sin tocar documentos ni la papelera.
4. **Control del sistema:** servicios, inicio automático, DNS con latencia medida y privacidad.

## Stack técnico

Windows: C# · .NET 8 · WPF (WPF-UI) — Linux: Python 3 · GTK 4 · libadwaita · polkit.

## Versión, licencia y descarga

- **Versión publicada:** v3.0.0 — https://github.com/dzenwertz/Win-Optimicer-Cchay/releases/tag/v3.0.0
- **Licencia:** GPL-3.0
- Las mejoras de la rama `pulido-reth` (interfaz nueva, Restaurar, permisos) **aún no están en ninguna release**.

## Capturas (`capturas/`)

Capturas reales de la app de Linux con **datos de demostración** (procesos, servicios, DNS, autostart y equipo ficticios; los valores de Rendimiento son del kernel de pruebas y no contienen datos personales). Tamaño 1100×720.

| Archivo | Pantalla |
| --- | --- |
| `01-inicio.png` | Inicio: estado, indicadores y mantenimiento |
| `02-memoria.png` | Memoria: qué aplicaciones usan la RAM |
| `03-rendimiento-restaurar.png` | Rendimiento con "Óptimo", "Aplicar" y "Restaurar" |
| `04-servicios.png` | Servicios con interruptores |
| `05-red-dns.png` | DNS con latencia medida |

## Pendiente (no verificado)

- Capturas de Windows: no hay VM disponible; la interfaz de Windows no se revisó visualmente.
- El código Windows de `pulido-reth` no se compiló (falta de RAM en el equipo de desarrollo); revisado a mano.
- Versión inconsistente: Windows y README dicen 3.0.0; Linux (`__init__.py`) dice 3.0.1. Definir el número de la próxima versión.
