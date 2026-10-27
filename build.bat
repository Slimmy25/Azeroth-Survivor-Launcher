@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem Builds "Azeroth Survivors Launcher.exe" with the C# compiler that is already part of Windows
rem (.NET Framework 4.8, on every Windows 10 and 11 PC). Nothing has to be installed and nothing is downloaded.
rem The result is one small exe in the "dist" folder. Players need nothing else either.
rem
rem After the build the exe is signed if you made a sign_config.bat (see sign_build.bat), and the SHA-256 of the
rem exe is written to dist\Azeroth Survivors Launcher.sha256.txt.
rem
rem To change the patch notes or the addon list, edit assets\changelog.txt or assets\addons.txt and run this again.

rem On GitHub (CI is set there) the window does not wait for a key press.
set "PAUSE=pause"
if defined CI set "PAUSE=rem"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo The .NET Framework 4 compiler was not found on this PC:
    echo   %CSC%
    echo Turn on ".NET Framework 4.8 Advanced Services" in "Turn Windows features on or off" and try again.
    %PAUSE%
    exit /b 1
)

for %%f in (src\Program.cs assets\changelog.txt assets\addons.txt assets\launcher-about.txt assets\launcher-changelog.txt assets\logo.png assets\portrait.png assets\AzerothSurvivors.ico app.manifest) do (
    if not exist "%%f" (
        echo Missing file: %%f
        echo Keep the whole folder together: build.bat, app.manifest, the src and the assets folders.
        %PAUSE%
        exit /b 1
    )
)

if not exist dist mkdir dist
set "OUT=dist\Azeroth Survivors Launcher.exe"
if exist "%OUT%" del "%OUT%"

rem every language file in assets\lang is built in (see assets\lang\README.md)
set "LANGRES="
for %%f in (assets\lang\*.txt) do set "LANGRES=!LANGRES! /resource:assets\lang\%%~nxf,AS.lang.%%~nf.txt"
for %%f in (assets\retro\*.png) do set "LANGRES=!LANGRES! /resource:assets\retro\%%~nxf,AS.retro.%%~nxf"
for %%f in (assets\fonts\*.ttf) do set "LANGRES=!LANGRES! /resource:assets\fonts\%%~nxf,AS.font.%%~nxf"

echo Building %OUT% ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /langversion:5 /warn:3 /nowarn:162,429 ^
  /out:"%OUT%" /win32icon:assets\AzerothSurvivors.ico /win32manifest:app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Management.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /resource:assets\logo.png,AS.logo.png /resource:assets\discord\symbol-blurple.png,AS.discord-blurple.png /resource:assets\discord\symbol-white.png,AS.discord-white.png /resource:assets\portrait.png,AS.portrait.png ^
  /resource:assets\AzerothSurvivors.ico,AS.app.ico /resource:assets\changelog.txt,AS.changelog.txt ^
  /resource:assets\addons.txt,AS.addons.txt /resource:assets\launcher-about.txt,AS.launcher-about.txt /resource:assets\guide.txt,AS.guide.txt /resource:assets\game-text.txt,AS.game-text.txt /resource:assets\talents.txt,AS.talents.txt /resource:assets\items.txt,AS.items.txt /resource:videos\big-streamers\videos.txt,AS.big-streamers.txt /resource:assets\gear-stats.txt,AS.gear-stats.txt /resource:assets\spell-icons.txt,AS.spell-icons.txt /resource:assets\npc-models.txt,AS.npc-models.txt !LANGRES! ^
  /resource:addons\AZSStats\AZSStats.toc,AS.addon.AZSStats.toc /resource:addons\AZSStats\AZSStats.lua,AS.addon.AZSStats.lua /resource:addons\AZSStats\AZSStatsLive.lua,AS.addon.AZSStatsLive.lua /resource:addons\AZSStats\AZSStatsIcons.lua,AS.addon.AZSStatsIcons.lua /resource:addons\AZSStats\AZSStatsUI.lua,AS.addon.AZSStatsUI.lua /resource:addons\AZSStats\AZSStatsFaces.lua,AS.addon.AZSStatsFaces.lua /resource:addons\AZSKit\AZSKit.lua,AS.addon.kit.AZSKit.lua /resource:addons\AZSHandbook\AZSHandbook.toc,AS.addon.AZSHandbook.AZSHandbook.toc /resource:addons\AZSHandbook\AZSHandbook.lua,AS.addon.AZSHandbook.AZSHandbook.lua /resource:addons\AZSMinorMods\AZSMinorMods.toc,AS.addon.AZSMinorMods.AZSMinorMods.toc /resource:addons\AZSMinorMods\AZSMinorMods.lua,AS.addon.AZSMinorMods.AZSMinorMods.lua ^
  src\*.cs
if errorlevel 1 goto fail

if exist "%~dp0sign_build.bat" call "%~dp0sign_build.bat" "%OUT%"
if errorlevel 1 goto fail

powershell -NoProfile -Command "Get-FileHash -Algorithm SHA256 '%OUT%' | ForEach-Object { $_.Hash.ToLower() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content -Encoding ascii 'dist\Azeroth Survivors Launcher.sha256.txt'"

echo.
echo Done.  %~dp0%OUT%
echo It is a single file: share just the exe (or zip it). Players put it in the folder where the game is (or should be installed).
if not defined CI explorer "%~dp0dist"
%PAUSE%
exit /b 0

:fail
echo.
echo Build failed. See the messages above (copy them and send them over if you want them fixed).
%PAUSE%
exit /b 1
