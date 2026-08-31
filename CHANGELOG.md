# Changelog

## 3.0.0 — 2026-08-31

RETH Optimicer. Misma base que Cchay Optimicer, rebrand y endurecimiento.

### Marca
- Identidad RETH Labs: ave cyan `#7ED4F5`, wordmark RETH, UI oscura.
- Producto: RETH Optimicer para Windows y Linux.

### Windows
- Un clic ya no desactiva Bluetooth, Xbox, OneDrive ni cambia el DNS.
- QoS sin límite a 0 bps.
- Anti-stutter apagado por defecto.
- DISM sin `/ResetBase`.
- Puntos de restauración sin `vssadmin delete shadows /all`.
- Desinstalador con botón explícito.
- Escáner heurístico: no borra archivos.
- Migración de datos desde `%LocalAppData%\CchayOptimicer`.

### Linux
- Cliente GTK 4 + libadwaita.
- `install.sh` / `uninstall.sh`.
- Helper privilegiado con lista blanca (`pkexec`).

### Web
- Landing de producto RETH Optimicer.

## 2.0.1

Cchay Optimicer: módulos extra, WiX, tests.
