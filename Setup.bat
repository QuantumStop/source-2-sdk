@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 (
    pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET 10 SDK is required but 'dotnet' was not found on PATH.
    echo Install it from https://dotnet.microsoft.com/download and rerun Setup.bat.
    popd
    pause
    exit /b 1
)

dotnet run --verbosity quiet --project ".\engine\Tools\SboxBuild\SboxBuild.csproj" -- bootstrap %*
set "exitCode=%errorlevel%"

popd
pause
exit /b %exitCode%