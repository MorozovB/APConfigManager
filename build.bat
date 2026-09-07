@echo off
setlocal

:: ============================================
::  AP Configuration Manager - Build Script
:: ============================================
set "VERSION=1.1.0"
set "CONFIG=Release"
set "RID=win-x64"
set "OUTNAME=APConfigManager-v%VERSION%-x64-setup"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo ============================================
echo  AP Configuration Manager - Build Script
echo  Version: %VERSION%
echo ============================================
echo.

:: [1/6] Clean
echo [1/6] Cleaning...
if exist build\publish rd /s /q build\publish
if exist build\wwwroot rd /s /q build\wwwroot
if exist build\output  rd /s /q build\output
mkdir build\publish\api
mkdir build\publish\desktop
mkdir build\wwwroot
mkdir build\output

:: [2/6] Build React UI
echo [2/6] Building React UI...
pushd src\APConfigManager.UI
call npm ci                                  || (popd & goto :error)
call npm run build                           || (popd & goto :error)
xcopy /E /Y /Q dist\* ..\..\build\wwwroot\   || (popd & goto :error)
popd

:: [3/6] Publish API (self-contained)
echo [3/6] Publishing API...
dotnet publish src\APConfigManager.Api\APConfigManager.Api.csproj -c %CONFIG% -r %RID% --self-contained true -o build\publish\api -p:PublishSingleFile=false || goto :error

:: [4/6] Copy React UI into API wwwroot
echo [4/6] Copying React UI into API...
xcopy /E /Y /Q build\wwwroot\* build\publish\api\wwwroot\ || goto :error

:: [5/6] Build Desktop with full MSBuild (unpackaged WinUI generates <AppName>.pri, not resources.pri)
echo [5/6] Building Desktop...
set "TFM=net8.0-windows10.0.19041.0"
set "DESKTOP_OUT=src\APConfigManager.Desktop\APConfigManager.Desktop\bin\x64\%CONFIG%\%TFM%"
if exist "%DESKTOP_OUT%" rd /s /q "%DESKTOP_OUT%"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" ( echo ERROR: vswhere.exe not found - install VS 2022 or Build Tools. & goto :error )
set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD ( echo ERROR: MSBuild.exe not found via vswhere. & goto :error )

"%MSBUILD%" src\APConfigManager.Desktop\APConfigManager.Desktop\APConfigManager.Desktop.csproj /t:Restore;Build /p:Configuration=%CONFIG% /p:Platform=x64 /p:WindowsPackageType=None /v:minimal /nologo || goto :error

if not exist "%DESKTOP_OUT%\APConfigManager.Desktop.exe" ( echo ERROR: no exe at %DESKTOP_OUT% & goto :error )
dir /b "%DESKTOP_OUT%\*.pri" >nul 2>&1 || ( echo ERROR: no .pri generated in %DESKTOP_OUT% ^(check EnableMsixTooling^). & goto :error )
xcopy /E /Y /Q "%DESKTOP_OUT%\*" build\publish\desktop\ || goto :error

:: [6/6] Build installer
echo [6/6] Building installer...
if not exist "installer\APConfigManager.iss" (
    echo ERROR: installer\APConfigManager.iss not found.
    echo Move your .iss out of the gitignored build\ folder into installer\ and commit it.
    goto :error
)
if not exist "%ISCC%" (
    echo Inno Setup not found at "%ISCC%".
    echo Skipping installer. Published files are in build\publish\.
    goto :done
)
"%ISCC%" /Q /DMyAppVersion=%VERSION% /O"build\output" /F"%OUTNAME%" "installer\APConfigManager.iss" || goto :error

echo.
echo ============================================
echo  BUILD COMPLETE!
echo  Installer: build\output\%OUTNAME%.exe
echo ============================================
goto :done

:error
echo.
echo ============================================
echo  BUILD FAILED - see the error above.
echo ============================================
pause
exit /b 1

:done
pause
exit /b 0