# 🎮 Grand Theft Auto VI — Countdown Widgets & Multiplatform App

[![Build and Release GTA 6 Countdown](https://github.com/SLAWASTR/gta6-countdown/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/SLAWASTR/gta6-countdown/actions/workflows/build-and-release.yml)
[![Release](https://img.shields.io/github/v/release/SLAWASTR/gta6-countdown?color=ff2a8d)](https://github.com/SLAWASTR/gta6-countdown/releases)
[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows%20%7C%20macOS%20%7C%20Web-00f2fe)](https://github.com/SLAWASTR/gta6-countdown)

Нативный кроссплатформенный набор виджетов и приложений обратного отсчета до выхода **Grand Theft Auto VI** (18 ноября 2026, 21:30 МСК) и старта предзагрузки (9 ноября 2026, 21:30 МСК).

---

## 📱 Возможности по платформам

### 1. 🤖 Android (`GTA6Countdown-Android.apk`)
- **Виджет 2х2 (Полный таймер)**:
  - Обратный отсчет до **Релиза** (18.11 • 21:30) и **Предзагрузки** (09.11 • 21:30).
  - Крупные неоновые цифры (Дни, Часы, Минуты).
  - Кнопка принудительного мгновенного обновления прямо на виджете.
  - Динамическая поддержка скрытия предзагрузки: если предзагрузка отключена в настройках приложения, блок предзагрузки скрывается, а таймер релиза занимает всю полезную площадь виджета.
- **Виджет 1х2 (Только Релиз)**:
  - Компактная горизонтальная карточка в одну строку для домашнего экрана.
  - Отображает исключительно таймер до релиза 18 ноября (`XX дн : YY час : ZZ мин`).
- **Основное приложение (`MainActivity`)**:
  - Атмосферный интерфейс в неоновом стиле Vice City с официальным логотипом.
  - Живой посекундный таймер в реальном времени.
  - **Ручной переключатель**: возможность в любой момент отключить/скрыть таймер предзагрузки в приложении и на виджете 2х2.
  - Быстрое добавление виджетов на рабочий стол в один клик (Pin Widget).

### 2. 🪟 Windows (`GTA6Widget.exe`)
- Нативный легковесный C#/WPF виджет без зависимостей.
- Закреплен на рабочем столе (`HWND_BOTTOM`, `WS_EX_NOACTIVATE` — не перекрывает активные окна при клике).
- Свободное перетаскивание и изменение размера за края/углы.
- Контекстное меню по правой кнопке мыши (ПКМ):
  - Переключение показа таймера предзагрузки.
  - Пресеты размеров («Виджет погоды», «Компактный», «Крупный»).
  - Режим «Поверх всех окон».
  - Закрепление положения на экране.

### 3. 🍏 macOS (`GTA6Widget.app`)
- Нативный SwiftUI/AppKit виджет с эффектом размытия живых обоев (`NSVisualEffectView`).
- Закреплен на уровне рабочего стола, поддержка свободного перемещения и изменения размеров.
- Контекстное меню со всеми настройками и управлением таймером предзагрузки.

### 4. 🌐 Web (`index.html`)
- Автономная интерактивная веб-страница с Vice City градиентами, аудио-эффектами, частицами конфетти при наступлении даты и экспортом события в календарь (`.ics`).

---

## 🚀 Скачать готовые сборки

Все бинарники автоматически собираются в GitHub Actions и публикуются в [Releases](https://github.com/SLAWASTR/gta6-countdown/releases):
- 📱 **Android**: `GTA6Countdown-Android.apk`
- 🪟 **Windows**: `GTA6Countdown-Windows.zip` (содержит `GTA6Widget.exe`, иконки и автозапуск)
- 🍏 **macOS**: `GTA6Countdown-macOS.zip` (содержит `GTA6Widget.app`)

---

## 🛠 Сборка из исходников

### Android APK
```bash
cd android
./gradlew assembleRelease
```
Результат: `android/app/build/outputs/apk/release/app-release.apk`

### Windows (через стандартный csc .NET Framework)
```cmd
build_win.bat
```
Результат: `GTA6Widget.exe`

### macOS (через swiftc)
```bash
chmod +x build.sh
./build.sh
```
Результат: `GTA6Widget.app`
