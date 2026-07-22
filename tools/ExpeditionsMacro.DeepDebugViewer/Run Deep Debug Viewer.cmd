@echo off
setlocal
dotnet run --project "%~dp0ExpeditionsMacro.DeepDebugViewer.csproj" -- %*
if errorlevel 1 pause
