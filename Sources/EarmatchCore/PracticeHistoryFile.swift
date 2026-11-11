import Foundation

public struct PracticeHistoryFile: Sendable {
    public static let formatVersion = 1

    public struct Loaded: Sendable {
        public var history: PracticeHistory
        public var quarantinedURL: URL? = nil
        public var problem: String? = nil
    }

    private struct Envelope: Codable {
        var version: Int
        var mixes: [PracticeMix]
        var lastTrace: PracticeTrace?
    }

    private struct UnsupportedVersion: Error, CustomStringConvertible {
        let version: Int
        var description: String { "unsupported practice history version \(version)" }
    }

    public let url: URL

    public init(url: URL) {
        self.url = url
    }

    public func load(now: Date = Date()) -> Loaded {
        let path = url.path(percentEncoded: false)
        guard FileManager.default.fileExists(atPath: path) else { return Loaded(history: PracticeHistory()) }
        let data: Data
        do {
            data = try Data(contentsOf: url)
        } catch {
            return Loaded(history: PracticeHistory(), problem: "unreadable: \(error.localizedDescription)")
        }
        do {
            let envelope = try JSONDecoder().decode(Envelope.self, from: data)
            guard envelope.version == Self.formatVersion else { throw UnsupportedVersion(version: envelope.version) }
            return Loaded(history: PracticeHistory(mixes: envelope.mixes, lastTrace: envelope.lastTrace))
        } catch {
            let problem = String(describing: error)
            do {
                let kept = quarantineURL(at: now)
                try FileManager.default.moveItem(at: url, to: kept)
                return Loaded(history: PracticeHistory(), quarantinedURL: kept, problem: problem)
            } catch let moveError {
                return Loaded(history: PracticeHistory(), problem: "\(problem); not renamed: \(moveError.localizedDescription)")
            }
        }
    }

    public func save(_ history: PracticeHistory) throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        let envelope = Envelope(version: Self.formatVersion, mixes: history.mixes, lastTrace: history.lastTrace)
        try encoder.encode(envelope).write(to: url, options: .atomic)
    }

    private func quarantineURL(at date: Date) -> URL {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        formatter.dateFormat = "yyyyMMdd'T'HHmmss'Z'"
        let directory = url.deletingLastPathComponent()
        let stem = "\(url.deletingPathExtension().lastPathComponent).corrupt-\(formatter.string(from: date))"
        let ext = url.pathExtension.isEmpty ? "" : ".\(url.pathExtension)"
        var candidate = directory.appending(path: stem + ext)
        var counter = 2
        while FileManager.default.fileExists(atPath: candidate.path(percentEncoded: false)) {
            candidate = directory.appending(path: "\(stem)-\(counter)\(ext)")
            counter += 1
        }
        return candidate
    }
}
