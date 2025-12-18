@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo Windows Service Installation Script
echo ========================================
echo.

:: =========================
:: Configuration (Modify if needed)
:: =========================
set "serviceName=ZakYip.WheelDiverterSorter"
set "serviceDisplayName=ZakYip.WheelDiverterSorter Service"
set "serviceDescription=Linear wheel diverter sorting system service"

:: EXE path - defaults to same directory as this batch file
set "exeName=ZakYip.WheelDiverterSorter.Host.exe"
set "exePath=%~dp0%exeName%"

:: =========================
:: Administrator Check
:: =========================
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo [ERROR] Administrator privileges required
  echo Please right-click and select "Run as administrator"
  pause
  exit /b 1
)

:: =========================
:: File Validation
:: =========================
echo [INFO] Checking for executable file...
if not exist "%exePath%" (
  echo [ERROR] Executable not found: %exePath%
  echo Please ensure %exeName% is in the same directory as this script
  pause
  exit /b 1
)
echo [OK] Found: %exePath%
echo.

:: =========================
:: Check for required files
:: =========================
echo [INFO] Checking for required configuration files...
set "nlogConfig=%~dp0nlog.config"
set "appsettings=%~dp0appsettings.json"

if not exist "%nlogConfig%" (
  echo [WARNING] nlog.config not found: %nlogConfig%
  echo The service may fail to start without proper logging configuration
)

if not exist "%appsettings%" (
  echo [WARNING] appsettings.json not found: %appsettings%
  echo The service will use default configuration
)
echo.

:: =========================
:: Stop and remove existing service
:: =========================
sc query "%serviceName%" >nul 2>&1
if %errorlevel%==0 (
  echo [INFO] Existing service detected: %serviceName%
  echo Stopping and removing...
  sc stop "%serviceName%" >nul 2>&1
  timeout /t 2 >nul
  sc delete "%serviceName%" >nul 2>&1
  timeout /t 1 >nul
  echo [OK] Existing service removed
  echo.
)

:: =========================
:: Create Service
:: =========================
echo [INFO] Creating service: %serviceName%
sc create "%serviceName%" binPath= "\"%exePath%\"" start= auto DisplayName= "%serviceDisplayName%" >nul
if %errorlevel% neq 0 (
  echo [FAIL] Service creation failed
  echo Check permissions or path: %exePath%
  pause
  exit /b 1
)
echo [OK] Service created successfully
echo.

:: Set description
sc description "%serviceName%" "%serviceDescription%" >nul

:: =========================
:: Configure failure recovery
:: - Reset failure count after 60 seconds
:: - Restart after 5 seconds on each failure (up to 3 times)
:: - Consider abnormal exits as failures
:: =========================
echo [INFO] Configuring automatic restart on failure...
sc failure "%serviceName%" reset= 60 actions= restart/5000/restart/5000/restart/5000 >nul
sc failureflag "%serviceName%" 1 >nul 2>&1
echo [OK] Failure recovery configured
echo.

:: =========================
:: Start Service
:: =========================
echo [INFO] Starting service...
sc start "%serviceName%"
if %errorlevel% neq 0 (
  echo [WARNING] Service may have failed to start
  echo Check: "sc query %serviceName%" for status
  echo Check logs in: %~dp0logs\
  echo.
  echo Common issues:
  echo   1. Missing nlog.config or appsettings.json
  echo   2. Port already in use (default: 5000)
  echo   3. Missing runtime dependencies
) else (
  timeout /t 2 >nul
  sc query "%serviceName%" | find "RUNNING" >nul
  if %errorlevel%==0 (
    echo [SUCCESS] Service is running
  ) else (
    echo [WARNING] Service started but not running
    echo Check logs in: %~dp0logs\
  )
)
echo.

:: =========================
:: Summary
:: =========================
echo ========================================
echo Installation Complete
echo ========================================
echo Service Name: %serviceName%
echo Display Name: %serviceDisplayName%
echo Executable: %exePath%
echo Status: Use "sc query %serviceName%" to check
echo Logs: %~dp0logs\
echo.
echo Auto-restart: Enabled (5 sec delay, 3 attempts)
echo.
echo Useful Commands:
echo   Start:   sc start %serviceName%
echo   Stop:    sc stop %serviceName%
echo   Status:  sc query %serviceName%
echo   Remove:  sc delete %serviceName%
echo.
pause
endlocal
