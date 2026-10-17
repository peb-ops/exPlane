@echo off
rem Builds a Release single-file exe into publish\exPlane.exe (needs the .NET 10 runtime to run).
dotnet publish "%~dp0src\ExPlane\ExPlane.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%~dp0publish" -nologo -v q
if errorlevel 1 (
    echo Publish failed.
    exit /b 1
)
echo Published to %~dp0publish\exPlane.exe
