import EarmatchCore
import Testing

@Suite struct UpdatePromptTests {
    @Test func anUpdateFoundRightAfterOpeningAsksInFront() {
        #expect(UpdatePrompt.showsAlert(immediateFocus: true, secondsSinceLaunch: 3, rekordboxIsFrontmost: false))
    }

    @Test func anUpdateFoundWhileRekordboxIsInFrontWaitsInTheMenus() {
        #expect(!UpdatePrompt.showsAlert(immediateFocus: true, secondsSinceLaunch: 3, rekordboxIsFrontmost: true))
    }

    @Test(arguments: [UpdatePrompt.launchWindow, 600, 3_600])
    func anIdleTimeCheckLongAfterOpeningWaitsInTheMenus(secondsSinceLaunch: Double) {
        #expect(!UpdatePrompt.showsAlert(immediateFocus: true, secondsSinceLaunch: secondsSinceLaunch, rekordboxIsFrontmost: false))
    }

    @Test func aCheckSparkleWouldShowBehindOtherWindowsWaitsInTheMenus() {
        #expect(!UpdatePrompt.showsAlert(immediateFocus: false, secondsSinceLaunch: 3, rekordboxIsFrontmost: false))
    }
}
