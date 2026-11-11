import CoreGraphics
import Testing
import EarmatchCore

@Suite struct BeatGridDetectorTests {
    let cropOrigin = CGPoint(x: 0, y: 136)
    let deck1Strip = CGRect(x: 0, y: 136, width: 1492, height: 69)
    let deck2Strip = CGRect(x: 0, y: 205, width: 1492, height: 69)
    let playheadX: CGFloat = 746
    let grey: (UInt8, UInt8, UInt8) = (124, 124, 124)
    let red: (UInt8, UInt8, UInt8) = (255, 0, 0)

    func syntheticStrip() -> PixelBuffer {
        let buffer = PixelBuffer(width: 1200, height: 120)
        for beat in 0 ..< 16 {
            let x = (100 + beat * 30) * 2
            if beat % 4 == 1 {
                buffer.paint(x: x - 5, y: 2, width: 11, height: 6, red)
            } else {
                buffer.paint(x: x - 2, y: 2, width: 5, height: 2, grey)
                buffer.paint(x: x, y: 2, width: 1, height: 6, grey)
            }
        }
        buffer.paint(x: 600, y: 0, width: 2, height: 120, (112, 112, 112))
        return buffer
    }

    @Test func readsSpacingAndPhaseFromSyntheticMarkers() throws {
        let reading = try #require(syntheticStrip().withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        })
        #expect(abs(reading.beatSpacing - 30) < 0.1)
        #expect(reading.downbeatXs.count == 4)
        let phase = try #require(reading.phaseInBar)
        #expect(abs(phase - 50.0 / 30.0) < 0.02)
    }

    @Test func thePlayheadIsNotABeat() throws {
        let reading = try #require(syntheticStrip().withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        })
        #expect(!reading.beatXs.contains { abs($0 - 300) < 2 })
    }

    @Test func anOffGridMarkerNearThePlayheadDoesNotBecomeTheGridReference() throws {
        let buffer = syntheticStrip()
        buffer.paint(x: 590, y: 2, width: 5, height: 2, grey)
        buffer.paint(x: 592, y: 2, width: 1, height: 6, grey)

        let reading = try #require(buffer.withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        })

        #expect(abs(reading.beatSpacing - 30) < 0.1)
        #expect(!reading.beatXs.contains { abs($0 - 296) < 2 })
        #expect(abs(try #require(reading.phaseInBar) - 50.0 / 30.0) < 0.02)
    }

    @Test func theWaveformsColourModeDoesNotChangeTheReading() throws {
        let plain = try #require(syntheticStrip().withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        })
        let colours: [(UInt8, UInt8, UInt8)] = [(230, 40, 30), (240, 240, 240), (40, 90, 230), (240, 150, 40), (124, 124, 124)]
        let buffer = syntheticStrip()
        for column in stride(from: 0, to: 1200, by: 6) {
            let height = 20 + (column * 7) % 70
            buffer.paint(x: column, y: 60 - height / 2, width: 6, height: height, colours[(column / 6) % colours.count])
        }

        let coloured = try #require(buffer.withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        })

        #expect(coloured == plain)
    }

    @Test func anEmptyStripHasNoReading() {
        let reading = PixelBuffer(width: 1200, height: 120).withImage {
            BeatGridDetector.read(strip: CGRect(x: 0, y: 0, width: 600, height: 60), playheadX: 300, in: $0)
        }
        #expect(reading == nil)
    }

    @Test func matchesTheBarCountersRekordboxDrawsOnScreen() throws {
        let buffer = try PixelBuffer(png: "waveform-playing-2.png")
        let (deck1, deck2) = try buffer.withImage(origin: cropOrigin) { image in
            (try #require(BeatGridDetector.read(strip: deck1Strip, playheadX: playheadX, in: image)),
             try #require(BeatGridDetector.read(strip: deck2Strip, playheadX: playheadX, in: image)))
        }
        #expect(abs(deck1.beatSpacing - 27.75) < 0.4)
        #expect(abs(deck2.beatSpacing - 30.0) < 0.4)
        #expect(Int(try #require(deck1.phaseInBar)) == 2)
        #expect(Int(try #require(deck2.phaseInBar)) == 1)
    }

    @Test func phaseAdvancesInProportionToEachDecksTempo() throws {
        func phases(_ name: String) throws -> (Double, Double, Double, Double) {
            try PixelBuffer(png: name).withImage(origin: cropOrigin) { image in
                let d1 = try #require(BeatGridDetector.read(strip: deck1Strip, playheadX: playheadX, in: image))
                let d2 = try #require(BeatGridDetector.read(strip: deck2Strip, playheadX: playheadX, in: image))
                return (try #require(d1.phaseInBar), try #require(d2.phaseInBar), d1.beatSpacing, d2.beatSpacing)
            }
        }
        let start = try phases("waveform-playing-0.png")
        let end = try phases("waveform-playing-4.png")
        func advance(_ from: Double, _ to: Double) -> Double { (to - from + 4).truncatingRemainder(dividingBy: 4) }
        let advance1 = advance(start.0, end.0)
        let advance2 = advance(start.1, end.1)

        #expect(abs(advance1 - 0.805 * 135 / 60) < 0.2)
        #expect(abs(advance1 / advance2 - 135.0 / 124.0) < 0.04)
        #expect(abs(start.3 / start.2 - 135.0 / 124.0) < 0.02)
    }

    @Test func spotsTheSyncMasterByItsBlueBorder() throws {
        let buffer = try PixelBuffer(png: "waveform-playing-2.png")
        buffer.withImage(origin: cropOrigin) { image in
            #expect(SyncMasterDetector.isMaster(button: CGRect(x: 612, y: 311, width: 70, height: 13), in: image))
            #expect(!SyncMasterDetector.isMaster(button: CGRect(x: 1414, y: 311, width: 70, height: 13), in: image))
        }
    }
}
