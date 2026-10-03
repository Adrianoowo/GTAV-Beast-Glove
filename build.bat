@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Building BeastGlove.dll for GTA V Legacy
echo ========================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "!CSC!" (
    echo Error: .NET Framework 4.8 csc.exe compiler not found at: !CSC!
    exit /b 1
)

set "REF=ScriptHookVDotNet3.dll"
if not exist "!REF!" (
    if exist "D:\SteamLibrary\steamapps\common\Grand Theft Auto V\ScriptHookVDotNet3.dll" (
        set "REF=D:\SteamLibrary\steamapps\common\Grand Theft Auto V\ScriptHookVDotNet3.dll"
    ) else (
        echo Error: ScriptHookVDotNet3.dll not found. Place it in this folder to build.
        exit /b 1
    )
)

"!CSC!" /target:library /out:BeastGlove.dll /reference:"!REF!" /reference:System.Windows.Forms.dll BeastGlove.cs

if !ERRORLEVEL! equ 0 (
    echo.
    echo [SUCCESS] BeastGlove.dll built successfully!
) else (
    echo.
    echo [ERROR] Build failed.
    exit /b !ERRORLEVEL!
)
