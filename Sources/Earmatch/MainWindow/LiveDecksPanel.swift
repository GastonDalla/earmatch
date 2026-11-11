import SwiftUI
import EarmatchCore

struct LiveDecksPanel: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let line = StatusText.liveDecks(model.status, snapshot: live.snapshot)
        let isConnected = line.tone == .live
        PaneCard(title: WindowText.liveDecks) {
            ZStack(alignment: .topLeading) {
                if isConnected {
                    DeckGrid(decks: live.decks, showsMeter: model.settings.isPhaseMeterEnabled)
                        .transition(.opacity)
                } else {
                    EmptyDecks().transition(.opacity)
                }
            }
            CardStatusRow(line: line)
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: isConnected)
    }
}

private struct DeckGrid: View {
    let decks: [LiveDeckSnapshot]
    let showsMeter: Bool

    var body: some View {
        let columns = decks.count > 2 ? 2 : 1
        let rows = stride(from: 0, to: decks.count, by: columns).map { Array(decks[$0 ..< min($0 + columns, decks.count)]) }
        Grid(horizontalSpacing: Spacing.medium, verticalSpacing: 14) {
            ForEach(rows, id: \.first?.number) { row in
                GridRow {
                    ForEach(row, id: \.number) { deck in
                        LiveDeckRow(deck: deck, showsMeter: showsMeter)
                    }
                    if row.count < columns {
                        Color.clear.gridCellUnsizedAxes([.horizontal, .vertical])
                    }
                }
            }
        }
    }
}

private struct LiveDeckRow: View {
    let deck: LiveDeckSnapshot
    let showsMeter: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.small) {
            HStack(spacing: Spacing.small) {
                DeckBadge(number: deck.number)
                Text(deck.title ?? LiveDecksText.noTrack)
                    .font(TypeScale.bodyStrong)
                    .foregroundStyle(deck.title == nil ? WindowTheme.secondaryText : WindowTheme.primaryText)
                    .lineLimit(1)
                    .truncationMode(.tail)
                Spacer(minLength: Spacing.small)
                if deck.isMaster { SyncMasterChip() }
            }
            ZStack {
                RoundedRectangle(cornerRadius: 6, style: .continuous).fill(Color.black)
                if !showsMeter {
                    MeterCaption(text: LiveDecksText.meterOff)
                } else if let meter = deck.meter {
                    PhaseMeterCanvas(display: meter)
                } else {
                    MeterCaption(text: LiveDecksText.readingGrid)
                }
            }
            .frame(height: 60)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(accessibilityText)
    }

    private var accessibilityText: String {
        var parts = [MeterSpeech.deck(deck.number), deck.title ?? MeterSpeech.noTrack]
        if deck.isMaster { parts.append(MeterSpeech.syncMaster) }
        if showsMeter, let meter = deck.meter, let own = meter.rows.first(where: { $0.role == .deck }) {
            if let beat = own.beatInBar {
                parts.append(MeterSpeech.beat(beat))
                if own.isOnMastersBeat == false { parts.append(MeterSpeech.outOfPhase(withDeck: meter.masterPlayer ?? 0)) }
            }
            parts.append(MeterSpeech.counter(own.counter))
        }
        return parts.joined(separator: ", ")
    }
}

private struct EmptyDecks: View {
    var body: some View {
        HStack(spacing: Spacing.medium) {
            HStack(spacing: Spacing.tight) {
                DeckBadge(number: 1, isDimmed: true)
                DeckBadge(number: 2, isDimmed: true)
            }
            .accessibilityHidden(true)
            Text(LiveDecksText.emptyHint)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
    }
}

private struct MeterCaption: View {
    let text: String

    var body: some View {
        Text(text).font(TypeScale.status).foregroundStyle(WindowTheme.sectionText)
    }
}

private struct DeckBadge: View {
    let number: Int
    var isDimmed = false

    var body: some View {
        Text(verbatim: "\(number)")
            .font(.system(size: 12, weight: .bold, design: .rounded))
            .monospacedDigit()
            .foregroundStyle(isDimmed ? WindowTheme.sectionText : WindowTheme.primaryText)
            .frame(width: 22, height: 22)
            .surface(cornerRadius: 6, fill: WindowTheme.well, stroke: Color.white.opacity(isDimmed ? 0.06 : 0.14))
    }
}

private struct SyncMasterChip: View {
    var body: some View {
        Text(LiveDecksText.syncMasterChip)
            .font(.system(size: 10, weight: .bold))
            .tracking(1.2)
            .foregroundStyle(WindowTheme.master)
            .padding(.horizontal, Spacing.small)
            .padding(.vertical, 3)
            .surface(cornerRadius: 6, fill: WindowTheme.master.opacity(0.14), stroke: WindowTheme.master.opacity(0.35))
    }
}

private enum LiveDecksText {
    static var noTrack: String { L10n.text("No track loaded") }
    static var meterOff: String { L10n.text("Phase meter off") }
    static var readingGrid: String { L10n.text("Reading the beat grid…") }
    static var emptyHint: String { L10n.text("Both decks show here with their phase meters while rekordbox plays in front.") }
    static var syncMasterChip: String { L10n.text("SYNC MASTER") }
}
