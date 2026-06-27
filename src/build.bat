@echo off
title CatCraft Builder

echo ========================================
echo    CatCraft Build Script
echo ========================================
echo.

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    echo Please install .NET 7.0 SDK
    pause
    exit /b 1
)

echo [INFO] Building CatCraft...

dotnet clean -c Release
dotnet restore
dotnet build -c Release

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo CopyImages
if exist "src\img" (
    xcopy /E /Y "src\img" "bin\Release\net7.0-windows\src\img\" >nul
    echo [SUCCESS] Images Copy completed!
)

echo.
echo [SUCCESS] Build completed!
echo Output: bin\Release\net7.0-windows\CatCraft.exe
echo.

pause