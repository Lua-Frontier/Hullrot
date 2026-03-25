@echo off
:SERVERFALL
set PDIR=%~dp0
cd %PDIR%Bin\Content.Server
call Content.Server.exe %*
cd %PDIR%
set PDIR=
echo (%time%) FUCK FRONTIER fall, i'am restart...
goto SERVERFALL