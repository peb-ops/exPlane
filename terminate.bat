@echo off
rem Closes exPlane gracefully (saves window position); force-kills it if it hasn't exited after ~5 s.
call :running || (echo exPlane is not running. & exit /b 0)

if exist "%~dp0publish\exPlane.exe" start "" "%~dp0publish\exPlane.exe" --exit

for /L %%i in (1,1,5) do (
    call :running || (echo exPlane closed. & exit /b 0)
    rem ~1 s pause; "timeout" fails when stdin is redirected.
    ping -n 2 127.0.0.1 >nul
)

taskkill /IM exPlane.exe /F >nul 2>&1
echo exPlane did not exit in time - force-killed.
exit /b 0

:running
tasklist /FI "IMAGENAME eq exPlane.exe" 2>nul | find /I "exPlane.exe" >nul
exit /b %errorlevel%
