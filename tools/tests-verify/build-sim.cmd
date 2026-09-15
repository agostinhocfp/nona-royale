@echo off
"C:\Program Files\dotnet\dotnet.exe" build "%~dp0..\sim\NonaRoyale.Sim" -v q --nologo
exit /b %ERRORLEVEL%
