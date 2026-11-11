import Testing
import EarmatchCore

@Suite struct BPMDisplayFormatterTests {
    @Test(arguments: [
        ("135.00", 135.0),
        (" 124.00", 124.0),
        ("127.96", 127.96),
        ("99.5", 99.5),
    ])
    func parsesRekordboxBPMStrings(raw: String, expected: Double) {
        #expect(BPMDisplayFormatter.parse(raw) == expected)
    }

    @Test(arguments: ["", "   ", "---", "BPM"])
    func rejectsTextThatIsNotABPM(raw: String) {
        #expect(BPMDisplayFormatter.parse(raw) == nil)
    }
}
