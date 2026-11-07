# Revisión de calidad — 2026-09-09

## Cambios comprobados

- Windows: mantenimiento rápido unificado, limitado a temporales, miniaturas y reportes. No vacía la papelera ni aplica ajustes de RAM, TCP, registro o energía.
- Windows: limpieza sin recorrer puntos de reanálisis; los bytes se contabilizan después de eliminar cada archivo. Corregida la interfaz que sobrescribía el tamaño antes de sumarlo.
- Windows: comprobación del resultado de papelera, DISM y optimización de unidades; confirmación explícita para limpieza manual y presentación de errores.
- Windows: ventana y navegación más compactas; corrección de prueba de iconos que interpretaba una URI como nombre de símbolo.
- Linux: corregido `stat.S_ISREG`, protección de rutas con enlaces simbólicos y conteo conservador de borrados.
- Linux: los fallos de pasos privilegiados afectan al resultado del mantenimiento; excepciones por paso se registran sin abortar los demás.
- Linux: DNS comprueba los comandos y ya no sobrescribe `/etc/resolv.conf` como alternativa. Validación de JSON y rangos sysctl.
- Linux: purga de caché RAM y TRIM desactivados por defecto para nuevas configuraciones. Las preferencias existentes se conservan.

## Validación

- Compilación Windows Release: 0 errores, 0 advertencias.
- Windows: 5 pruebas seleccionadas correctas (dos regresiones aisladas, perfiles, iconos y lectura de memoria).
- Linux: 6 regresiones correctas en Python 3.12 sobre Windows; una prueba de enlaces omitida por permisos del host.
- Sintaxis Python comprobada con compileall.
- Herramientas locales de validación en `.tools/`, excluidas de Git.

## Límites de esta revisión

No se ejecutaron limpiezas sobre datos reales ni comandos de optimización privilegiados. No se validó visualmente WPF/GTK ni se ejecutó la suite dependiente de `/proc` en Linux. Falta comprobar instalación, desinstalación, restauración, servicios, ajustes y operaciones de mantenimiento en máquinas virtuales Windows y Linux antes de publicar. No se afirma una auditoría exhaustiva ni ausencia total de errores.

Se conservaron los cambios Linux que ya existían al iniciar la revisión.
