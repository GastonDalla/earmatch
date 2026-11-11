import Foundation
import Testing
@testable import EarmatchCore

struct PracticeHistoryFileTests {
    private let directory: URL
    private let file: PracticeHistoryFile

    init() throws {
        directory = FileManager.default.temporaryDirectory
            .appending(path: "earmatch-practice-tests-\(UUID().uuidString)", directoryHint: .isDirectory)
            .appending(path: "Earmatch", directoryHint: .isDirectory)
        file = PracticeHistoryFile(url: directory.appending(path: "practice.json"))
    }

    private func removeScratch() {
        try? FileManager.default.removeItem(at: directory.deletingLastPathComponent())
    }

    private func contents() throws -> [String] {
        try FileManager.default.contentsOfDirectory(atPath: directory.path(percentEncoded: false)).sorted()
    }

    @Test func `a missing file loads an empty history and creates nothing`() {
        defer { removeScratch() }

        let loaded = file.load()

        #expect(loaded.history == PracticeHistory())
        #expect(loaded.quarantinedURL == nil)
        #expect(!FileManager.default.fileExists(atPath: directory.path(percentEncoded: false)))
    }

    @Test func `a saved history loads back unchanged`() throws {
        defer { removeScratch() }
        var history = PracticeHistory()
        history.record(PracticeHistoryTests.result(endingAt: Date(timeIntervalSince1970: 1_793_840_000)))
        history.record(PracticeHistoryTests.result(endingAt: Date(timeIntervalSince1970: 1_793_840_600), error: nil))

        try file.save(history)
        let loaded = file.load()

        #expect(loaded.history == history)
        #expect(loaded.quarantinedURL == nil)
        #expect(try contents() == ["practice.json"])
    }

    @Test func `a corrupt file starts fresh and is kept renamed`() throws {
        defer { removeScratch() }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let garbage = Data("{\"version\":1,\"mixes\":[{\"broken\"".utf8)
        try garbage.write(to: file.url)

        let loaded = file.load()

        #expect(loaded.history == PracticeHistory())
        let kept = try #require(loaded.quarantinedURL)
        #expect(kept.deletingLastPathComponent().standardizedFileURL == directory.standardizedFileURL)
        #expect(kept.lastPathComponent.hasPrefix("practice.corrupt-"))
        #expect(try Data(contentsOf: kept) == garbage)
        #expect(!FileManager.default.fileExists(atPath: file.url.path(percentEncoded: false)))

        try file.save(PracticeHistory())
        #expect(try contents().count == 2)
    }

    @Test func `a file from a newer format is kept renamed, not overwritten`() throws {
        defer { removeScratch() }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let future = Data("{\"version\":99,\"mixes\":[]}".utf8)
        try future.write(to: file.url)

        let loaded = file.load()

        #expect(loaded.history == PracticeHistory())
        #expect(try Data(contentsOf: try #require(loaded.quarantinedURL)) == future)
    }
}
