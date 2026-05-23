@echo off
setlocal
cd /d "%~dp0"

REM Build (no-op if already built; fast on subsequent runs)
dotnet build -c Release --nologo -v minimal
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

set EXE=bin\Release\net8.0-windows\PKSVModMerger.exe

set MODS=C:\Users\User\Desktop\Scarlet Mods
set BASE=%MODS%\4K HD Texture Pack\romfs\arc\data.trpfd
set ADD1=%MODS%\Clothing Pack 2\romfs\arc\data.trpfd
set ADD2=%MODS%\Pokemon SV Plus\romfs\arc\data.trpfd
set OUT=C:\Users\User\AppData\Roaming\yuzu\load\0100A3D008C5C000\AAA_Master\romfs\arc\data.trpfd

echo.
echo Merging:
echo   base: %BASE%
echo   +    %ADD1%
echo   +    %ADD2%
echo   out: %OUT%
echo.

"%EXE%" "%OUT%" "%BASE%" "%ADD1%" "%ADD2%"
if errorlevel 1 (
    echo Merge failed.
    pause
    exit /b 1
)

echo.
echo Done.
pause
