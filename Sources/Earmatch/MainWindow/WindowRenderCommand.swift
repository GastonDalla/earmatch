import AppKit
import EarmatchCore
import SwiftUI

enum WindowRenderCommand {
    static func run(_ request: WindowRenderRequest) -> Int32 {
        NSApplication.shared.setActivationPolicy(.prohibited)
        do {
            try FileManager.default.createDirectory(at: request.directory, withIntermediateDirectories: true)
        } catch {
            report("Cannot create \(request.directory.path(percentEncoded: false)): \(error.localizedDescription)")
            return 1
        }
        let overview = WindowRenderSample.practiceOverview(now: Date(), calendar: .autoupdatingCurrent)
        for shot in WindowRenderShot.all {
            let url = request.directory.appending(path: shot.fileName, directoryHint: .notDirectory)
            guard let png = render(shot, size: request.size, overview: overview) else {
                report("Rendering \(shot.fileName) failed")
                return 1
            }
            do {
                try png.write(to: url, options: .atomic)
            } catch {
                report("Cannot write \(url.path(percentEncoded: false)): \(error.localizedDescription)")
                return 1
            }
            print(url.path(percentEncoded: false))
        }
        return 0
    }

    private static func render(_ shot: WindowRenderShot, size: CGSize, overview: PracticeOverview) -> Data? {
        let model = MainWindowModel(sample: WindowRenderSample.settings, pane: shot.pane,
                                    context: WindowRenderSample.statusContext(deckCount: shot.deckCount))
        let live = LiveDecksModel()
        live.apply(WindowRenderSample.decks(count: shot.deckCount), at: 0)
        let practice = PracticeController(sample: overview, live: WindowRenderSample.practiceLive)
        let view = MainWindowView(model: model, live: live, practice: practice)
            .overlay(alignment: .topLeading) { WindowControlsSketch() }
            .frame(width: size.width, height: size.height)
        let renderer = ImageRenderer(content: view)
        renderer.scale = WindowRenderShot.scale
        renderer.proposedSize = ProposedViewSize(size)
        renderer.isOpaque = true
        guard let image = renderer.cgImage else { return nil }
        return NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:])
    }

    private static func report(_ message: String) {
        FileHandle.standardError.write(Data((message + "\n").utf8))
    }
}

private struct WindowControlsSketch: View {
    private static let colours = [
        Color(red: 1, green: 0.373, blue: 0.341),
        Color(red: 0.996, green: 0.737, blue: 0.180),
        Color(red: 0.157, green: 0.784, blue: 0.251),
    ]

    var body: some View {
        HStack(spacing: 8) {
            ForEach(Self.colours.indices, id: \.self) { index in
                Circle()
                    .fill(Self.colours[index])
                    .overlay { Circle().strokeBorder(Color.black.opacity(0.18), lineWidth: 0.5) }
                    .frame(width: 12, height: 12)
            }
        }
        .padding(.leading, 14)
        .padding(.top, 8)
        .accessibilityHidden(true)
    }
}
