@echo off
REM ============================================================
REM  Lanzador de Unity con el ring buffer de graficos ampliado
REM  Proyecto: ElSenderoDeLasEstrellas (Unity 6000.5.4f1)
REM
REM  Soluciona el aviso de consola:
REM  "Ran out of Graphics Ring Buffer space..."
REM ============================================================

set UNITY_EXE="C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"
set PROJECT_PATH="C:\Users\luarb\dev\unity\ElSenderoDeLasEstrellas"

if not exist %UNITY_EXE% (
    echo No se ha encontrado Unity en la ruta:
    echo %UNITY_EXE%
    echo.
    echo Abre este .bat con el boton derecho -^> Editar y corrige
    echo la linea UNITY_EXE con la ruta real de tu instalacion
    echo de Unity 6000.5.4f1 ^(puedes verla en Unity Hub -^> Installs^).
    pause
    exit /b 1
)

echo Cierra primero el proyecto si ya esta abierto en otro Unity Editor.
echo Abriendo el proyecto con -gfx-ring-buffer-size 67108864 (64 MB en bytes) ...

start "" %UNITY_EXE% -projectPath %PROJECT_PATH% -gfx-ring-buffer-size 67108864

exit /b 0
