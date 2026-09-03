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

:: [5/6] Publish Desktop shell
echo [5/6] Publishing Desktop...
dotnet publish src\APConfigManager.Desktop\APConfigManager.Desktop\APConfigManager.Desktop.csproj -c %CONFIG% -r %RID% --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -o build\publish\desktop || goto :error

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