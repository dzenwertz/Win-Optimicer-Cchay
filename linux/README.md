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

## Requisitos

- Python 3.10+
- GTK 4
- libadwaita
- `pkexec` para acciones privilegiadas

En Arch/EndeavourOS:

```bash
sudo pacman -S python gtk4 libadwaita polkit
```

## Desinstalar

```bash
./uninstall.sh
```
