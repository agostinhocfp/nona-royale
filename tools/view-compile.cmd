@echo off
REM ---------------------------------------------------------------
REM Nona Royale - compile the Unity assembly without opening Unity.
REM
REM Runs Unity's own Roslyn over the response file Unity's build
REM graph already wrote for NonaRoyale.Unity, so the references,
REM defines and source list are exactly the editor's. Output goes to
REM Temp\, never into Library\Bee, so the editor's graph is untouched.
REM
REM Double-click it, or run it from anywhere: it anchors itself to the
REM project root. Everything it prints also lands in
REM Temp\view-compile.log, which is the thing to share when it fails.
REM
REM Scratch tooling; safe to delete. Unity recompiles on focus anyway -
REM this is for when the editor is closed.
REM ---------------------------------------------------------------
setlocal

REM Explorer starts a double-clicked script in its own folder, so anchor
REM to the project root before anything looks for a relative path.
cd /d "%~dp0.."

REM Double-clicked rather than run from a console: the window would shut
REM on the first error before it could be read, so hold it open.
set "INTERACTIVE="
echo(%cmdcmdline% | find /i "%~nx0" >nul && set "INTERACTIVE=1"

set "UNITY_ROOT=C:\Program Files\Unity\Hub\Editor\6000.6.0f1"
set "CSC=%UNITY_ROOT%\Editor\Data\Tools\Roslyn\csc.exe"
set "LOG=Temp\view-compile.log"

if not exist "Temp" mkdir "Temp"

call :run > "%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
type "%LOG%"
echo.
echo (also written to %LOG%)
if defined INTERACTIVE pause
exit /b %RC%

:run
if not exist "%CSC%" (
  echo Roslyn not found at "%CSC%".
  echo Edit UNITY_ROOT at the top of this script to the editor version the project uses.
  exit /b 2
)

set "RSP="
for /f "delims=" %%F in ('dir /b /s /o-d "Library\Bee\artifacts\*.dag\NonaRoyale.Unity.rsp" 2^>nul') do (
  if not defined RSP set "RSP=%%F"
)

if not defined RSP (
  echo No NonaRoyale.Unity.rsp under Library\Bee - let Unity compile the project once first.
  exit /b 2
)

echo Project: %CD%
echo Rsp:     %RSP%
echo.

"%CSC%" "@%RSP%" -out:"Temp\view-compile.dll" -refout:"Temp\view-compile.ref.dll"
if errorlevel 1 (
  echo.
  echo FAILED.
  exit /b 1
)

echo.
echo Clean.
exit /b 0
