import Foundation

public struct WindowRenderShot: Equatable, Sendable {
    public static let scale: CGFloat = 2

    public static let all = [
        WindowRenderShot(fileName: "live.png", pane: .live, deckCount: 2),
        WindowRenderShot(fileName: "overlay.png", pane: .overlay, deckCount: 2),
        WindowRenderShot(fileName: "practice.png", pane: .practice, deckCount: 2),
        WindowRenderShot(fileName: "setup.png", pane: .setup, deckCount: 2),
        WindowRenderShot(fileName: "live-4deck.png", pane: .live, deckCount: 4),
    ]

    public var fileName: String
    public var pane: MainWindowPane
    public var deckCount: Int

    public init(fileName: String, pane: MainWindowPane, deckCount: Int) {
        self.fileName = fileName
        self.pane = pane
        self.deckCount = deckCount
    }
}
