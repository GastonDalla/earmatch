import SwiftUI

struct PracticeErrorMeter: View {
    static let range = 50.0
    static let inPhase = 10.0

    let errorMilliseconds: Double?
    var isDimmed = false

    var body: some View {
        Canvas { context, size in
            PracticeErrorMeterDrawing(errorMilliseconds: errorMilliseconds, isDimmed: isDimmed).draw(in: &context, size: size)
        }
        .background(RoundedRectangle(cornerRadius: 6, style: .continuous).fill(Color.black))
        .accessibilityHidden(true)
    }

    static func color(for errorMilliseconds: Double) -> Color {
        abs(errorMilliseconds) <= inPhase ? WindowTheme.deck : WindowTheme.downbeat
    }
}

private struct PracticeErrorMeterDrawing {
    let errorMilliseconds: Double?
    let isDimmed: Bool

    func draw(in context: inout GraphicsContext, size: CGSize) {
        let inset: CGFloat = 14
        let track = CGRect(x: inset, y: 0, width: size.width - 2 * inset, height: size.height)
        let midY = size.height / 2 + 4
        let range = PracticeErrorMeter.range
        func x(_ milliseconds: Double) -> CGFloat {
            track.midX + CGFloat(min(max(milliseconds, -range), range) / range) * track.width / 2
        }

        let band = CGRect(x: x(-PracticeErrorMeter.inPhase), y: midY - 8,
                          width: x(PracticeErrorMeter.inPhase) - x(-PracticeErrorMeter.inPhase), height: 16)
        context.fill(Path(roundedRect: band, cornerRadius: 3), with: .color(WindowTheme.deck.opacity(0.14)))
        context.fill(Path(CGRect(x: track.minX, y: midY - 0.75, width: track.width, height: 1.5)),
                     with: .color(WindowTheme.deckDim))

        var ticks = Path()
        for milliseconds in stride(from: -range, through: range, by: 12.5) where milliseconds != 0 {
            let height: CGFloat = milliseconds.truncatingRemainder(dividingBy: 25) == 0 ? 8 : 4
            ticks.addRect(CGRect(x: x(milliseconds) - 0.6, y: midY - height / 2, width: 1.2, height: height))
        }
        context.fill(ticks, with: .color(WindowTheme.deckDim))
        context.fill(Path(CGRect(x: track.midX - 1.25, y: midY - 11, width: 2.5, height: 22)), with: .color(.white))

        let caption = Font.system(size: 7.5, weight: .heavy)
        context.draw(Text(PracticeStrings.behind).font(caption).foregroundStyle(WindowTheme.sectionText),
                     at: CGPoint(x: track.minX, y: 7), anchor: .leading)
        context.draw(Text(PracticeStrings.ahead).font(caption).foregroundStyle(WindowTheme.sectionText),
                     at: CGPoint(x: track.maxX, y: 7), anchor: .trailing)

        guard let errorMilliseconds else { return }
        let marker = CGRect(x: x(errorMilliseconds) - 7, y: midY - 6, width: 14, height: 12)
        let colour = PracticeErrorMeter.color(for: errorMilliseconds)
        context.fill(Path(roundedRect: marker, cornerRadius: 2), with: .color(colour.opacity(isDimmed ? 0.45 : 1)))
        if abs(errorMilliseconds) > range {
            let edge = errorMilliseconds > 0 ? marker.maxX + 3 : marker.minX - 3
            let direction: CGFloat = errorMilliseconds > 0 ? 1 : -1
            var arrow = Path()
            arrow.move(to: CGPoint(x: edge, y: midY - 4))
            arrow.addLine(to: CGPoint(x: edge + 4 * direction, y: midY))
            arrow.addLine(to: CGPoint(x: edge, y: midY + 4))
            arrow.closeSubpath()
            context.fill(arrow, with: .color(colour))
        }
    }
}
