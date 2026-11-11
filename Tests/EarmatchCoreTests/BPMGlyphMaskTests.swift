import CoreGraphics
import Testing
import EarmatchCore

@Suite struct BPMGlyphMaskTests {
    struct JogFixture {
        let file: String
        let jogFrame: CGRect
        var origin: CGPoint { CGPoint(x: jogFrame.minX - 10, y: jogFrame.minY - 7) }
    }

    let deck1 = JogFixture(file: "jog-bpm-deck1-135.00.png", jogFrame: CGRect(x: 558, y: 427, width: 54, height: 23))
    let deck2 = JogFixture(file: "jog-bpm-deck2-124.00.png", jogFrame: CGRect(x: 890, y: 427, width: 54, height: 23))

    func withFixture<T>(_ fixture: JogFixture, _ body: (PixelImage) throws -> T) throws -> T {
        try PixelBuffer(png: fixture.file).withImage(origin: fixture.origin, body)
    }

    @Test func findsTheIntegerDigitsThePointAndBothDecimals() throws {
        let glyphs = try #require(try withFixture(deck1) { BPMGlyphMask.glyphs(in: deck1.jogFrame, image: $0) })

        #expect(glyphs.integerDigits.count == 3)
        #expect(glyphs.decimals.count == 2)
        #expect(abs(glyphs.point.minX - 590) <= 0.5)
        #expect(abs(glyphs.decimals[1].maxX - 605.5) <= 0.5)
    }

    @Test(arguments: ["deck1", "deck2"])
    func oneDecimalCoversOnlyTheLastDigit(deck: String) throws {
        let fixture = deck == "deck1" ? deck1 : deck2
        let (glyphs, cover) = try withFixture(fixture) { image in
            (BPMGlyphMask.glyphs(in: fixture.jogFrame, image: image), BPMGlyphMask.cover(.oneDecimal, jogFrame: fixture.jogFrame, image: image))
        }
        let decimals = try #require(glyphs).decimals

        #expect(cover.minX > decimals[0].maxX)
        #expect(cover.minX <= decimals[1].minX)
        #expect(cover.maxX >= decimals[1].maxX)
        #expect(cover.minY <= fixture.jogFrame.minY && cover.maxY >= fixture.jogFrame.maxY)
    }

    @Test(arguments: ["deck1", "deck2"])
    func noDecimalsCoversThePointAndBothDecimals(deck: String) throws {
        let fixture = deck == "deck1" ? deck1 : deck2
        let (glyphs, cover) = try withFixture(fixture) { image in
            (BPMGlyphMask.glyphs(in: fixture.jogFrame, image: image), BPMGlyphMask.cover(.noDecimals, jogFrame: fixture.jogFrame, image: image))
        }
        let found = try #require(glyphs)

        #expect(cover.minX > found.integerDigits[2].maxX)
        #expect(cover.minX <= found.point.minX)
        #expect(cover.maxX >= found.decimals[1].maxX)
    }

    @Test func coverFullyCoversTheWholeDisplay() throws {
        let cover = try withFixture(deck1) { BPMGlyphMask.cover(.coverFully, jogFrame: deck1.jogFrame, image: $0) }
        #expect(cover == deck1.jogFrame)
    }

    @Test func unreadableDigitsAreCoveredFully() {
        let jogFrame = CGRect(x: 10, y: 10, width: 54, height: 23)
        let blank = PixelBuffer(width: 160, height: 90)
        let cover = blank.withImage { BPMGlyphMask.cover(.oneDecimal, jogFrame: jogFrame, image: $0) }

        #expect(cover == jogFrame)
    }
}
