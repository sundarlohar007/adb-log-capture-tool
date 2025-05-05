@echo off
setlocal enabledelayedexpansion

:: ==== Sundar's LogCapture v1.2 ====

:: Set default app name , you can change the fates to any other name of your package
set "APP_FILTER=ENTER_YOUR_APP_NAME_HERE"

:: Get date and time for unique filename
for /f "tokens=1-3 delims=/ " %%a in ('date /t') do (
    set "MYDATE=%%c-%%a-%%b"
)
for /f "tokens=1-2 delims=:" %%a in ("%time%") do (
    set "HOUR=%%a"
    set "MIN=%%b"
)

if "!HOUR:~0,1!"==" " set "HOUR=0!HOUR:~1!"

set "AMPM=AM"
set /a HOUR_INT=!HOUR!
if !HOUR_INT! GEQ 12 (
    set "AMPM=PM"
    if not !HOUR_INT! EQU 12 (
        set /a HOUR_INT-=12
    )
) else (
    if !HOUR_INT! EQU 0 (
        set "HOUR_INT=12"
    )
)
if !HOUR_INT! LSS 10 set "HOUR_INT=0!HOUR_INT!"
set "TIMESTAMP=!HOUR_INT!_!MIN!!AMPM!"

:: Wait for device connection
:wait_device
cls
echo Waiting for device...
adb get-state 1>nul 2>nul
if errorlevel 1 (
    timeout /t 2 >nul
    goto :wait_device
)

:: Get device info
for /f "tokens=* delims=" %%d in ('adb shell getprop ro.product.manufacturer') do set "MANUFACTURER=%%d"
for /f "tokens=* delims=" %%d in ('adb shell getprop ro.product.model') do set "MODEL=%%d"
for /f "tokens=* delims=" %%d in ('adb shell getprop ro.build.version.sdk') do set "ANDROID_SDK=%%d"
set "DEVICE_NAME=%MANUFACTURER%_%MODEL%"

:: Search for app and PID
:checkagain
cls
echo Looking for app: %APP_FILTER% ...
adb shell ps -A | findstr /i "u0" > temp_process.txt

set "FOUND_PID="
set "PACKAGE_NAME="

for /f "tokens=2,9" %%a in (temp_process.txt) do (
    echo %%b | findstr /i "%APP_FILTER%" >nul
    if !errorlevel! == 0 (
        set "FOUND_PID=%%a"
        set "PACKAGE_NAME=%%b"
        goto :found
    )
)

timeout /t 2 >nul
goto :checkagain

:found
:: Prepare log file
set "PACKAGE_NAME_SAFE=!PACKAGE_NAME::=__!"
set "PACKAGE_NAME_SAFE=!PACKAGE_NAME_SAFE:/=__!"
set "PACKAGE_NAME_SAFE=!PACKAGE_NAME_SAFE:\=__!"
set "LOG_FILE=%~dp0%DEVICE_NAME%_!PACKAGE_NAME_SAFE!_!TIMESTAMP!.log"

:: Show ASCII art

echo ===========================================
echo  DEVICE NAME   : !DEVICE_NAME!
echo  MANUFACTURER  : !MANUFACTURER!
echo  MODEL         : !MODEL!
echo  SDK VERSION   : !ANDROID_SDK!
echo  PACKAGE FOUND : !PACKAGE_NAME!
echo  PID           : !FOUND_PID!
echo  LOG FILE      : !LOG_FILE!
echo ===========================================
echo Logging started... Press any key to stop.
echo.

:: Save metadata to log
echo DeviceName: !DEVICE_NAME! > "!LOG_FILE!"
echo Manufacturer: !MANUFACTURER! >> "!LOG_FILE!"
echo Model: !MODEL! >> "!LOG_FILE!"
echo AndroidSDK: !ANDROID_SDK! >> "!LOG_FILE!"
echo Package: !PACKAGE_NAME! >> "!LOG_FILE!"
echo PID: !FOUND_PID! >> "!LOG_FILE!"
echo Timestamp: !MYDATE! !TIMESTAMP! >> "!LOG_FILE!"
echo. >> "!LOG_FILE!"

:: Start log capture
start "" /b adb logcat --pid=!FOUND_PID! >> "!LOG_FILE!" 2>&1

:: Wait for keypress
pause >nul

:: ==== Stop log capture ====
:: Note: Not killing adb to avoid interrupting other services
echo.
echo Logging stopped. Logs saved to:
echo !LOG_FILE!


echo.
echo Logging stopped.
echo Log saved to: !LOG_FILE!

:: Cleanup
del temp_process.txt >nul 2>&1
endlocal
pause
