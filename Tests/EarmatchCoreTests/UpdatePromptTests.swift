import EarmatchCore
import Testing

@Suite struct UpdatePromptTests {
    @Test func anUpdateFoundRightAfterOpeningAsksInFront() {
        #expect(UpdatePrompt.asksNow(secondsSinceLaunch: 3, rekordboxIsFrontmost: false))
    }

    @Test func anUpdateFoundWhileRekordboxIsInFrontWaitsInTheMenus() {
        #expect(!UpdatePrompt.asksNow(secondsSinceLaunch: 3, rekordboxIsFrontmost: true))
    }

    @Test(arguments: [UpdatePrompt.launchWindow, 600, 3_600])
    func anUpdateFoundLongAfterOpeningWaitsInTheMenus(secondsSinceLaunch: Double) {
        #expect(!UpdatePrompt.asksNow(secondsSinceLaunch: secondsSinceLaunch, rekordboxIsFrontmost: false))
    }
}
