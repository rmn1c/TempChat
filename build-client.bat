@echo off
setlocal

set JAR_NAME=chat-client-0.1.0-SNAPSHOT.jar
set APP_NAME=TempChat
set OUT_DIR=%~dp0dist

echo [1/2] Building fat JAR...
cd "%~dp0chat-client"
call mvn package -q
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo [2/2] Packaging as Windows executable...
if exist "%OUT_DIR%" rmdir /s /q "%OUT_DIR%"
jpackage ^
    --input target ^
    --main-jar %JAR_NAME% ^
    --name %APP_NAME% ^
    --app-version 1.0 ^
    --type app-image ^
    --dest "%OUT_DIR%"
if errorlevel 1 (
    echo jpackage failed.
    exit /b 1
)

echo.
echo Done! Run: dist\%APP_NAME%\%APP_NAME%.exe
