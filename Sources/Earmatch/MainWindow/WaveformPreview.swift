import SwiftUI
import EarmatchCore

struct DeckPhases: Equatable {
    var one: Double
    var two: Double
}

struct WaveformPreview: View {
    let mode: WaveformMode
    let live: DeckPhases?
    let isPaused: Bool

    var body: some View {
        Group {
            if let live {
                WaveformSketch(mode: mode, phases: live)
            } else {
                TimelineView(.animation(paused: isPaused)) { timeline in
                    let clock = PreviewClock(date: timeline.date)
                    WaveformSketch(mode: mode, phases: DeckPhases(one: clock.deckBeats + 0.31, two: clock.masterBeats))
                }
            }
        }
        .accessibilityElement()
        .accessibilityLabel(accessibilityText)
    }

    private static let normalText = L10n.text("Preview: rekordbox's stacked waveforms")
    private static let splitText = L10n.text("Preview: each deck's waveform in its own half with its own playhead")
    private static let hiddenText = L10n.text("Preview: both waveforms hidden")

    private var accessibilityText: String {
        switch mode {
        case .normal: Self.normalText
        case .split: Self.splitText
        case .hideBoth: Self.hiddenText
        }
    }
}

private struct WaveformSketch: View {
    let mode: WaveformMode
    let phases: DeckPhases

    var body: some View {
        Canvas { context, size in
            let bounds = CGRect(origin: .zero, size: size)
            switch mode {
            case .normal:
                let top = CGRect(x: 0, y: 0, width: size.width, height: size.height / 2 - 1)
                let bottom = CGRect(x: 0, y: size.height / 2 + 1, width: size.width, height: size.height / 2 - 1)
                PreviewWaveformStrip.draw(in: &context, rect: top, beats: phases.one, color: WindowTheme.deck)
                PreviewWaveformStrip.draw(in: &context, rect: bottom, beats: phases.two, color: WindowTheme.deckTwo)
                PreviewWaveformStrip.playhead(in: &context, x: bounds.midX, from: 0, to: size.height)
            case .split:
                let gap: CGFloat = 6
                let left = CGRect(x: 0, y: 0, width: (size.width - gap) / 2, height: size.height)
                let right = CGRect(x: left.maxX + gap, y: 0, width: left.width, height: size.height)
                PreviewWaveformStrip.draw(in: &context, rect: left, beats: phases.one, color: WindowTheme.deck)
                PreviewWaveformStrip.draw(in: &context, rect: right, beats: phases.two, color: WindowTheme.deckTwo)
                PreviewWaveformStrip.playhead(in: &context, x: left.midX, from: 0, to: size.height)
                PreviewWaveformStrip.playhead(in: &context, x: right.midX, from: 0, to: size.height)
                context.fill(Path(CGRect(x: left.maxX + gap / 2 - 0.5, y: 4, width: 1, height: size.height - 8)),
                             with: .color(.white.opacity(0.14)))
            case .hideBoth:
                break
            }
        }
    }
}

enum PreviewWaveformStrip {
    static let pointsPerBeat: CGFloat = 26

    static func draw(in context: inout GraphicsContext, rect: CGRect, beats: Double, color: Color) {
        context.drawLayer { layer in
            layer.clip(to: Path(rect))
            let playheadX = rect.midX
            let midY = rect.midY

            var bars = Path()
            var x = rect.minX
            while x <= rect.maxX {
                let u = beats + Double((x - playheadX) / pointsPerBeat)
                let height = max(1.5, CGFloat(amplitude(at: u)) * rect.height * 0.86)
                bars.addRect(CGRect(x: x, y: midY - height / 2, width: 1.4, height: height))
                x += 2.4
            }
            layer.fill(bars, with: .color(color))

            let first = Int(floor(beats + Double((rect.minX - playheadX) / pointsPerBeat)))
            let last = Int(ceil(beats + Double((rect.maxX - playheadX) / pointsPerBeat)))
            for beat in first...last {
                let tickX = playheadX + CGFloat(Double(beat) - beats) * pointsPerBeat
                layer.fill(Path(CGRect(x: tickX - 0.5, y: rect.minY, width: 1, height: rect.height)),
                           with: .color(.white.opacity(0.22)))
                if beat % 4 == 0 {
                    var marker = Path()
                    marker.move(to: CGPoint(x: tickX - 3, y: rect.minY))
                    marker.addLine(to: CGPoint(x: tickX + 3, y: rect.minY))
                    marker.addLine(to: CGPoint(x: tickX, y: rect.minY + 4.5))
                    marker.closeSubpath()
                    layer.fill(marker, with: .color(WindowTheme.downbeat))
                }
            }
        }
    }

    static func playhead(in context: inout GraphicsContext, x: CGFloat, from top: CGFloat, to bottom: CGFloat) {
        context.fill(Path(CGRect(x: x - 1, y: top, width: 2, height: bottom - top)), with: .color(.white))
    }

    private static func amplitude(at beats: Double) -> Double {
        let fraction = beats - floor(beats)
        let kick = 0.22 + 0.78 * exp(-5 * fraction)
        let slice = Int(floor(beats * 10))
        return kick * (0.55 + 0.45 * noise(((slice % 40) + 40) % 40))
    }

    private static func noise(_ index: Int) -> Double {
        var hash = UInt32(truncatingIfNeeded: index &* 374_761_393 &+ 668_265_263)
        hash = (hash ^ (hash >> 13)) &* 1_274_126_177
        hash ^= hash >> 16
        return Double(hash & 0xFFFF) / 65535
    }
}
