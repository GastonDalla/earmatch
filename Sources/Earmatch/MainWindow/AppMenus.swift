import AppKit
import EarmatchCore

enum Credit {
    static let author = "GastonDalla"
    static var line: String { L10n.format("Made by %@", author) }
    static let url = URL(string: "https://github.com/GastonDalla")!
    static let displayURL = "github.com/GastonDalla"
    static var menuLine: String { "\(line) · \(displayURL)" }

    static func openProfile() {
        NSWorkspace.shared.open(url)
    }
}

enum AboutPanel {
    static func show() {
        let centered = NSMutableParagraphStyle()
        centered.alignment = .center
        let base: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
            .foregroundColor: NSColor.labelColor,
            .paragraphStyle: centered,
        ]
        let credits = NSMutableAttributedString(string: Credit.line, attributes: base)
        if let range = credits.string.range(of: Credit.author, options: .backwards) {
            credits.addAttribute(.link, value: Credit.url, range: NSRange(range, in: credits.string))
        }
        credits.append(NSAttributedString(string: "\n\(Credit.displayURL)", attributes: base.merging([
            .foregroundColor: NSColor.secondaryLabelColor,
        ]) { $1 }))

        NSApp.activate()
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Earmatch",
            .credits: credits,
        ])
    }
}

enum MainMenu {
    static func install(replacing: Bool = false, selectPane: @escaping (MainWindowPane) -> Void) {
        guard replacing || NSApp.mainMenu == nil else { return }
        let main = NSMenu()

        let appMenu = NSMenu(title: "Earmatch")
        appMenu.addItem(ClosureMenuItem(title: L10n.text("About Earmatch")) { AboutPanel.show() })
        if let updates = AppUpdater.shared.makeMenuItem() { appMenu.addItem(updates) }
        appMenu.addItem(ClosureMenuItem(title: Credit.line) { Credit.openProfile() })
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: L10n.text("Hide Earmatch"), action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        let hideOthers = appMenu.addItem(withTitle: L10n.text("Hide Others"), action: #selector(NSApplication.hideOtherApplications(_:)), keyEquivalent: "h")
        hideOthers.keyEquivalentModifierMask = [.command, .option]
        appMenu.addItem(withTitle: L10n.text("Show All"), action: #selector(NSApplication.unhideAllApplications(_:)), keyEquivalent: "")
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: L10n.text("Quit Earmatch"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        main.addItem(submenuItem(appMenu))

        let viewMenu = NSMenu(title: L10n.text("View"))
        for pane in MainWindowPane.allCases {
            let item = ClosureMenuItem(title: pane.title) { selectPane(pane) }
            item.keyEquivalent = pane.keyEquivalent
            item.keyEquivalentModifierMask = .command
            viewMenu.addItem(item)
        }
        main.addItem(submenuItem(viewMenu))

        let windowMenu = NSMenu(title: L10n.text("Window"))
        windowMenu.addItem(withTitle: L10n.text("Close"), action: #selector(NSWindow.performClose(_:)), keyEquivalent: "w")
        windowMenu.addItem(withTitle: L10n.text("Minimize"), action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        main.addItem(submenuItem(windowMenu))

        NSApp.mainMenu = main
        NSApp.windowsMenu = windowMenu
    }

    private static func submenuItem(_ menu: NSMenu) -> NSMenuItem {
        let item = NSMenuItem(title: menu.title, action: nil, keyEquivalent: "")
        item.submenu = menu
        return item
    }
}
