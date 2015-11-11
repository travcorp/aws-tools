@echo off

SET version=%1

powershell -ExecutionPolicy Bypass -File .\version.ps1 -version %version%
exit /b %errorlevel%
