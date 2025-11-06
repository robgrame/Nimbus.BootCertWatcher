@echo off
REM Start Development Environment - Secure Boot Dashboard

echo.
echo ==============================================
echo   Starting Secure Boot Dashboard (Dev)
echo ==============================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET 8 SDK.
    pause
    exit /b 1
)

echo Starting API on https://localhost:7120...
start "SecureBootDashboard.Api" cmd /k "cd SecureBootDashboard.Api && dotnet run"

timeout /t 3 /nobreak >nul

echo Starting Web on https://localhost:7001...
start "SecureBootDashboard.Web" cmd /k "cd SecureBootDashboard.Web && dotnet run"

timeout /t 3 /nobreak >nul

echo.
echo ==============================================
echo   Services Started!
echo ==============================================
echo.
echo   API:     https://localhost:7120
echo   Swagger: https://localhost:7120/swagger
echo   Web:     https://localhost:7001
echo.
echo   Close the command windows to stop services.
echo ==============================================
echo.

pause
