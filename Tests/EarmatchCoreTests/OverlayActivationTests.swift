import CoreGraphics
import Testing
import EarmatchCore

@Suite struct OverlayActivationTests {
    let active: RekordboxLayout

    init() throws {
        active = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-loaded.json"))
    }

    @Test func activeOnlyWhenEverythingLinesUp() {
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: active, isFrontmost: true) == .active)
    }

    @Test func permissionComesFirst() {
        #expect(OverlayActivation.evaluate(isTrusted: false, isRunning: true, layout: active, isFrontmost: true) == .accessibilityMissing)
    }

    @Test func waitsForRekordboxAndItsWindow() {
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: false, layout: nil, isFrontmost: false) == .rekordboxNotRunning)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: nil, isFrontmost: true) == .mainWindowNotFound)
    }

    @Test func pausesWhenRekordboxIsNotInFront() {
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: active, isFrontmost: false) == .rekordboxNotFrontmost)
    }

    @Test func asksForPerformanceModeAndTheTwoDeckLayout() throws {
        let export = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-export-mode.json"))
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: export, isFrontmost: true) == .wrongMode(.export))

        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        let index = try #require(snapshot.elements.firstIndex { $0.value == "2Deck Horizontal" })
        snapshot.elements[index].value = "Browse"
        let browse = RekordboxLayout.locate(in: snapshot)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: browse, isFrontmost: true) == .wrongLayout("Browse"))
    }

    @Test func waitsWhenTheWaveformCannotBeFound() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        snapshot.elements.removeAll { $0.help?.hasPrefix("Matching") == true || $0.help?.hasPrefix("Capture") == true }
        let layout = RekordboxLayout.locate(in: snapshot)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: layout, isFrontmost: true) == .decksNotFound)
    }

    @Test func everyStateExplainsItselfToTheUser() {
        let states: [OverlayActivation] = [.accessibilityMissing, .rekordboxNotRunning, .mainWindowNotFound, .rekordboxNotFrontmost, .wrongMode(.export), .wrongLayout("Browse"), .wrongLayout(nil), .decksNotFound, .active]
        #expect(states.allSatisfy { !$0.message.isEmpty })
    }
}
