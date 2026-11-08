@echo off
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

echo Compiling GTA6Widget.exe...
"%CSC%" /target:winexe /out:GTA6Widget.exe /win32icon:icon.ico /res:logo.png,logo.png /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:System.dll /r:System.Xaml.dll /r:System.Drawing.dll GTA6Widget.cs

echo Done! Run GTA6Widget.exe
if "%1"=="" (
    rem non-interactive or direct
)
