# Changelog

## 3.1.0 — 2026-11-07

Pulido de producto: cambios reversibles, permisos explicados e interfaz Linux nueva.

### Windows
- Punto de restauración completo (`BEGIN` + `END_SYSTEM_CHANGE`) y el registro queda como estaba.
- Si no se puede crear el punto de restauración (Protección del sistema desactivada), se pregunta antes de aplicar el cambio.
- Un ajuste solo se marca como aplicado si `netsh`/`powercfg` funcionaron; si no, se muestra el comando que falló.
- La app ya no exige administrador al abrirse: explica para qué lo necesita y, sin él, funciona en modo diagnóstico.
- Modo Game con instantánea y restauración de los valores originales.
- Reporte de soporte sin usuario ni direcciones IP.
- Enlaces del programa e instalador apuntan al repositorio correcto.

### Linux
- Interfaz nueva y minimalista con listas agrupadas de libadwaita; se adapta a ventanas estrechas.
- Botón «Restaurar» para sysctl, gobernador de CPU y privacidad (se guarda el valor original).
- El helper con privilegios solo se ejecuta si es propiedad de root (antes podía usarse una copia modificable por el usuario).
- Swappiness recomendado correcto con zram (180) y DNS sin líneas de comentario.
- Las pantallas cargan en segundo plano: Red ya no bloquea el arranque; Memoria no parpadea.
- Requiere libadwaita 1.4 o superior.

### Pendiente
- El código Windows de esta versión se valida en el CI de GitHub; falta probarlo en un Windows 11 limpio.

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
