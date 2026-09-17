@echo off
REM Wrapper para ejecutar build.ps1 desde cmd / doble clic.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
