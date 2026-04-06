@echo off
setlocal

set "ROOT=%~dp0"
set "OUT_DIR=%ROOT%dist\TempChat"

echo Building TempChat standalone EXE...
dotnet publish "%ROOT%chat-client-c\TempChat.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUT_DIR%"

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo.
echo Done! dist\TempChat\TempChat.exe
