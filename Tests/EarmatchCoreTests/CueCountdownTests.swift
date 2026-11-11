import CoreGraphics
import Testing
import EarmatchCore

@Suite struct CueCountdownTests {
    let cropOrigin = CGPoint(x: 0, y: 136)
    let deck1Strip = CGRect(x: 0, y: 136, width: 1492, height: 69)
    let deck2Strip = CGRect(x: 0, y: 205, width: 1492, height: 69)
    let playheadX: CGFloat = 746

    func markers(_ name: String, strip: CGRect) throws -> [CueMarker] {
        try PixelBuffer(png: name).withImage(origin: cropOrigin) { CueCountdown.markers(strip: strip, from: strip.minX, in: $0) }
    }

    @Test func findsTheHotCueFlagAndTheCuePointTriangleInRealFrames() throws {
        let deck1 = try markers("waveform-playing-0.png", strip: deck1Strip)
        let deck2 = try markers("waveform-playing-0.png", strip: deck2Strip)

        #expect(deck1.count == 2)
        #expect(Set(deck1.map(\.kind)) == [.hotCue, .cuePoint])
        #expect(deck1.allSatisfy { abs($0.x - 491.5) <= 1 })
        #expect(deck2.count == 2)
        #expect(deck2.contains { $0.kind == .cuePoint && abs($0.x - 491.5) <= 1 })
        #expect(deck2.contains { $0.kind == .hotCue && abs($0.x - 610.5) <= 1 })
    }

    @Test func downbeatTrianglesBeatTicksAndTheWaveformAreNotCues() throws {
        for name in ["waveform-playing-0.png", "waveform-playing-2.png", "waveform-playing-4.png"] {
            let buffer = try PixelBuffer(png: name)
            for strip in [deck1Strip, deck2Strip] {
                let ahead = buffer.withImage(origin: cropOrigin) { CueCountdown.markers(strip: strip, from: playheadX, in: $0) }
                #expect(ahead.isEmpty, "\(name)")
            }
        }
    }

    @Test func withNoCueAheadTheCountdownReadsDashes() throws {
        let buffer = try PixelBuffer(png: "waveform-playing-0.png")
        let beats = try buffer.withImage(origin: cropOrigin) { image in
            let grid = try #require(BeatGridDetector.read(strip: deck1Strip, playheadX: playheadX, in: image))
            return CueCountdown.beatsToNextCue(strip: deck1Strip, playheadX: playheadX, beatSpacing: grid.beatSpacing, in: image)
        }

        #expect(beats == nil)
        #expect(CueCountdown.counter(beatsAhead: beats) == nil)
    }

    @Test func countsDownToTheNearestCueAheadInBarsAndBeats() throws {
        let buffer = try PixelBuffer(png: "waveform-playing-0.png")
        try buffer.withImage(origin: cropOrigin) { image in
            let spacing1 = try #require(BeatGridDetector.read(strip: deck1Strip, playheadX: playheadX, in: image)).beatSpacing
            let spacing2 = try #require(BeatGridDetector.read(strip: deck2Strip, playheadX: playheadX, in: image)).beatSpacing
            func countdown(_ strip: CGRect, from x: CGFloat, spacing: Double) -> String? {
                CueCountdown.counter(beatsAhead: CueCountdown.beatsToNextCue(strip: strip, playheadX: x, beatSpacing: spacing, in: image))?.label
            }

            #expect(countdown(deck2Strip, from: 300, spacing: spacing2) == "01.3")
            #expect(countdown(deck2Strip, from: 540, spacing: spacing2) == "00.3")
            #expect(countdown(deck1Strip, from: 470, spacing: spacing1) == "00.1")
        }
    }

    @Test func cueMarkersScrollWithTheBeatGrid() throws {
        func reading(_ name: String, _ strip: CGRect) throws -> (hotCue: Double, phase: Double, spacing: Double) {
            try PixelBuffer(png: name).withImage(origin: cropOrigin) { image in
                let grid = try #require(BeatGridDetector.read(strip: strip, playheadX: playheadX, in: image))
                let hotCue = try #require(CueCountdown.markers(strip: strip, from: strip.minX, in: image).first { $0.kind == .hotCue })
                return (hotCue.x, try #require(grid.phaseInBar), grid.beatSpacing)
            }
        }
        for strip in [deck1Strip, deck2Strip] {
            let start = try reading("waveform-playing-0.png", strip)
            let end = try reading("waveform-playing-4.png", strip)
            let gridAdvance = (end.phase - start.phase + 4).truncatingRemainder(dividingBy: 4)
            let cueAdvance = (start.hotCue - end.hotCue) / start.spacing

            #expect(abs(cueAdvance - gridAdvance) < 0.1)
        }
    }

    @Test func anOrangeWaveformBodyIsNotACuePoint() {
        let buffer = PixelBuffer(width: 1200, height: 140)
        buffer.paint(x: 400, y: 10, width: 40, height: 128, (240, 150, 40))
        let found = buffer.withImage { CueCountdown.markers(strip: CGRect(x: 0, y: 0, width: 600, height: 70), from: 0, in: $0) }

        #expect(found.isEmpty)
    }

    @Test func aDrawnCuePointTriangleAndHotCueFlagAreFoundInSRGB() {
        let buffer = PixelBuffer(width: 1200, height: 140)
        for row in 0 ..< 20 {
            let half = 1 + row * 14 / 19
            buffer.paint(x: 600 - half, y: 116 + row, width: 2 * half, height: 1, (247, 160, 48))
        }
        buffer.paint(x: 789, y: 0, width: 22, height: 22, (255, 55, 111))
        let found = buffer.withImage { CueCountdown.markers(strip: CGRect(x: 0, y: 0, width: 600, height: 70), from: 0, in: $0) }

        #expect(found.map(\.kind) == [.cuePoint, .hotCue])
        #expect(abs(found[0].x - 300) <= 0.5)
        #expect(abs(found[1].x - 400) <= 0.5)
    }

    @Test func distancesBecomeBarsAndBeatsLikeThePlayers() {
        #expect(CueCountdown.counter(beatsAhead: 0.3)?.label == "00.1")
        #expect(CueCountdown.counter(beatsAhead: 4)?.label == "00.4")
        #expect(CueCountdown.counter(beatsAhead: 4.2)?.label == "01.1")
        #expect(CueCountdown.counter(beatsAhead: 26)?.label == "06.2")
        #expect(CueCountdown.counter(beatsAhead: 0) == nil)
        #expect(CueCountdown.counter(beatsAhead: -1) == nil)
    }

    @Test func theCountdownMovesOnAtTheDecksTempoBetweenFrames() {
        #expect(CueCountdown.beatsAhead(10, measuredAt: 2, beatsPerSecond: 2, at: 3.5) == 7)
        #expect(CueCountdown.beatsAhead(10, measuredAt: 2, beatsPerSecond: 0, at: 9) == 10)
    }
}
