# RETH Optimicer

Optimizador todo en uno para **Windows** y **Linux**. Producto de RETH Labs: sin publicidad, sin telemetría oculta, código auditable.

Versión **3.0.0** · Licencia [GPL-3.0](LICENSE) · Repositorio: [dzenwertz/Win-Optimicer-Cchay](https://github.com/dzenwertz/Win-Optimicer-Cchay)

## Descargar

Instaladores y paquetes en **[Releases](https://github.com/dzenwertz/Win-Optimicer-Cchay/releases)**:

| Archivo | Para |
|---|---|
| `RETH-Optimicer-Windows-portable-v3.0.0.zip` | Windows 10/11 (x64), ejecutable autónomo |
| `RETH-Optimicer-Linux-v3.0.0.tar.gz` | Linux (GTK 4 + libadwaita) |
| `RETH-Optimicer-Website-v3.0.0.zip` | Landing del producto |
| `SHA256SUMS.txt` | Verificación de integridad |

Cada tag `vX.Y.Z` dispara el workflow de Release y publica esos archivos.

## Qué hace

- Diagnóstico de CPU, RAM, disco y red
- Limpieza de memoria y temporales
- Ajustes de rendimiento y privacidad con confirmación
- Programas de inicio y servicios
- DNS, respaldo y reparación básica
- Un clic para mantenimiento **seguro** (sin desactivar Bluetooth, Xbox ni OneDrive)

## Estructura del repositorio

```
cchay-optimicer-cs/        App Windows (WPF, .NET 8)
cchay-optimicer-cs-tests/  Tests Windows
linux/                     App Linux (Python + GTK 4)
installer/                 Inno Setup y scripts Windows
installer_project/         Asistente de instalación embebido
website/                   Landing RETH Optimicer
.github/workflows/         CI y Releases
```

## Windows

Requisitos: Windows 10 (build 19041+) o 11, administrador.

Portable (Release):

1. Baja el zip `RETH-Optimicer-Windows-portable-*.zip`
2. Extrae y ejecuta `RethOptimicer.exe`

Compilar:

```bat
dotnet publish cchay-optimicer-cs\cchay-optimicer-cs.csproj -c Release -r win-x64 --self-contained true -o publish
```

Instalador Inno Setup: `installer/installer.iss` → `RETH_Optimicer_Setup_v3.0.0.exe`

## Linux

Requisitos: Python 3.10+, GTK 4, libadwaita, `pkexec` para acciones de root.

```bash
git clone https://github.com/dzenwertz/Win-Optimicer-Cchay.git
cd Win-Optimicer-Cchay/linux
chmod +x install.sh
./install.sh
reth-optimicer
```

Arch / EndeavourOS:

```bash
sudo pacman -S python gtk4 libadwaita polkit
```

## Pruebas

```bash
python3 -m unittest linux.tests.test_modules
```

```bat
dotnet test cchay-optimicer-cs-tests
```

## Marca

Ave RETH + wordmark. Acento `#7ED4F5`, fondo `#07090B`.
