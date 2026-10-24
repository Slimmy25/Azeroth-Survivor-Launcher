@echo off
rem Signs one file with a code-signing certificate. Usage: sign_build.bat "path\to\file.exe"
rem
rem WHY: an unsigned exe has no publisher and no reputation, which is what antivirus programs, Microsoft
rem SmartScreen and Windows 11 Smart App Control judge hardest. A valid signature is the single biggest fix.
rem
rem HOW: create a file called sign_config.bat in this folder with ONE of the blocks below. Without that
rem file this script does nothing and the build stays unsigned.
rem
rem ---- A) Azure Artifact Signing (about 10 USD a month; individuals in the USA/Canada, organizations in
rem         the USA, Canada, EU and UK; identity check takes a few business days) ----
rem   set SIGN_MODE=azure
rem   set SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe
rem   set AZ_DLIB=C:\path\to\Microsoft.ArtifactSigning.Client\bin\x64\Azure.CodeSigning.Dlib.dll
rem   set AZ_METADATA=C:\path\to\metadata.json
rem   (metadata.json holds your Endpoint, CodeSigningAccountName and CertificateProfileName. The Endpoint must
rem    be the region you created the account in, e.g. https://eus.codesigning.azure.net for East US.
rem    Needs the .NET 8 runtime and a Windows SDK signtool 10.0.2261.755 or newer.)
rem
rem ---- B) A normal code-signing certificate in a .pfx file ----
rem   set SIGN_MODE=pfx
rem   set SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe
rem   set PFX_FILE=C:\path\to\certificate.pfx
rem   set PFX_PASS=the password
rem
rem ---- C) A certificate on a hardware token or in the Windows certificate store ----
rem   set SIGN_MODE=store
rem   set SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe
rem   set CERT_SHA1=thumbprint of the certificate
rem
rem Always timestamped: Azure certificates only last 3 days, the timestamp keeps the signature valid after.
rem Never share sign_config.bat or the .pfx file.
setlocal
if "%~1"=="" exit /b 0
if not exist "%~dp0sign_config.bat" goto nocfg
call "%~dp0sign_config.bat"
if not defined SIGNTOOL goto findtool
:havetool
if not exist "%SIGNTOOL%" goto notool

if /i "%SIGN_MODE%"=="azure" goto azure
if /i "%SIGN_MODE%"=="pfx" goto pfx
if /i "%SIGN_MODE%"=="store" goto store
echo [sign] sign_config.bat must set SIGN_MODE to azure, pfx or store.
exit /b 1

:azure
"%SIGNTOOL%" sign /v /fd SHA256 /tr "http://timestamp.acs.microsoft.com" /td SHA256 /dlib "%AZ_DLIB%" /dmdf "%AZ_METADATA%" "%~1"
goto done

:pfx
"%SIGNTOOL%" sign /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /f "%PFX_FILE%" /p "%PFX_PASS%" "%~1"
goto done

:store
"%SIGNTOOL%" sign /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /sha1 %CERT_SHA1% "%~1"
goto done

:done
if errorlevel 1 goto failed
"%SIGNTOOL%" verify /pa "%~1"
if errorlevel 1 goto failed
echo [sign] Signed and verified: %~1
exit /b 0

:failed
echo [sign] Signing FAILED. The build was stopped so an unsigned exe is not shared by mistake.
exit /b 1

:findtool
for /f "delims=" %%t in ('where signtool 2^>nul') do set "SIGNTOOL=%%t"
if defined SIGNTOOL goto havetool
:notool
echo [sign] signtool.exe was not found. Install the Windows SDK and set SIGNTOOL in sign_config.bat.
exit /b 1

:nocfg
echo [sign] No sign_config.bat, so the exe is NOT signed. See the top of sign_build.bat.
exit /b 0
