import CoreGraphics
import Testing
import EarmatchCore

@Suite struct LayoutPixelsTests {
    struct Frame {
        let file: String
        let layout: String
        let origin: CGPoint
    }

    static let twoVertical = Frame(file: "waveform-2deck-vertical.png", layout: "ax-7.2.19-2deck-vertical.json", origin: CGPoint(x: 622, y: 136))
    static let fourVertical = Frame(file: "waveform-4deck-vertical.png", layout: "ax-7.2.19-4deck-vertical.json", origin: CGPoint(x: 622, y: 136))
    static let fourHorizontal = Frame(file: "waveform-4deck-horizontal.png", layout: "ax-7.2.19-4deck-horizontal.json", origin: CGPoint(x: 0, y: 136))

    static func strip(_ frame: Frame, deck: Int) throws -> WaveformStrip {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot(frame.layout))
        return try #require(layout.decks.first { $0.number == deck }?.strip)
    }

    static func withImage<T>(_ frame: Frame, _ body: (PixelImage) throws -> T) throws -> T {
        try PixelBuffer(png: frame.file).withImage(origin: frame.origin, body)
    }

    @Test func findsThePlayheadLineRekordboxDrawsJustPastTheStripsCentre() throws {
        let frames: [Frame] = [Self.twoVertical, Self.twoVertical, Self.fourVertical, Self.fourVertical, Self.fourHorizontal, Self.fourHorizontal]
        let decks = [1, 2, 1, 2, 1, 2]
        let expectations: [CGFloat] = [206.5, 206.5, 235, 235, 746.5, 746.5]
        for index in frames.indices {
            let frame = frames[index], deck = decks[index], expected = expectations[index]
            let strip = try Self.strip(frame, deck: deck)
            let found = try Self.withImage(frame) { PlayheadDetector.locate(strip, in: $0) }
            #expect(found == expected, "\(frame.file) deck \(deck)")
        }
    }

    @Test func theStackedTwoDeckPlayheadIsFoundToo() throws {
        let strip = WaveformStrip(frame: CGRect(x: 0, y: 136, width: 1492, height: 69), orientation: .horizontal)
        let found = try PixelBuffer(png: "waveform-playing-2.png").withImage(origin: CGPoint(x: 0, y: 136)) { PlayheadDetector.locate(strip, in: $0) }
        #expect(found == 746.5)
    }

    @Test func anEmptyDeckHasNoPlayhead() throws {
        for (frame, deck) in [(Self.fourVertical, 3), (Self.fourHorizontal, 4)] {
            let strip = try Self.strip(frame, deck: deck)
            #expect(try Self.withImage(frame) { PlayheadDetector.locate(strip, in: $0) } == nil)
        }
    }

    @Test(arguments: [1, 2])
    func readsTheTwoDeckVerticalColumnsGrid(deck: Int) throws {
        let strip = try Self.strip(Self.twoVertical, deck: deck)
        let reading = try #require(try Self.withImage(Self.twoVertical) { BeatGridDetector.read(strip: strip, playhead: 206.5, in: $0) })

        #expect(abs(reading.beatSpacing - 10.5) < 0.4)
        #expect(reading.beatXs.allSatisfy { $0 >= 164 && $0 < 206 })
        #expect(reading.beatXs.count >= 3)
        let phase = try #require(reading.phaseInBar)
        #expect(Int(phase) == 3)
    }

    @Test(arguments: [1, 2])
    func readsTheFourDeckVerticalSixPointBeats(deck: Int) throws {
        let strip = try Self.strip(Self.fourVertical, deck: deck)
        let reading = try #require(try Self.withImage(Self.fourVertical) { BeatGridDetector.read(strip: strip, playhead: 235, in: $0) })

        #expect(abs(reading.beatSpacing - 6) < 0.2)
        #expect(reading.beatXs.count >= 10)
        #expect(reading.downbeatXs.count == 2)
        for downbeat in reading.downbeatXs {
            #expect(abs(downbeat - 188) < 0.6 || abs(downbeat - 212) < 0.6)
        }
        let phase = try #require(reading.phaseInBar)
        #expect(Int(phase) == 3)
        #expect(phase > 3.75)
    }

    @Test(arguments: [1, 2])
    func readsTheFourDeckHorizontalStrips(deck: Int) throws {
        let strip = try Self.strip(Self.fourHorizontal, deck: deck)
        let reading = try #require(try Self.withImage(Self.fourHorizontal) { BeatGridDetector.read(strip: strip, playhead: 746.5, in: $0) })

        #expect(reading.beatXs.allSatisfy { $0 > 12 && $0 < 746.5 })
        #expect(reading.downbeatXs.count >= 4)
        #expect(Int(try #require(reading.phaseInBar)) == 3)
    }

    @Test(arguments: [3, 4])
    func emptyDecksHaveNoGrid(deck: Int) throws {
        for frame in [Self.fourVertical, Self.fourHorizontal] {
            let strip = try Self.strip(frame, deck: deck)
            #expect(try Self.withImage(frame) { BeatGridDetector.read(strip: strip, playhead: strip.center, in: $0) } == nil)
        }
    }

    @Test func theButtonBarOverTheColumnsTopIsNotReadAsMarkers() throws {
        let strip = try Self.strip(Self.fourVertical, deck: 1)
        let reading = try #require(try Self.withImage(Self.fourVertical) { BeatGridDetector.read(strip: strip, playhead: 235, in: $0) })
        #expect(reading.beatXs.allSatisfy { $0 >= 164 })
    }

    @Test func readsSyntheticVerticalMarkersOnBothEdges() throws {
        let buffer = PixelBuffer(width: 120, height: 600)
        for beat in 0 ..< 10 {
            let y = (15 + beat * 30) * 2
            if beat % 4 == 1 {
                buffer.paint(x: 2, y: y - 5, width: 6, height: 11, (255, 0, 0))
                buffer.paint(x: 112, y: y - 5, width: 6, height: 11, (255, 0, 0))
            } else {
                buffer.paint(x: 2, y: y - 2, width: 2, height: 6, (124, 124, 124))
                buffer.paint(x: 2, y: y, width: 7, height: 1, (124, 124, 124))
                buffer.paint(x: 114, y: y - 2, width: 2, height: 6, (124, 124, 124))
                buffer.paint(x: 111, y: y, width: 7, height: 1, (124, 124, 124))
            }
        }
        buffer.paint(x: 0, y: 300, width: 120, height: 2, (112, 112, 112))
        let strip = WaveformStrip(frame: CGRect(x: 0, y: 0, width: 60, height: 300), orientation: .vertical)

        let (reading, playhead) = buffer.withImage { image in
            (BeatGridDetector.read(strip: strip, playhead: 150.5, in: image), PlayheadDetector.locate(strip, in: image))
        }

        #expect(playhead == 150.5)
        let found = try #require(reading)
        #expect(abs(found.beatSpacing - 30) < 0.1)
        #expect(found.downbeatXs.count == 3)
        let phase = try #require(found.phaseInBar)
        #expect(abs(phase - (150.5 - 45) / 30) < 0.03)
    }

    @Test func aMarkOnOneEdgeOnlyIsNotABeat() throws {
        let buffer = PixelBuffer(width: 120, height: 600)
        for beat in 0 ..< 10 {
            let y = (15 + beat * 30) * 2
            buffer.paint(x: 2, y: y - 2, width: 7, height: 6, (124, 124, 124))
            buffer.paint(x: 111, y: y - 2, width: 7, height: 6, (124, 124, 124))
        }
        buffer.paint(x: 2, y: 2 * 62, width: 7, height: 6, (124, 124, 124))
        let strip = WaveformStrip(frame: CGRect(x: 0, y: 0, width: 60, height: 300), orientation: .vertical)

        let reading = try #require(buffer.withImage { BeatGridDetector.read(strip: strip, playhead: 150.5, in: $0) })

        #expect(!reading.beatXs.contains { abs($0 - 63.5) < 2 })
        #expect(abs(reading.beatSpacing - 30) < 0.1)
    }

    @Test(arguments: ["horizontal", "vertical"])
    func spotsTheSyncMasterAmongFourDecks(layout: String) throws {
        let snapshot = try Fixture.snapshot("ax-7.2.19-4deck-\(layout).json")
        let decks = RekordboxLayout.locate(in: snapshot).decks
        var masters: [Int] = []
        for deck in decks {
            let button = deck.masterButtonFrame
            let isMaster = try PixelBuffer(png: "master-4deck-\(layout)-deck\(deck.number).png")
                .withImage(origin: CGPoint(x: button.minX - 4, y: button.minY - 4)) { SyncMasterDetector.isMaster(button: button, in: $0) }
            if isMaster { masters.append(deck.number) }
        }
        #expect(masters == [2])
    }

    @Test func coversTheLargerTwoDeckVerticalJogDigits() throws {
        let jog = CGRect(x: 467, y: 316, width: 70, height: 26)
        let (glyphs, oneDecimal, noDecimals) = try PixelBuffer(png: "jog-bpm-2deck-vertical-deck1-135.08.png")
            .withImage(origin: CGPoint(x: 457, y: 309)) { image in
                (BPMGlyphMask.glyphs(in: jog, image: image), BPMGlyphMask.cover(.oneDecimal, jogFrame: jog, image: image),
                 BPMGlyphMask.cover(.noDecimals, jogFrame: jog, image: image))
            }
        let found = try #require(glyphs)

        #expect(found.integerDigits.count == 3)
        #expect(found.decimals.count == 2)
        #expect(oneDecimal.minX > found.decimals[0].maxX && oneDecimal.minX <= found.decimals[1].minX)
        #expect(noDecimals.minX > found.integerDigits[2].maxX && noDecimals.minX <= found.point.minX)
    }
}
