import EarmatchCore
import SwiftUI

struct PracticeProgressChart: View {
    let days: [PracticeDay]
    var chartHeight: CGFloat = 58

    var body: some View {
        let measured = days.compactMap(\.medianPhaseErrorMilliseconds)
        VStack(alignment: .leading, spacing: Spacing.small) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.small) {
                PracticeLabel(text: PracticeStrings.progress)
                Spacer(minLength: Spacing.small)
                if let first = measured.first, let last = measured.last, measured.count > 1 {
                    Text(PracticeStrings.trend(from: first, to: last))
                        .font(TypeScale.status)
                        .monospacedDigit()
                        .foregroundStyle(last.rounded() < first.rounded() ? WindowTheme.accent : WindowTheme.secondaryText)
                        .lineLimit(1)
                        .minimumScaleFactor(0.85)
                }
            }
            VStack(spacing: 6) {
                Canvas { context, size in
                    PracticeProgressDrawing(values: days.map(\.medianPhaseErrorMilliseconds)).draw(in: &context, size: size)
                }
                .frame(height: chartHeight)
                HStack {
                    Text(days.first.map { PracticeStrings.shortDay($0.day) } ?? "")
                    Spacer()
                    Text(PracticeStrings.medianPerDay)
                    Spacer()
                    Text(days.last.map { PracticeStrings.shortDay($0.day) } ?? "")
                }
                .font(.system(size: 10.5))
                .foregroundStyle(WindowTheme.sectionText)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            }
            .padding(12)
            .surface(cornerRadius: 10, fill: WindowTheme.well, stroke: WindowTheme.hairline)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(PracticeStrings.progressAccessibility(days.map { (PracticeStrings.shortDay($0.day), $0.medianPhaseErrorMilliseconds) }))
    }
}

private struct PracticeProgressDrawing {
    let values: [Double?]

    func draw(in context: inout GraphicsContext, size: CGSize) {
        guard !values.isEmpty else { return }
        let top = max(values.compactMap { $0 }.max() ?? 0, PracticeErrorMeter.inPhase * 2) * 1.1
        let slots = CGFloat(max(values.count, 7))
        let slot = size.width / slots
        let barWidth = min(slot * 0.62, 18)
        let origin = size.width - CGFloat(values.count) * slot
        func y(_ milliseconds: Double) -> CGFloat { size.height - CGFloat(milliseconds / top) * size.height }

        let threshold = y(PracticeErrorMeter.inPhase)
        var dashes = Path()
        dashes.move(to: CGPoint(x: 0, y: threshold))
        dashes.addLine(to: CGPoint(x: size.width, y: threshold))
        context.stroke(dashes, with: .color(WindowTheme.deck.opacity(0.45)), style: StrokeStyle(lineWidth: 1, dash: [3, 3]))
        context.fill(Path(CGRect(x: 0, y: size.height - 1, width: size.width, height: 1)), with: .color(WindowTheme.hairline))

        for (index, value) in values.enumerated() {
            let centre = origin + (CGFloat(index) + 0.5) * slot
            guard let value else {
                context.fill(Path(ellipseIn: CGRect(x: centre - 2, y: size.height - 6, width: 4, height: 4)),
                             with: .color(WindowTheme.sectionText))
                continue
            }
            let height = max(size.height - y(value), 3)
            let bar = CGRect(x: centre - barWidth / 2, y: size.height - height, width: barWidth, height: height)
            let isLatest = index == values.count - 1
            let colour = value <= PracticeErrorMeter.inPhase
                ? (isLatest ? WindowTheme.deck : WindowTheme.deck.opacity(0.55))
                : (isLatest ? WindowTheme.master : WindowTheme.masterDim)
            context.fill(Path(roundedRect: bar, cornerRadius: 3), with: .color(colour))
        }
    }
}
