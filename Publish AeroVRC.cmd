@echo off
rem Double-click launcher for the AeroVRC Publish & Release GUI.
rem Starts publish-gui.ps1 (sitting next to this file) with no console window.
start "" powershell -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0publish-gui.ps1"
