#!/bin/bash
set -e

echo "Сборка GTA6Widget.app (SwiftUI)..."
pkill -f GTA6Widget 2>/dev/null || true

mkdir -p GTA6Widget.app/Contents/MacOS
mkdir -p GTA6Widget.app/Contents/Resources

swiftc -O -framework Cocoa -framework SwiftUI -framework Combine main.swift -o GTA6Widget.app/Contents/MacOS/GTA6Widget
cp logo.png GTA6Widget.app/Contents/Resources/ 2>/dev/null || true
cp logo.svg GTA6Widget.app/Contents/Resources/ 2>/dev/null || true
codesign --force --deep -s - GTA6Widget.app

echo "Готово! Запуск: open GTA6Widget.app"
