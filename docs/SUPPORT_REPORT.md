# Reporte de diagnóstico para soporte TI

El botón **Exportar Reporte** del panel Inicio guarda una lectura de los datos que ya cargó el panel. Generar el texto no limpia archivos, no cambia ajustes ni ejecuta el escáner de procesos.

## Flujo de atención

1. Identifica el síntoma y pide permiso para revisar el equipo.
2. Revisa los datos del panel: sistema operativo, memoria, almacenamiento y lecturas disponibles.
3. Exporta el texto desde **Exportar Reporte**. El nombre propuesto es `Reporte_RETH_yyyyMMdd.txt`.
4. Revisa el contenido antes de adjuntarlo a un ticket. Añade el síntoma y las comprobaciones realizadas; el reporte no sustituye ese diagnóstico.
5. Comprueba el resultado después de cualquier intervención y documenta qué cambió.

## Datos incluidos y omitidos

El reporte conserva SO/build, modelo de CPU/GPU, núcleos/hilos, memoria, disco C:, temperatura, SMART, uptime y estado de ejecución como administrador. Omite los campos de usuario, IP local e IP pública. El nombre sugerido no contiene el nombre del equipo.

El puntaje es una estimación orientativa del uso de recursos y algunas lecturas; no certifica la salud del hardware ni detecta todas las causas de una incidencia. Una lectura desconocida sigue apareciendo como desconocida.

Este cambio reduce identificadores habituales, pero no garantiza anonimización completa: los datos técnicos aún pueden describir un equipo concreto. No adjuntes registros de clientes ni capturas con información personal a un repositorio público.

El panel sigue recopilando sus datos con el comportamiento existente, incluida una consulta de IP pública. La exportación no realiza esa consulta por sí misma. Este cambio no crea un modo offline ni modifica la recopilación del panel.

## Verificación reproducible

En Windows con SDK .NET 8:

```powershell
dotnet test cchay-optimicer-cs-tests/cchay-optimicer-cs-tests.csproj --filter FullyQualifiedName~SystemReportTests
```

Estas pruebas usan datos ficticios; no consultan el equipo, no hacen peticiones de red y no ejecutan limpieza. Comprueban que se omitan usuario/IP, se conserven datos técnicos, se expliquen los límites del puntaje y no se modifique el objeto de entrada.

La integración continua ejecuta este grupo de pruebas de forma explícita. No se ejecuta la suite completa de servicios sobre el equipo del desarrollador, porque contiene pruebas de acciones del sistema ajenas al reporte.

La funcionalidad corresponde al código de esta revisión; no se afirma que esté incluida en el portable v3.0.0 ya publicado.
