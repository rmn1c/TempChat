@echo off
setlocal

set ROOT=%~dp0
set JAVA_HOME=%ROOT%tools\jdk-21
set MAVEN_HOME=%ROOT%tools\maven

set JAR_NAME=chat-client-0.1.0-SNAPSHOT.jar
set APP_NAME=TempChat
set OUT_DIR=%ROOT%dist

set JAVA=%JAVA_HOME%\bin\java.exe
set JPACKAGE=%JAVA_HOME%\bin\jpackage.exe
set CLASSWORLDS_JAR=%MAVEN_HOME%\boot\plexus-classworlds-2.9.0.jar

echo [1/2] Building fat JAR...
"%JAVA%" ^
  -classpath "%CLASSWORLDS_JAR%" ^
  "-Dclassworlds.conf=%MAVEN_HOME%\bin\m2.conf" ^
  "-Dmaven.home=%MAVEN_HOME%" ^
  "-Dmaven.multiModuleProjectDirectory=%ROOT%chat-client" ^
  org.codehaus.plexus.classworlds.launcher.Launcher ^
  -f "%ROOT%chat-client\pom.xml" ^
  package -q

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo [2/2] Packaging as Windows executable...
if exist "%OUT_DIR%" rmdir /s /q "%OUT_DIR%"
"%JPACKAGE%" ^
    --input "%ROOT%chat-client\target" ^
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
