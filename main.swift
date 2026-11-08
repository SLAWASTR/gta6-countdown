import Cocoa
import SwiftUI
import Combine

// MARK: - Native macOS Wallpaper Blur
struct VisualEffectBackground: NSViewRepresentable {
    var material: NSVisualEffectView.Material = .sidebar
    var blendingMode: NSVisualEffectView.BlendingMode = .behindWindow
    
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = blendingMode
        view.state = .active
        return view
    }
    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}

// MARK: - Live Countdown Data Model
class CountdownModel: ObservableObject {
    @Published var now = Date()
    @Published var hidePreload: Bool = UserDefaults.standard.bool(forKey: "hidePreload") {
        didSet {
            UserDefaults.standard.set(hidePreload, forKey: "hidePreload")
        }
    }
    private var timerCancellable: AnyCancellable?

    init() {
        timerCancellable = Timer.publish(every: 1.0, on: .main, in: .common)
            .autoconnect()
            .sink { [weak self] currentDate in
                self?.now = currentDate
            }
    }

    var releaseDate: Date {
        let cal = Calendar.current
        let year = cal.component(.year, from: now)
        var comp = DateComponents(year: year, month: 11, day: 18, hour: 21, minute: 30, second: 0)
        var date = cal.date(from: comp) ?? now
        if now.timeIntervalSince(date) > 30 * 86400 {
            comp.year = year + 1
            date = cal.date(from: comp) ?? now
        }
        return date
    }

    var preloadDate: Date {
        let cal = Calendar.current
        let year = cal.component(.year, from: now)
        var comp = DateComponents(year: year, month: 11, day: 9, hour: 21, minute: 30, second: 0)
        var date = cal.date(from: comp) ?? now
        if now.timeIntervalSince(date) > 30 * 86400 {
            comp.year = year + 1
            date = cal.date(from: comp) ?? now
        }
        return date
    }

    func timeParts(for target: Date) -> (isDone: Bool, d: String, h: String, m: String, s: String, daysInt: Int) {
        let diff = target.timeIntervalSince(now)
        if diff <= 0 {
            return (true, "00", "00", "00", "00", 0)
        }
        let total = Int(diff)
        let days = total / 86400
        let hours = (total % 86400) / 3600
        let mins = (total % 3600) / 60
        let secs = total % 60
        return (
            false,
            String(format: "%02d", days),
            String(format: "%02d", hours),
            String(format: "%02d", mins),
            String(format: "%02d", secs),
            days
        )
    }
}

// MARK: - Compact Digit Box
struct DigitBox: View {
    let value: String
    let unit: String
    var accentColor: Color = .white
    
    var body: some View {
        VStack(spacing: 1) {
            Text(value)
                .font(.system(size: 13, weight: .bold, design: .rounded))
                .monospacedDigit()
                .lineLimit(1)
                .minimumScaleFactor(0.55)
                .foregroundColor(accentColor)
            Text(unit)
                .font(.system(size: 7.5, weight: .bold))
                .lineLimit(1)
                .minimumScaleFactor(0.6)
                .foregroundColor(.white.opacity(0.6))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.vertical, 3)
        .background(Color.white.opacity(0.09))
        .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
    }
}

// MARK: - Countdown Column Card
struct CountdownCard: View {
    let title: String
    let icon: String
    let dateStr: String
    let isDone: Bool
    let doneText: String
    let days: String
    let hours: String
    let mins: String
    let secs: String
    let accentColor: Color
    
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 3) {
                Text(icon)
                    .font(.system(size: 9.5))
                Text(title)
                    .font(.system(size: 9, weight: .heavy))
                    .lineLimit(1)
                    .foregroundColor(accentColor)
                Spacer(minLength: 4)
                Text(dateStr)
                    .font(.system(size: 9, weight: .semibold))
                    .lineLimit(1)
                    .foregroundColor(.white.opacity(0.55))
            }
            
            if isDone {
                HStack {
                    Spacer()
                    Text(doneText)
                        .font(.system(size: 9.5, weight: .heavy))
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                        .foregroundColor(Color(red: 0.0, green: 1.0, blue: 0.55))
                    Spacer()
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .padding(.vertical, 4)
                .background(Color.green.opacity(0.12))
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
            } else {
                HStack(spacing: 3) {
                    DigitBox(value: days, unit: "ДН")
                    DigitBox(value: hours, unit: "ЧАС")
                    DigitBox(value: mins, unit: "МИН")
                    DigitBox(value: secs, unit: "СЕК")
                }
            }
        }
        .padding(6)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color.black.opacity(0.24))
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(Color.white.opacity(0.08), lineWidth: 0.5)
        )
    }
}

// MARK: - Fluid Resilient Widget Content
struct GTAFluidWidgetView: View {
    @ObservedObject var model: CountdownModel
    
    var logoImage: NSImage? {
        if let bundleURL = Bundle.main.url(forResource: "logo", withExtension: "svg"),
           let img = NSImage(contentsOf: bundleURL) {
            return img
        }
        return NSImage(contentsOfFile: "logo.svg")
    }

    var body: some View {
        let r = model.timeParts(for: model.releaseDate)
        let p = model.timeParts(for: model.preloadDate)

        ZStack {
            // Native live wallpaper blur
            VisualEffectBackground(material: .sidebar, blendingMode: .behindWindow)
            
            // Atmospheric twilight gradient
            LinearGradient(
                colors: [
                    Color(red: 0.16, green: 0.10, blue: 0.28).opacity(0.68),
                    Color(red: 0.08, green: 0.06, blue: 0.18).opacity(0.78)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            
            VStack(spacing: 6) {
                // Top section (Weather Widget Metaphor)
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("Grand Theft Auto VI")
                            .font(.system(size: 13, weight: .semibold))
                            .lineLimit(1)
                            .minimumScaleFactor(0.8)
                            .foregroundColor(.white.opacity(0.9))
                        
                        HStack(alignment: .firstTextBaseline, spacing: 4) {
                            Text(r.isDone ? "0" : "\(r.daysInt)")
                                .font(.system(size: 38, weight: .light, design: .rounded))
                                .lineLimit(1)
                                .minimumScaleFactor(0.7)
                                .foregroundColor(.white)
                            VStack(alignment: .leading, spacing: 0) {
                                Text("ДНЕЙ")
                                    .font(.system(size: 9.5, weight: .bold))
                                    .lineLimit(1)
                                    .foregroundColor(.white.opacity(0.8))
                                Text(r.isDone ? "игра вышла!" : "до релиза")
                                    .font(.system(size: 8.5, weight: .medium))
                                    .lineLimit(1)
                                    .foregroundColor(.white.opacity(0.55))
                            }
                        }
                    }
                    
                    Spacer(minLength: 8)
                    
                    VStack(alignment: .trailing, spacing: 2) {
                        if let img = logoImage {
                            Image(nsImage: img)
                                .resizable()
                                .scaledToFit()
                                .frame(height: 32)
                                .shadow(color: Color.pink.opacity(0.35), radius: 6, x: 0, y: 1)
                        }
                        
                        Text("18.11 в 21:30")
                            .font(.system(size: 10, weight: .semibold))
                            .lineLimit(1)
                            .foregroundColor(.white.opacity(0.85))
                        
                        HStack(spacing: 2) {
                            Text(p.isDone ? "⚡ Загрузка активна" : "⚡ Загрузка: 9.11")
                                .font(.system(size: 9, weight: .semibold))
                                .lineLimit(1)
                                .foregroundColor(p.isDone ? Color(red: 0.2, green: 1.0, blue: 0.6) : Color(red: 0.2, green: 0.85, blue: 1.0))
                        }
                    }
                }
                .padding(.horizontal, 2)
                
                // Thin divider
                Rectangle()
                    .fill(Color.white.opacity(0.12))
                    .frame(height: 0.5)
                
                // Bottom section (Two compact countdown capsules)
                HStack(spacing: 8) {
                    CountdownCard(
                        title: "РЕЛИЗ",
                        icon: "🚀",
                        dateStr: "18.11",
                        isDone: r.isDone,
                        doneText: "ИГРА ВЫШЛА! 🎉",
                        days: r.d,
                        hours: r.h,
                        mins: r.m,
                        secs: r.s,
                        accentColor: Color(red: 1.0, green: 0.28, blue: 0.6)
                    )
                    if !model.hidePreload {
                        CountdownCard(
                            title: "ЗАГРУЗКА",
                            icon: "⚡",
                            dateStr: "09.11",
                            isDone: p.isDone,
                            doneText: "ДОСТУПНА! ⚡",
                            days: p.d,
                            hours: p.h,
                            mins: p.m,
                            secs: p.s,
                            accentColor: Color(red: 0.2, green: 0.85, blue: 1.0)
                        )
                    }
                }
                .frame(maxHeight: .infinity)
            }
            .padding(11)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .stroke(Color.white.opacity(0.18), lineWidth: 0.8)
        )
    }
}

// MARK: - Custom Panel that never steals focus
class WidgetPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

// MARK: - Native Draggable & Context-Click View
class DraggableHostingView<Content: View>: NSHostingView<Content> {
    var onRightClick: ((NSEvent) -> Void)?
    var isDraggingLocked = false
    private let edgeMargin: CGFloat = 8.0

    override func rightMouseDown(with event: NSEvent) {
        onRightClick?(event)
    }

    override func mouseDown(with event: NSEvent) {
        guard let window = self.window else { return }
        
        let loc = convert(event.locationInWindow, from: nil)
        let isEdge = loc.x < edgeMargin || loc.x > bounds.width - edgeMargin ||
                     loc.y < edgeMargin || loc.y > bounds.height - edgeMargin

        if isEdge {
            super.mouseDown(with: event)
        } else if !isDraggingLocked {
            window.performDrag(with: event)
        }
    }
}

// MARK: - App Delegate & Window Setup
class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: WidgetPanel!
    var hostingView: DraggableHostingView<GTAFluidWidgetView>!
    let model = CountdownModel()
    
    var isFloating = false
    var isLocked = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)

        let screenRect = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900)
        
        // Exact Apple Medium Widget dimensions (342 × 158)
        var widgetWidth: CGFloat = 342
        var widgetHeight: CGFloat = 158

        if let savedW = UserDefaults.standard.value(forKey: "widgetW_v4") as? CGFloat, savedW >= 250 {
            widgetWidth = savedW
        }
        if let savedH = UserDefaults.standard.value(forKey: "widgetH_v4") as? CGFloat, savedH >= 110 {
            widgetHeight = savedH
        }

        var originX = screenRect.maxX - widgetWidth - 40
        var originY = screenRect.maxY - widgetHeight - 40

        if let savedX = UserDefaults.standard.value(forKey: "widgetX_v4") as? CGFloat,
           let savedY = UserDefaults.standard.value(forKey: "widgetY_v4") as? CGFloat {
            originX = savedX
            originY = savedY
        }

        let rect = NSRect(x: originX, y: originY, width: widgetWidth, height: widgetHeight)
        
        window = WidgetPanel(
            contentRect: rect,
            styleMask: [.nonactivatingPanel, .borderless, .resizable],
            backing: .buffered,
            defer: false
        )
        window.delegate = self
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = true
        window.isMovableByWindowBackground = true

        // Freeform resizing: user can resize freely from any corner or edge!
        window.minSize = NSSize(width: 250, height: 110)
        window.maxSize = NSSize(width: 800, height: 500)
        
        // Sits on desktop layer (below normal windows): NEVER jumps in front of other apps!
        window.level = NSWindow.Level(rawValue: -1)
        window.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]

        hostingView = DraggableHostingView(rootView: GTAFluidWidgetView(model: model))
        hostingView.frame = window.contentView!.bounds
        hostingView.autoresizingMask = [.width, .height]
        hostingView.onRightClick = { [weak self] event in
            self?.showContextMenu(event: event)
        }
        window.contentView = hostingView

        window.makeKeyAndOrderFront(nil)
    }

    func windowDidMove(_ notification: Notification) {
        UserDefaults.standard.set(window.frame.origin.x, forKey: "widgetX_v4")
        UserDefaults.standard.set(window.frame.origin.y, forKey: "widgetY_v4")
    }

    func windowDidResize(_ notification: Notification) {
        UserDefaults.standard.set(window.frame.width, forKey: "widgetW_v4")
        UserDefaults.standard.set(window.frame.height, forKey: "widgetH_v4")
    }

    func setSize(width: CGFloat, height: CGFloat) {
        let currentOrigin = window.frame.origin
        let newOriginY = window.frame.maxY - height
        window.setFrame(NSRect(x: currentOrigin.x, y: newOriginY, width: width, height: height), display: true, animate: true)
        UserDefaults.standard.set(width, forKey: "widgetW_v4")
        UserDefaults.standard.set(height, forKey: "widgetH_v4")
    }

    // MARK: - Context Menu on Right Click (ПК)
    func showContextMenu(event: NSEvent) {
        let menu = NSMenu()
        
        let titleItem = NSMenuItem(title: "🎮 GTA VI Виджет", action: nil, keyEquivalent: "")
        titleItem.isEnabled = false
        menu.addItem(titleItem)
        menu.addItem(NSMenuItem.separator())

        // Preset Sizes Submenu
        let sizeMenu = NSMenu()
        
        let sExact = NSMenuItem(title: "Виджет погоды (342 × 158)", action: #selector(setSizeWeather), keyEquivalent: "")
        sExact.target = self
        sizeMenu.addItem(sExact)

        let sCompact = NSMenuItem(title: "Компактный (290 × 134)", action: #selector(setSizeCompact), keyEquivalent: "")
        sCompact.target = self
        sizeMenu.addItem(sCompact)

        let sLarge = NSMenuItem(title: "Крупный (400 × 185)", action: #selector(setSizeLarge), keyEquivalent: "")
        sLarge.target = self
        sizeMenu.addItem(sLarge)

        let sizeParent = NSMenuItem(title: "Сбросить размер к...", action: nil, keyEquivalent: "")
        sizeParent.submenu = sizeMenu
        menu.addItem(sizeParent)

        menu.addItem(NSMenuItem.separator())

        let floatItem = NSMenuItem(title: "Поверх всех окон", action: #selector(toggleFloating), keyEquivalent: "")
        floatItem.target = self
        floatItem.state = isFloating ? .on : .off
        menu.addItem(floatItem)

        let lockItem = NSMenuItem(title: "Закрепить положение", action: #selector(toggleLock), keyEquivalent: "")
        lockItem.target = self
        lockItem.state = isLocked ? .on : .off
        menu.addItem(lockItem)

        let preloadItem = NSMenuItem(title: "⚡ Показывать таймер предзагрузки", action: #selector(togglePreload), keyEquivalent: "")
        preloadItem.target = self
        preloadItem.state = model.hidePreload ? .off : .on
        menu.addItem(preloadItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(title: "Закрыть виджет", action: #selector(quitApp), keyEquivalent: "")
        quitItem.target = self
        menu.addItem(quitItem)

        NSMenu.popUpContextMenu(menu, with: event, for: self.hostingView)
    }

    @objc func setSizeWeather() { setSize(width: 342, height: 158) }
    @objc func setSizeCompact() { setSize(width: 290, height: 134) }
    @objc func setSizeLarge()   { setSize(width: 400, height: 185) }

    @objc func toggleFloating() {
        isFloating.toggle()
        window.level = isFloating ? .floating : NSWindow.Level(rawValue: -1)
    }

    @objc func toggleLock() {
        isLocked.toggle()
        hostingView.isDraggingLocked = isLocked
        window.isMovableByWindowBackground = !isLocked
    }

    @objc func togglePreload() {
        model.hidePreload.toggle()
    }

    @objc func quitApp() {
        NSApplication.shared.terminate(nil)
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
