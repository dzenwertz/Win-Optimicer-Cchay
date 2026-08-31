# Instaladores de RETH Optimicer

## Windows (Inno Setup)

1. Publicar la app:

```powershell
dotnet publish ..\cchay-optimicer-cs\cchay-optimicer-cs.csproj -c Release -r win-x64 --self-contained true -o ..\publish
```

2. Compilar `installer.iss` con Inno Setup 6. Salida:

`../installer_output/RETH_Optimicer_Setup_v3.0.0.exe`

El instalador pide administrador, copia archivos, crea accesos directos y puede lanzar la app al terminar.

## Windows (asistente embebido)

`installer_project` genera un `.exe` que extrae `RethOptimicer.exe` a `%LocalAppData%\Programs\RETH Optimicer`.

## Linux

Ver `linux/install.sh` en la raíz del repositorio.
