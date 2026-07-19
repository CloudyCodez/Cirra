@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish_update.ps1" %*
