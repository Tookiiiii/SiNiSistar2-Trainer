@echo off
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] .NET Framework 4.x x64 compiler not found: %CSC%
    exit /b 1
)

if not exist "release" mkdir "release"

echo [*] Building SiNiSistar2_Trainer.exe (x64)...
"%CSC%" /nologo /optimize+ /platform:x64 /target:winexe /utf8output ^
    /win32icon:"assets\icon.ico" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:"release\SiNiSistar2_Trainer.exe" ^
    "src\SiNiSistar2Trainer.cs"

if errorlevel 1 (
    echo [ERROR] Build failed!
    exit /b 1
)

echo [OK] Build succeeded: release\SiNiSistar2_Trainer.exe
endlocal
