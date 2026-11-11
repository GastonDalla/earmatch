import Foundation
import Testing

@Suite struct UpdateConfigurationTests {
    static let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()

    static func info() throws -> [String: Any] {
        let data = try Data(contentsOf: root.appending(path: "Support/Info.plist"))
        return try #require(try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any])
    }

    @Test func theFeedIsTheWebsiteAppcastOverHTTPS() throws {
        #expect(try Self.info()["SUFeedURL"] as? String == "https://earmatch.gastondalla.com/appcast.xml")
    }

    @Test func updateKeysComeFromTheBuildNotTheRepository() throws {
        let info = try Self.info()
        #expect(info["SUPublicEDKey"] == nil)
        #expect(info["SUEnableAutomaticChecks"] == nil)
    }
}
