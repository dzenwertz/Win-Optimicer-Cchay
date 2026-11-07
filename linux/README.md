# RETH Optimicer para Linux

Cliente GTK 4 + libadwaita. Acciones de root solo a través de `pkexec` y un helper con lista blanca.

## Instalar

Desde el repositorio:

```bash
git clone https://github.com/dzenwertz/Win-Optimicer-Cchay.git
cd Win-Optimicer-Cchay/linux
chmod +x install.sh
./install.sh
reth-optimicer
```

O desde un Release: extrae `RETH-Optimicer-Linux-*.tar.gz` y ejecuta `linux/install.sh`.

Sin root instala en `~/.local`. Con root, en `/usr/local` (incluye política polkit).

## RAM (lo más útil)

En el inicio verás **RAM disponible**, no “libre”. Linux llena la RAM vacía con caché; eso es normal.

1. **Liberar RAM** — reclama caché del kernel (pide contraseña).
2. **Qué está usando la RAM** — lista agrupada. Cerrar Brave/Chrome suele devolver más que cualquier tweak.
3. Si sigue justa, cierra pestañas. En 8 GB un navegador con muchas pestañas se come el equipo.

## Requisitos

- Python 3.10+
- GTK 4
- libadwaita 1.4 o superior (Ubuntu 24.04, Fedora 39, Debian 13 o más recientes)
- `pkexec` para acciones privilegiadas

En Arch/EndeavourOS:

```bash
sudo pacman -S python gtk4 libadwaita polkit
```

## Desinstalar

```bash
./uninstall.sh
```
