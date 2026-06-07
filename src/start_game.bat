@echo off
title CatCraft - 小猫挖矿

echo ========================================
echo    小猫挖矿 CatCraft v3.1
echo    Pre-Release 2
echo ========================================
echo.

if not exist "bin\Release\net7.0-windows\CatCraft.exe" (
    echo 未找到游戏程序文件！
    echo 请先运行 build.bat 编译游戏。
    echo.
    pause
    exit /b 1
)

echo 正在启动游戏...
start "" "bin\Release\net7.0-windows\CatCraft.exe"