@echo off
rem Starts the published exPlane without waiting for it.
if not exist "%~dp0publish\exPlane.exe" (
    echo publish\exPlane.exe not found - run publish.bat first.
    exit /b 1
)
start "" "%~dp0publish\exPlane.exe"
echo exPlane started.
