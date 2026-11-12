using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class ReleaseNotesTests
{
    [Fact]
    public void Parse_ReadsTheHeadingAndEachBulletOfTheReleaseNotes()
    {
        var notes = """
            # Earmatch 0.1.2

            - Earmatch now asks for its two permissions
              before anything else.
            - The download opens on a **designed** window.
            """;
        Assert.Equal(
        [
            new ReleaseNoteBlock(ReleaseNoteKind.Heading, "Earmatch 0.1.2"),
            new ReleaseNoteBlock(ReleaseNoteKind.Bullet, "Earmatch now asks for its two permissions before anything else."),
            new ReleaseNoteBlock(ReleaseNoteKind.Bullet, "The download opens on a designed window."),
        ], ReleaseNotes.Parse(notes));
    }

    [Fact]
    public void Parse_JoinsTheLinesOfAParagraphAndKeepsTheTextOfLinksAndCode()
    {
        var notes = "Read the [guide](https://example.com/guide)\r\nand run `Earmatch.exe --dump-uia`.\r\n\r\nThanks.";
        Assert.Equal(
        [
            new ReleaseNoteBlock(ReleaseNoteKind.Paragraph, "Read the guide and run Earmatch.exe --dump-uia."),
            new ReleaseNoteBlock(ReleaseNoteKind.Paragraph, "Thanks."),
        ], ReleaseNotes.Parse(notes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n \n")]
    public void Parse_GivesNothingForEmptyNotes(string? notes)
    {
        Assert.Empty(ReleaseNotes.Parse(notes));
    }
}
