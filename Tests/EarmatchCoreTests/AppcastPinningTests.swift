import Foundation
import Testing

@Suite struct AppcastPinningTests {
    static let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
    static let releases = "https://github.com/GastonDalla/earmatch/releases/download"

    static func item(version: String, build: String, urls: [String]) -> String {
        let enclosures = urls.map { "<enclosure url=\"\($0)\" length=\"1\" type=\"application/octet-stream\" sparkle:edSignature=\"sig\"/>" }
        return """
            <item>
                <title>\(version)</title>
                <sparkle:version>\(build)</sparkle:version>
                <sparkle:shortVersionString>\(version)</sparkle:shortVersionString>
                \(enclosures.joined(separator: "\n            "))
            </item>
        """
    }

    static func feed(_ items: [String], header: String = "") -> String {
        """
        <?xml version="1.0" encoding="utf-8"?>
        \(header)<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
            <channel>
        \(items.joined(separator: "\n"))
            </channel>
        </rss>
        """
    }

    static func pin(_ xml: String) throws -> (output: String, status: Int32) {
        let dir = FileManager.default.temporaryDirectory.appending(path: "appcast-pin-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let file = dir.appending(path: "appcast.xml")
        try xml.write(to: file, atomically: true, encoding: .utf8)
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/bash")
        process.arguments = [
            root.appending(path: "scripts/pin-appcast-urls.sh").path(percentEncoded: false),
            file.path(percentEncoded: false),
            "GastonDalla/earmatch",
        ]
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        try process.run()
        process.waitUntilExit()
        return (try String(contentsOf: file, encoding: .utf8), process.terminationStatus)
    }

    @Test func anOlderReleaseRewrittenToTheNewTagPointsBackAtItsOwnTag() throws {
        let newest = Self.item(version: "0.1.1", build: "2", urls: ["\(Self.releases)/v0.1.1/Earmatch-0.1.1.dmg", "\(Self.releases)/v0.1.1/Earmatch2-1.delta"])
        let older = Self.item(version: "0.1.0", build: "1", urls: ["\(Self.releases)/v0.1.1/Earmatch-0.1.0.dmg"])
        let result = try Self.pin(Self.feed([newest, older]))
        #expect(result.status == 0)
        #expect(result.output == Self.feed([newest, Self.item(version: "0.1.0", build: "1", urls: ["\(Self.releases)/v0.1.0/Earmatch-0.1.0.dmg"])]))
    }

    @Test func aFeedWhoseItemsAlreadyUseTheirOwnTagsStaysByteForByteTheSame() throws {
        let xml = Self.feed([
            Self.item(version: "0.1.1", build: "2", urls: ["\(Self.releases)/v0.1.1/Earmatch-0.1.1.dmg", "\(Self.releases)/v0.1.1/Earmatch2-1.delta"]),
            Self.item(version: "0.1.0", build: "1", urls: ["\(Self.releases)/v0.1.0/Earmatch-0.1.0.dmg"]),
        ])
        let result = try Self.pin(xml)
        #expect(result.status == 0)
        #expect(result.output == xml)
    }

    @Test func downloadsHostedOutsideTheRepositoryReleasesAreLeftAlone() throws {
        let xml = Self.feed([Self.item(version: "0.1.0", build: "1", urls: ["https://earmatch.gastondalla.com/downloads/Earmatch-0.1.0.dmg"])])
        let result = try Self.pin(xml)
        #expect(result.status == 0)
        #expect(result.output == xml)
    }

    @Test func aSignedFeedIsRefusedAndLeftUntouched() throws {
        let xml = Self.feed(
            [Self.item(version: "0.1.0", build: "1", urls: ["\(Self.releases)/v0.1.1/Earmatch-0.1.0.dmg"])],
            header: "<!-- sparkle-signatures:\nedSignature: abc\nlength: 1\n-->\n"
        )
        let result = try Self.pin(xml)
        #expect(result.status == 1)
        #expect(result.output == xml)
    }
}
