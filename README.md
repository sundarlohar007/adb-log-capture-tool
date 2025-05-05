# ADB Log Capture Tool for QA Testing

This is a simple yet powerful Batch tool designed to capture logs from an Android device using ADB. It's tailored for QA testers who want to collect app-specific logs in real time without manually entering PIDs or searching through log clutter.

---

## Features

- Supports both **wired** and **wireless** ADB connection
- Automatically waits for device connection
- Automatically detects the target app process
- Saves logs with **timestamped filenames** (never overwritten)
- Shows **device info**, including manufacturer, model, SDK version
- Runs logcat filtered by PID (focused logs)
- Keeps window open after logging

---

## Requirements

- Windows PC
- ADB installed or bundled with the script
- Android device with developer mode + USB debugging or wireless debugging

---

## Usage

1. Open the bat file and enter your application name under : set "APP_FILTER= ENTER_YOUR_APP_NAME_HERE"
2. Connect your Android device (via USB or WiFi debugging)
3. Run `adb_log_capture.bat`
4. Wait for app detection (default app filter is `"EMPTY"`)
5. Logging will start — press any key to stop

The logs will be saved in the same folder with names like:
