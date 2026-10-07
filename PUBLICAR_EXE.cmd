@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_COMMAND=dotnet"
if exist ".tools\dotnet\dotnet.exe" set "DOTNET_COMMAND=.tools\dotnet\dotnet.exe"
set "OUTPUT_DIR=%~dp0dist\SentiaGlobal"

echo Publicando Sentia Global para Windows...
%DOTNET_COMMAND% publish src\Web\Web.csproj -p:PublishProfile=WindowsExe

if errorlevel 1 (
  echo.
  echo No se pudo crear el ejecutable.
  pause
  exit /b 1
)

rem Nunca distribuir datos ni credenciales creados en este equipo.
if exist "%OUTPUT_DIR%\AnalisisSentimiento.db" del /q "%OUTPUT_DIR%\AnalisisSentimiento.db"
if exist "%OUTPUT_DIR%\AnalisisSentimiento.db-shm" del /q "%OUTPUT_DIR%\AnalisisSentimiento.db-shm"
if exist "%OUTPUT_DIR%\AnalisisSentimiento.db-wal" del /q "%OUTPUT_DIR%\AnalisisSentimiento.db-wal"
if exist "%OUTPUT_DIR%\.keys" rmdir /s /q "%OUTPUT_DIR%\.keys"
del /q "%OUTPUT_DIR%\*.pdb" 2>nul

echo.
echo Listo: dist\SentiaGlobal\SentiaGlobal.exe
echo Envia la carpeta completa "dist\SentiaGlobal" al otro usuario.
echo No hace falta instalar .NET ni configurar API keys para usar los datos demostrativos.
pause
