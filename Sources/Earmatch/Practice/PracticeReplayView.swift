import Accessibility
import EarmatchCore
import SwiftUI

struct PracticeReplayView: View {
    let trace: PracticeTrace
    @State private var playbackStart: Date?

    private var playbackSeconds: Double { min(max(trace.durationSeconds / 6, 4), 12) }
    private var isPlaying: Bool { playbackStart != nil }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.small) {
            HStack(spacing: Spacing.small) {
                PracticeLabel(text: PracticeStrings.replay)
                Spacer(minLength: Spacing.small)
                ActionButton(kind: .pill,
                             accessibilityText: isPlaying ? PracticeStrings.stopHint : PracticeStrings.playHint,
                             action: toggle) {
                    Label(isPlaying ? PracticeStrings.stop : PracticeStrings.play,
                          systemImage: isPlaying ? "stop.fill" : "play.fill")
                        .contentTransition(.opacity)
                }
            }
            TimelineView(.animation(minimumInterval: nil, paused: !isPlaying)) { timeline in
                ReplayWell(trace: trace, cursor: cursor(at: timeline.date), isPlaying: isPlaying)
            }
        }
        .task(id: playbackStart) {
            guard let start = playbackStart else { return }
            try? await Task.sleep(for: .seconds(playbackSeconds))
            guard !Task.isCancelled, playbackStart == start else { return }
            playbackStart = nil
        }
        .onChange(of: trace.mixID) { playbackStart = nil }
    }

    private func toggle() {
        playbackStart = isPlaying ? nil : Date()
    }

    private func cursor(at date: Date) -> Double? {
        guard let playbackStart else { return trace.worstSample?.seconds }
        let progress = min(max(date.timeIntervalSince(playbackStart) / playbackSeconds, 0), 1)
        return progress * trace.durationSeconds
    }
}

private struct ReplayWell: View {
    let trace: PracticeTrace
    let cursor: Double?
    let isPlaying: Bool

    var body: some View {
        let error = cursor.flatMap(trace.errorMilliseconds(at:))
        VStack(spacing: Spacing.small) {
            HStack(spacing: Spacing.medium) {
                PracticeErrorMeter(errorMilliseconds: error, isDimmed: !isMatched(at: cursor))
                    .frame(height: 30)
                Text(error.map { PracticeStrings.milliseconds($0, signed: true) } ?? PracticeStrings.noValue)
                    .font(.system(size: 15, weight: .semibold, design: .monospaced))
                    .foregroundStyle(error.map(PracticeErrorMeter.color(for:)) ?? WindowTheme.secondaryText)
                    .frame(width: 70, alignment: .trailing)
            }
            Canvas { context, size in
                PracticeTraceDrawing(trace: trace, cursor: cursor).draw(in: &context, size: size)
            }
            .frame(height: 56)
            HStack {
                Text(PracticeStrings.duration(0))
                Spacer()
                Text(caption)
                Spacer()
                Text(PracticeStrings.duration(trace.durationSeconds))
            }
            .font(.system(size: 10.5).monospacedDigit())
            .foregroundStyle(WindowTheme.sectionText)
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(WindowTheme.well))
        .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(WindowTheme.hairline))
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(PracticeStrings.replayAccessibility(
            duration: trace.durationSeconds, matchedAt: trace.matchedAtSeconds,
            worst: trace.worstSample.map { ($0.seconds, $0.errorMilliseconds) }))
        .accessibilityValue(cursor.map { PracticeStrings.replayValue(seconds: $0, error: error) } ?? "")
        .accessibilityChartDescriptor(PracticeTraceChart(trace: trace))
    }

    private var caption: String {
        guard let cursor else { return "" }
        return isPlaying ? PracticeStrings.replayPosition(cursor, of: trace.durationSeconds) : PracticeStrings.worstAt(cursor)
    }

    private func isMatched(at seconds: Double?) -> Bool {
        guard let seconds, let matched = trace.matchedAtSeconds else { return false }
        return seconds >= matched
    }
}

private struct PracticeTraceDrawing {
    let trace: PracticeTrace
    let cursor: Double?

    func draw(in context: inout GraphicsContext, size: CGSize) {
        let duration = max(trace.durationSeconds, 0.1)
        let matched = trace.matchedAtSeconds
        let worst = trace.worstSample.map { abs($0.errorMilliseconds) } ?? 0
        let scale = matched == nil ? 100 : min(max(worst * 1.25, PracticeErrorMeter.inPhase * 2), 100)
        let midY = size.height / 2
        func x(_ seconds: Double) -> CGFloat { CGFloat(seconds / duration) * size.width }
        func y(_ milliseconds: Double) -> CGFloat {
            midY - CGFloat(min(max(milliseconds, -scale), scale) / scale) * (size.height / 2 - 2)
        }

        let bandTop = y(PracticeErrorMeter.inPhase)
        context.fill(Path(CGRect(x: 0, y: bandTop, width: size.width, height: y(-PracticeErrorMeter.inPhase) - bandTop)),
                     with: .color(WindowTheme.deck.opacity(0.12)))
        context.fill(Path(CGRect(x: 0, y: midY - 0.5, width: size.width, height: 1)), with: .color(.white.opacity(0.28)))

        if let matched {
            let line = x(matched)
            context.fill(Path(CGRect(x: 0, y: 0, width: line, height: size.height)), with: .color(.black.opacity(0.35)))
            var marker = Path()
            marker.move(to: CGPoint(x: line, y: 0))
            marker.addLine(to: CGPoint(x: line, y: size.height))
            context.stroke(marker, with: .color(WindowTheme.accent.opacity(0.7)), style: StrokeStyle(lineWidth: 1, dash: [2, 2]))
        }

        let before = path(before: matched ?? .infinity, x: x, y: y, width: size.width, matchedSide: false)
        let after = path(before: matched ?? .infinity, x: x, y: y, width: size.width, matchedSide: true)
        context.stroke(before, with: .color(WindowTheme.secondaryText.opacity(0.6)), lineWidth: 1)
        context.stroke(after, with: .color(WindowTheme.deck), style: StrokeStyle(lineWidth: 1.5, lineJoin: .round))

        if let worstSample = trace.worstSample, cursor != worstSample.seconds {
            let point = CGPoint(x: x(worstSample.seconds), y: y(worstSample.errorMilliseconds))
            context.fill(Path(ellipseIn: CGRect(x: point.x - 2.5, y: point.y - 2.5, width: 5, height: 5)),
                         with: .color(WindowTheme.downbeat))
        }

        guard let cursor, let error = trace.errorMilliseconds(at: cursor) else { return }
        let cursorX = x(cursor)
        context.fill(Path(CGRect(x: cursorX - 0.75, y: 0, width: 1.5, height: size.height)), with: .color(.white.opacity(0.85)))
        let dot = CGRect(x: cursorX - 4, y: y(error) - 4, width: 8, height: 8)
        context.fill(Path(ellipseIn: dot), with: .color(PracticeErrorMeter.color(for: error)))
        context.stroke(Path(ellipseIn: dot), with: .color(.white), lineWidth: 1.2)
    }

    private func path(before matched: Double, x: (Double) -> CGFloat, y: (Double) -> CGFloat,
                      width: CGFloat, matchedSide: Bool) -> Path {
        let samples = trace.samples.filter { ($0.seconds >= matched) == matchedSide }
        let bucketWidth = max(1, Int((Double(samples.count) / Double(max(width, 1))).rounded(.up)))
        var path = Path()
        var previous: Double?
        var index = 0
        while index < samples.count {
            let bucket = samples[index ..< min(index + bucketWidth, samples.count)]
            index += bucketWidth
            guard let low = bucket.min(by: { $0.errorMilliseconds < $1.errorMilliseconds }),
                  let high = bucket.max(by: { $0.errorMilliseconds < $1.errorMilliseconds })
            else { continue }
            let ordered = low == high ? [low] : low.seconds <= high.seconds ? [low, high] : [high, low]
            for sample in ordered {
                let point = CGPoint(x: x(sample.seconds), y: y(sample.errorMilliseconds))
                if let previous, sample.seconds - previous <= 0.5 {
                    path.addLine(to: point)
                } else {
                    path.move(to: point)
                }
                previous = sample.seconds
            }
        }
        return path
    }
}

private struct PracticeTraceChart: AXChartDescriptorRepresentable {
    let trace: PracticeTrace

    func makeChartDescriptor() -> AXChartDescriptor {
        let values = trace.samples.map(\.errorMilliseconds)
        let bound = max(values.map(abs).max() ?? 0, PracticeErrorMeter.inPhase)
        let xAxis = AXNumericDataAxisDescriptor(title: PracticeStrings.timeAxis, range: 0 ... max(trace.durationSeconds, 0.1), gridlinePositions: []) {
            PracticeStrings.duration($0)
        }
        let yAxis = AXNumericDataAxisDescriptor(title: PracticeStrings.phaseErrorAxis, range: -bound ... bound, gridlinePositions: [0]) {
            PracticeStrings.milliseconds($0, signed: true)
        }
        let series = AXDataSeriesDescriptor(name: PracticeStrings.phaseErrorAxis, isContinuous: true, dataPoints: trace.samples.map {
            AXDataPoint(x: $0.seconds, y: $0.errorMilliseconds)
        })
        return AXChartDescriptor(title: PracticeStrings.replay, summary: nil, xAxis: xAxis, yAxis: yAxis,
                                 additionalAxes: [], series: [series])
    }
}
