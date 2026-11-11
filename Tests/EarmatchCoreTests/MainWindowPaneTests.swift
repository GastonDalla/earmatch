import Foundation
import Testing
import EarmatchCore

@Suite struct MainWindowPaneTests {
    @Test func theSidebarListsLiveOverlayPracticeAndSetupInThatOrder() {
        #expect(MainWindowPane.allCases == [.live, .overlay, .practice, .setup])
    }

    @Test func commandOneToFourSelectsThePanesInSidebarOrder() {
        #expect(MainWindowPane.allCases.map(\.shortcutNumber) == [1, 2, 3, 4])
        #expect(["1", "2", "3", "4"].map(MainWindowPane.init(keyEquivalent:)) == [.live, .overlay, .practice, .setup])
    }

    @Test(arguments: ["0", "5", "9", "a", "", "12", "⌘1"])
    func otherKeysSelectNoPane(key: String) {
        #expect(MainWindowPane(keyEquivalent: key) == nil)
    }

    @Test func theArrowKeysMoveOnePaneAndStopAtTheEnds() {
        #expect(MainWindowPane.live.moved(by: 1) == .overlay)
        #expect(MainWindowPane.practice.moved(by: -1) == .overlay)
        #expect(MainWindowPane.live.moved(by: -1) == .live)
        #expect(MainWindowPane.setup.moved(by: 1) == .setup)
    }

    @Test func aMissingPermissionOpensSetupWhateverWasRemembered() {
        for stored in [nil, "live", "overlay", "practice", "setup", "unknown"] {
            #expect(MainWindowPane.initial(stored: stored, arePermissionsMissing: true) == .setup)
        }
    }

    @Test func withEveryPermissionTheLastPaneIsRemembered() {
        for pane in MainWindowPane.allCases {
            #expect(MainWindowPane.initial(stored: pane.rawValue, arePermissionsMissing: false) == pane)
        }
    }

    @Test(arguments: [nil, "", "Live", "browse", "4"])
    func anUnknownRememberedPaneOpensLive(stored: String?) {
        #expect(MainWindowPane.initial(stored: stored, arePermissionsMissing: false) == .live)
    }

    @Test func everyPaneTitleIsLocalized() {
        let titles = L10n.$override.withValue({ "⟦\($0)⟧" }) { MainWindowPane.allCases.map(\.title) }
        #expect(titles == ["⟦Live⟧", "⟦Overlay⟧", "⟦Practice⟧", "⟦Setup⟧"])
    }
}

@Suite struct MainWindowStatusTests {
    @Test func aMissingPermissionWinsOverEverythingElse() {
        #expect(MainWindowStatus(arePermissionsMissing: true, isEnabled: true, activation: .active) == .needsPermission)
        #expect(MainWindowStatus(arePermissionsMissing: true, isEnabled: false, activation: .active) == .needsPermission)
    }

    @Test func turnedOffEarmatchReadsOff() {
        #expect(MainWindowStatus(arePermissionsMissing: false, isEnabled: false, activation: .active) == .off)
        #expect(MainWindowStatus(arePermissionsMissing: false, isEnabled: false, activation: .rekordboxNotRunning) == .off)
    }

    @Test func anActiveOverlayReadsLive() {
        #expect(MainWindowStatus(arePermissionsMissing: false, isEnabled: true, activation: .active) == .live)
    }

    @Test(arguments: [OverlayActivation.rekordboxNotRunning, .mainWindowNotFound, .rekordboxNotFrontmost, .wrongMode(.export),
                      .wrongLayout("Browse"), .decksNotFound])
    func anEnabledButInactiveOverlayReadsWaiting(activation: OverlayActivation) {
        #expect(MainWindowStatus(arePermissionsMissing: false, isEnabled: true, activation: activation) == .waiting)
    }

    @Test func eachStatusUsesTheMatchingTone() {
        #expect(MainWindowStatus.live.tone == .live)
        #expect(MainWindowStatus.off.tone == .off)
        #expect(MainWindowStatus.waiting.tone == .waiting)
        #expect(MainWindowStatus.needsPermission.tone == .waiting)
    }

    @Test func everyStatusTitleIsLocalized() {
        let statuses: [MainWindowStatus] = [.live, .off, .waiting, .needsPermission]
        let titles = L10n.$override.withValue({ "⟦\($0)⟧" }) { statuses.map(\.title) }
        #expect(titles.allSatisfy { $0.hasPrefix("⟦") && $0.hasSuffix("⟧") })
        #expect(Set(titles).count == statuses.count)
    }
}
