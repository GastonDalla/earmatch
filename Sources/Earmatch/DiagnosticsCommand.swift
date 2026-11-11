import AppKit
import EarmatchCore

enum DiagnosticsCommand {
    static func dumpAccessibility() -> Int32 {
        guard AccessibilityReader.isTrusted else {
            log("Accessibility permission is missing for this process.")
            return 1
        }
        guard let rekordbox = AccessibilityReader.runningRekordbox() else {
            log("rekordbox is not running.")
            return 1
        }
        let start = Date()
        AccessibilityReader.applyGlobalTimeout()
        let read: RekordboxWindowSnapshot?
        do {
            read = try AccessibilityReader.snapshot(pid: rekordbox.processIdentifier)
        } catch is AccessibilityStall {
            log("rekordbox did not answer within \(AccessibilityReader.timeout) s; it is busy or not responding.")
            return 1
        } catch {
            log("Reading rekordbox's window failed: \(error)")
            return 1
        }
        guard let snapshot = read else {
            log("The rekordbox main window was not found.")
            return 1
        }
        let elapsedMs = Date().timeIntervalSince(start) * 1000
        print(fixtureJSON(snapshot))

        let layout = RekordboxLayout.locate(in: snapshot)
        log("rekordbox \(rekordbox.bundleVersion): \(snapshot.elements.count) elements read in \(String(format: "%.1f", elapsedMs)) ms")
        log("mode=\(layout.mode.rawValue) layout=\(layout.layoutName ?? "-") active=\(layout.isSupportedPerformanceLayout) waveform=\(describe(layout.stackedWaveform))")
        for deck in layout.decks {
            log("deck \(deck.number): title=\(deck.title ?? "-") bpm=\(deck.bpm.map { String($0) } ?? "-") header=\(describe(deck.headerBPMFrame)) jog=\(describe(deck.jogBPMFrame)) master=\(describe(deck.masterButtonFrame)) strip=\(describe(deck.waveformStrip)) markers=\(describe(deck.strip?.markerArea)) bars=\(deck.barsText ?? "-") elapsed=\(deck.elapsedSeconds.map { String($0) } ?? "-")")
        }
        return 0
    }

    static func fixtureJSON(_ snapshot: RekordboxWindowSnapshot) -> String {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        let lines = snapshot.elements.compactMap { (try? encoder.encode($0)).flatMap { String(data: $0, encoding: .utf8) } }
        let frame = (try? encoder.encode(snapshot.windowFrame)).flatMap { String(data: $0, encoding: .utf8) } ?? "null"
        return "{\"windowFrame\":\(frame),\"elements\":[\n" + lines.joined(separator: ",\n") + "\n]}"
    }

    private static func describe(_ rect: CGRect?) -> String {
        guard let rect else { return "-" }
        return "(\(Int(rect.minX)),\(Int(rect.minY)) \(Int(rect.width))x\(Int(rect.height)))"
    }

    private static func log(_ message: String) {
        FileHandle.standardError.write(Data((message + "\n").utf8))
    }
}

extension NSRunningApplication {
    var bundleVersion: String {
        guard let url = bundleURL, let bundle = Bundle(url: url) else { return "?" }
        return bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "?"
    }
}
