import Testing
import EarmatchCore

@Suite struct MainWindowListCheckTests {
    @Test func aListWithOtherWindowsButNoMainWindowMeansItWasClosed() {
        var check = MainWindowListCheck()
        let gone = check.isMainWindowGone(windowTitles: ["Preferences"])
        #expect(gone)
    }

    @Test func aListWithTheMainWindowMeansItIsOpen() {
        var check = MainWindowListCheck()
        let gone = check.isMainWindowGone(windowTitles: ["Preferences", "rekordbox"])
        #expect(!gone)
    }

    @Test func anEmptyListIsTransientUntilASecondReadConfirmsIt() {
        var check = MainWindowListCheck()
        let first = check.isMainWindowGone(windowTitles: [])
        let second = check.isMainWindowGone(windowTitles: [])

        #expect(!first)
        #expect(second)
    }

    @Test func aFailedReadIsUnknownAndBreaksTheConfirmation() {
        var check = MainWindowListCheck()
        let failed = check.isMainWindowGone(windowTitles: nil)
        _ = check.isMainWindowGone(windowTitles: [])
        let afterFailure = check.isMainWindowGone(windowTitles: nil)
        let emptyAgain = check.isMainWindowGone(windowTitles: [])

        #expect(!failed)
        #expect(!afterFailure)
        #expect(!emptyAgain)
    }

    @Test func findingTheWindowForgetsAnEarlierEmptyList() {
        var check = MainWindowListCheck()
        _ = check.isMainWindowGone(windowTitles: [])
        check.mainWindowFound()
        let gone = check.isMainWindowGone(windowTitles: [])

        #expect(!gone)
    }
}
