import CoreGraphics

public struct WaveformOverlayPlan: Equatable, Sendable {
    public struct Pane: Equatable, Sendable {
        public let deck: Int
        public let frame: CGRect
        public let waveformHeight: CGFloat
        public let source: CGRect
        public let showsMeter: Bool
    }

    public static let dividerWidth: CGFloat = 2
    static let bandShare: CGFloat = 0.6

    public let panel: CGRect?
    public let panes: [Pane]
    public let dividers: [CGRect]
    public let bands: [Int: CGRect]
    public let sideMeters: [Int: CGRect]
    public let allowsMasterWaveform: Bool

    public init?(layout: RekordboxLayout, mode: WaveformMode, showsMeters: Bool, bandSize: CGSize) {
        guard let arrangement = layout.arrangement, let stacked = layout.stackedWaveform else { return nil }
        var strips: [Int: CGRect] = [:]
        for deck in layout.decks { strips[deck.number] = deck.waveformStrip }
        let decks = layout.decks.map(\.number).filter { strips[$0] != nil }
        switch arrangement.orientation {
        case .horizontal:
            guard mode != .normal || showsMeters else { return nil }
            panel = stacked
            sideMeters = [:]
            allowsMasterWaveform = mode == .split
            if mode == .normal {
                panes = []
                dividers = []
                var bands: [Int: CGRect] = [:]
                for number in decks {
                    let strip = strips[number]!
                    let height = min(bandSize.height, (strip.height * Self.bandShare * 10).rounded() / 10)
                    bands[number] = CGRect(x: strip.midX - stacked.minX - bandSize.width / 2, y: strip.minY - stacked.minY,
                                           width: bandSize.width, height: height)
                }
                self.bands = bands
            } else {
                bands = [:]
                let rows = max(1, (arrangement.deckCount + 1) / 2)
                let width = (stacked.width - Self.dividerWidth) / 2
                let height = (stacked.height - CGFloat(rows - 1) * Self.dividerWidth) / CGFloat(rows)
                panes = decks.map { number in
                    let strip = strips[number]!
                    let column = (number - 1) % 2, row = (number - 1) / 2
                    let waveform = min(strip.height, height)
                    return Pane(deck: number,
                                frame: CGRect(x: CGFloat(column) * (width + Self.dividerWidth), y: CGFloat(row) * (height + Self.dividerWidth),
                                              width: width, height: height),
                                waveformHeight: waveform,
                                source: CGRect(x: strip.midX - width / 2, y: strip.minY, width: width, height: waveform),
                                showsMeter: true)
                }
                dividers = [CGRect(x: width, y: 0, width: Self.dividerWidth, height: stacked.height)]
                    + (1 ..< rows).map { CGRect(x: 0, y: CGFloat($0) * (height + Self.dividerWidth) - Self.dividerWidth,
                                                width: stacked.width, height: Self.dividerWidth) }
            }
        case .vertical:
            var meters: [Int: CGRect] = [:]
            if showsMeters {
                for deck in layout.decks {
                    guard let overview = deck.overviewFrame else { continue }
                    let width = min(bandSize.width, overview.width), height = min(bandSize.height, overview.height)
                    let x = overview.midX < stacked.midX ? overview.maxX - width : overview.minX
                    meters[deck.number] = CGRect(x: x, y: overview.midY - height / 2, width: width, height: height)
                }
            }
            guard mode == .hideBoth || !meters.isEmpty else { return nil }
            sideMeters = meters
            bands = [:]
            dividers = []
            allowsMasterWaveform = false
            if mode == .hideBoth {
                panel = stacked
                panes = decks.map { number in
                    Pane(deck: number, frame: strips[number]!.offsetBy(dx: -stacked.minX, dy: -stacked.minY),
                         waveformHeight: 0, source: .zero, showsMeter: false)
                }
            } else {
                panel = nil
                panes = []
            }
        }
    }
}
