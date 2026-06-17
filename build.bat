@echo off
echo ============================================
echo  AP Configuration Manager - Build Script
echo  Version: 1.0.0-beta
echo ============================================
echo.

:: Clean
echo [1/6] Cleaning...
if exist build\publish rd /s /q build\publish
if exist build\wwwroot rd /s /q build\wwwroot
if exist build\output rd /s /q build\output
mkdir build\publish\api
mkdir build\publish\desktop
mkdir build\wwwroot
mkdir build\output

:: Build React
echo [2/6] Building React UI...
cd src\APConfigManage.UI
call npm ci
call npm run build
xcopy /E /Y /Q dist\* ..\..\build\wwwroot\
cd ..\..

:: Publish API
echo [3/6] Publishing API (self-contained)...
dotnet publish src\APConfigManager.Api\APConfigManager.Api.csproj -c Release -r win-x64 --self-contained true -o build\publish\api -p:PublishSingleFile=false

:: Copy React to API wwwroot
echo [4/6] Copying React UI to API...
xcopy /E /Y /Q build\wwwroot\* build\publish\api\wwwroot\

:: Publish Desktop
echo [5/6] Publishing Desktop...
dotnet publish src\APConfigManager.Desktop\APConfigManager.Desktop\APConfigManager.Desktop.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -o build\publish\desktop

:: Build Installer
echo [6/6] Building installer...
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" build\installer\APConfigManager.iss
    echo.
    echo ============================================
    echo  BUILD COMPLETE!
    echo  Installer: build\output\APConfigManager-v1.0.0-beta-x64-setup.exe
    echo ============================================
) else (
    echo Inno Setup not found. Skipping installer.
    echo Published files are in build\publish\
)

pause