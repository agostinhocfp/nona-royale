@echo off
"C:\Program Files\dotnet\dotnet.exe" restore "%~dp0NonaRoyale.TestsVerify.csproj" --nologo -v q
exit /b %ERRORLEVEL%
