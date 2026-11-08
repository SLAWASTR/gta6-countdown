@echo off
chcp 65001 >nul
echo ========================================================
echo   Отправка GTA 6 Countdown на GitHub (SLAWASTR)
echo ========================================================
echo.
git push -u origin main --tags
echo.
if %ERRORLEVEL% equ 0 (
    echo [OK] Успешно отправлено!
    echo GitHub Actions автоматически соберет Android APK, Windows и macOS и опубликует в Releases.
) else (
    echo [!] Если репозиторий 'gta6-countdown' еще не создан на GitHub:
    echo     1. Перейдите на https://github.com/new
    echo     2. Создайте публичный репозиторий с именем: gta6-countdown
    echo     3. Запустите этот скрипт еще раз!
)
pause
