import Testing
import EarmatchCore

@Suite struct PermissionLockTests {
    @Test func startsLockedWhileAPermissionIsMissing() {
        #expect(PermissionLock(arePermissionsMissing: true, activation: .rekordboxNotRunning).isLocked)
        #expect(PermissionLock(arePermissionsMissing: false, activation: .screenRecordingMissing).isLocked)
        #expect(!PermissionLock(arePermissionsMissing: false, activation: .rekordboxNotRunning).isLocked)
    }

    @Test func waitsForTheWindowAndTheMonitorToSeeBothPermissions() {
        #expect(PermissionLock.isWaiting(arePermissionsMissing: true, activation: .active))
        #expect(PermissionLock.isWaiting(arePermissionsMissing: false, activation: .accessibilityMissing))
        #expect(PermissionLock.isWaiting(arePermissionsMissing: false, activation: .screenRecordingMissing))
        #expect(!PermissionLock.isWaiting(arePermissionsMissing: false, activation: .rekordboxNotRunning))
        #expect(!PermissionLock.isWaiting(arePermissionsMissing: false, activation: .active))
    }

    @Test func unlocksAMomentAfterBothAreAllowed() {
        var lock = PermissionLock(arePermissionsMissing: true, activation: .screenRecordingMissing)

        let unlocksAt = lock.update(arePermissionsMissing: false, activation: .rekordboxNotRunning, at: 10)
        #expect(unlocksAt == 10 + PermissionLock.unlockDelay)
        #expect(lock.isLocked)

        #expect(lock.update(arePermissionsMissing: false, activation: .rekordboxNotRunning, at: 10.5) == unlocksAt)
        #expect(lock.isLocked)

        #expect(lock.update(arePermissionsMissing: false, activation: .rekordboxNotRunning, at: 10 + PermissionLock.unlockDelay) == nil)
        #expect(!lock.isLocked)
    }

    @Test func staysLockedUntilTheMonitorAlsoSeesBothPermissions() {
        var lock = PermissionLock(arePermissionsMissing: true, activation: .screenRecordingMissing)

        #expect(lock.update(arePermissionsMissing: false, activation: .screenRecordingMissing, at: 10) == nil)
        #expect(lock.update(arePermissionsMissing: false, activation: .screenRecordingMissing, at: 20) == nil)
        #expect(lock.isLocked)

        #expect(lock.update(arePermissionsMissing: false, activation: .rekordboxNotRunning, at: 21) == 21 + PermissionLock.unlockDelay)
        #expect(lock.isLocked)
    }

    @Test func aPermissionLostDuringTheDelayRestartsIt() {
        var lock = PermissionLock(arePermissionsMissing: true, activation: .accessibilityMissing)
        _ = lock.update(arePermissionsMissing: false, activation: .active, at: 10)

        #expect(lock.update(arePermissionsMissing: true, activation: .active, at: 10.3) == nil)
        #expect(lock.isLocked)

        let allowedAgainAt = 10 + PermissionLock.unlockDelay
        #expect(lock.update(arePermissionsMissing: false, activation: .active, at: allowedAgainAt) == allowedAgainAt + PermissionLock.unlockDelay)
        #expect(lock.isLocked)
    }

    @Test func locksAgainAtOnceWhenAPermissionIsRevoked() {
        var lock = PermissionLock(arePermissionsMissing: false, activation: .active)

        #expect(lock.update(arePermissionsMissing: false, activation: .active, at: 10) == nil)
        #expect(!lock.isLocked)

        #expect(lock.update(arePermissionsMissing: false, activation: .accessibilityMissing, at: 11) == nil)
        #expect(lock.isLocked)
    }
}
