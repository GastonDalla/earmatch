import Testing
import EarmatchCore

@Suite struct RetryScheduleTests {
    @Test func aFailureIsRetriedAfterASecondAndAMissingPermissionAfterTwo() {
        var schedule = RetrySchedule()
        let failure = schedule.retry(.failure, at: 10)
        var permission = RetrySchedule()
        let denied = permission.retry(.permissionDenied, at: 10)

        #expect(failure == RetrySchedule.Timer(delay: 1, fireTime: 11))
        #expect(denied == RetrySchedule.Timer(delay: 2, fireTime: 12))
    }

    @Test func noNewAttemptStartsBeforeTheBackoffEnds() {
        var schedule = RetrySchedule()
        #expect(schedule.allowsAttempt(at: 0))
        _ = schedule.retry(.failure, at: 10)

        #expect(!schedule.allowsAttempt(at: 10.5))
        #expect(schedule.allowsAttempt(at: 11))
    }

    @Test func anOffScreenWindowIsLookedForLessAndLessOftenUpToTwoSeconds() throws {
        var schedule = RetrySchedule()
        var delays: [Double] = []
        var now = 0.0
        for _ in 0 ..< 7 {
            let timerArmed = schedule.retry(.windowOffScreen, at: now)
            let timer = try #require(timerArmed)
            delays.append(timer.delay)
            now = timer.fireTime
            let fired = schedule.fire(timer)
            #expect(fired)
            schedule.attemptStarted()
        }
        #expect(delays == [0.1, 0.2, 0.4, 0.8, 1.6, 2, 2])
    }

    @Test func findingTheWindowStartsTheOffScreenBackoffOver() {
        var schedule = RetrySchedule()
        _ = schedule.retry(.windowOffScreen, at: 0)
        schedule.attemptStarted()
        _ = schedule.retry(.windowOffScreen, at: 1)
        schedule.attemptStarted()
        schedule.windowFound()
        let next = schedule.retry(.windowOffScreen, at: 5)

        #expect(next?.delay == 0.1)
    }

    @Test func stoppingStartsTheOffScreenBackoffOver() {
        var schedule = RetrySchedule()
        _ = schedule.retry(.windowOffScreen, at: 0)
        schedule.attemptStarted()
        _ = schedule.retry(.windowOffScreen, at: 1)
        schedule.attemptStarted()
        schedule.reset()
        let next = schedule.retry(.windowOffScreen, at: 5)

        #expect(next?.delay == 0.1)
    }

    @Test func anEarlierRetryReplacesALaterOneAndALaterOneIsNotScheduled() throws {
        var schedule = RetrySchedule()
        let laterArmed = schedule.retry(.permissionDenied, at: 0)
        let later = try #require(laterArmed)
        let earlierArmed = schedule.retry(.windowOffScreen, at: 0.5)
        let earlier = try #require(earlierArmed)
        let notArmed = schedule.retry(.failure, at: 0.55)
        let laterFired = schedule.fire(later)
        let earlierFired = schedule.fire(earlier)

        #expect(notArmed == nil)
        #expect(!laterFired)
        #expect(earlierFired)
    }

    @Test func aLaterRetryKeepsTheGateOpenWhenTheArmedEarlierTimerFires() throws {
        var schedule = RetrySchedule()
        let earlierArmed = schedule.retry(.windowOffScreen, at: 0)
        let earlier = try #require(earlierArmed)
        let notArmed = schedule.retry(.permissionDenied, at: 0.05)
        let allowedAtFire = schedule.allowsAttempt(at: earlier.fireTime)
        let fired = schedule.fire(earlier)

        #expect(notArmed == nil)
        #expect(!schedule.allowsAttempt(at: 0.06))
        #expect(allowedAtFire)
        #expect(fired)
    }

    @Test func aTimerFiresOnceAndNotAfterANewAttemptStarted() throws {
        var schedule = RetrySchedule()
        let timerArmed = schedule.retry(.failure, at: 0)
        let timer = try #require(timerArmed)
        let first = schedule.fire(timer)
        let second = schedule.fire(timer)
        let nextArmed = schedule.retry(.failure, at: 2)
        let next = try #require(nextArmed)
        schedule.attemptStarted()
        let afterStart = schedule.fire(next)

        #expect(first)
        #expect(!second)
        #expect(!afterStart)
    }
}
