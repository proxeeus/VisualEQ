@echo off
REM VisualEQ launcher — opens the in-app main menu (zone browser, decoder, settings).
REM For legacy CLI-style launch (skip menu, load directly), use load_zone.bat.

REM On Windows ARM64 (Parallels/M-series Mac) the default dotnet is ARM64 but cimgui.dll is x64.
REM Use the x64 dotnet runtime (exec, not run) so native DLLs load correctly.
set "DOTNET_X64=C:\Program Files\dotnet\x64\dotnet.exe"

pushd "%~dp0VisualEQ"
if exist "%DOTNET_X64%" (
    "%DOTNET_X64%" exec "bin\Debug\net8.0\VisualEQ.dll"
) else (
    echo x64 dotnet runtime not found at %DOTNET_X64%.
    echo Falling back to default dotnet — may crash on ARM64 due to cimgui.dll x64 native.
    dotnet exec "bin\Debug\net8.0\VisualEQ.dll"
)
popd
