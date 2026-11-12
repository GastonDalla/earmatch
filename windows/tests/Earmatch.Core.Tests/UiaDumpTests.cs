using System.Text.Json;
using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class UiaDumpTests
{
    const string TwoDeckHorizontal = "uia-7.2.19-perf-2deck-horizontal.json";

    static string Json(string name) => File.ReadAllText(PathOf(name));

    static readonly UiaDumpInfo Info = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)), "Microsoft Windows 10.0.26100",
        "ARM64", 2, 2808, @"C:\Program Files\rekordbox\rekordbox 7.2.19\rekordbox.exe", "7.2.19.0342");

    [Fact]
    public void TheSnapshotOfTheNodesIsTheParsedFixture()
    {
        var json = Json(TwoDeckHorizontal);

        var snapshot = UiaDump.Snapshot(UiaDump.ReadNodes(json), VmDisplayAt200Percent);
        var parsed = UiaDump.Parse(json, VmDisplayAt200Percent);

        Assert.Equal(parsed.WindowFrame, snapshot.WindowFrame);
        Assert.Equal(parsed.Elements, snapshot.Elements);
        Assert.Equal(VmDisplayAt200Percent, snapshot.Scale);
    }

    [Fact]
    public void EachElementOfTheSnapshotPointsBackToItsNode()
    {
        var nodes = UiaDump.ReadNodes(Json(TwoDeckHorizontal));

        var snapshot = UiaDump.Snapshot(nodes, VmDisplayAt200Percent, out var positions);

        Assert.Equal(snapshot.Elements.Count, positions.Count);
        for (var index = 0; index < positions.Count; index++)
        {
            var node = nodes[positions[index]];
            Assert.Equal(node.Frame, snapshot.Elements[index].Frame);
            Assert.Equal(node.Value, snapshot.Elements[index].Value);
        }
    }

    [Fact]
    public void WrittenNodesReadBackUnchanged()
    {
        var nodes = UiaDump.ReadNodes(Json(TwoDeckHorizontal));

        var written = UiaDump.Write(Info, nodes);

        Assert.Equal(nodes, UiaDump.ReadNodes(written));
        Assert.Equal(Layout(TwoDeckHorizontal), RekordboxLayout.Locate(UiaDump.Parse(written, VmDisplayAt200Percent)));
    }

    [Fact]
    public void TheWrittenDumpRecordsTheScaleAndTheRekordboxProcess()
    {
        using var document = JsonDocument.Parse(UiaDump.Write(Info, UiaDump.ReadNodes(Json(TwoDeckHorizontal))));
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("scale").GetDouble());
        Assert.Equal("ARM64", root.GetProperty("arch").GetString());
        Assert.Equal(2808, root.GetProperty("process").GetProperty("id").GetInt32());
        Assert.Equal("7.2.19.0342", root.GetProperty("process").GetProperty("version").GetString());
        Assert.False(root.GetProperty("truncated").GetBoolean());
        var first = root.GetProperty("nodes")[0];
        Assert.Equal(["index", "parent", "depth", "controlType", "name", "helpText", "value", "frame"], first.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public void ANodeWithoutAFrameIsWrittenWithANullFrameAndLeftOutOfTheSnapshot()
    {
        List<UiaNode> nodes =
        [
            new(0, -1, 0, "ControlType.Window", "rekordbox", "", null, new ScreenRect(0, 0, 100, 50)),
            new(1, 0, 1, "ControlType.Text", "hidden", "", "x", null),
            new(2, 0, 1, "ControlType.Text", "", "", "128.00", new ScreenRect(10, 10, 20, 8)),
        ];

        var written = UiaDump.Write(Info, nodes);
        var snapshot = UiaDump.Snapshot(nodes, 1, out var positions);

        Assert.Equal(nodes, UiaDump.ReadNodes(written));
        Assert.Equal([new UiaElementSnapshot(UiaControlType.Text, new ScreenRect(10, 10, 20, 8), Value: "128.00")], snapshot.Elements);
        Assert.Equal([2], positions);
    }

    [Fact]
    public void OnlyTheNodesUnderTheRekordboxWindowAreInTheSnapshot()
    {
        List<UiaNode> nodes =
        [
            new(0, -1, 0, "ControlType.Window", "Preferences", "", null, new ScreenRect(0, 0, 10, 10)),
            new(1, 0, 1, "ControlType.Button", "OK", "", null, new ScreenRect(1, 1, 2, 2)),
            new(2, -1, 0, "ControlType.Window", "rekordbox", "", null, new ScreenRect(0, 0, 100, 50)),
            new(3, 2, 1, "ControlType.Custom", "", "", null, new ScreenRect(0, 0, 100, 20)),
            new(4, 3, 2, "ControlType.Button", "RST", "", null, new ScreenRect(2, 2, 6, 6)),
        ];

        var snapshot = UiaDump.Snapshot(nodes, 1);

        Assert.Equal(new ScreenRect(0, 0, 100, 50), snapshot.WindowFrame);
        Assert.Equal(["RST"], snapshot.Elements.Where(element => element.Name is not null).Select(element => element.Name));
        Assert.Equal(2, snapshot.Elements.Count);
    }

    [Theory]
    [InlineData("Deck BPM display:", "Deck BPM display:")]
    [InlineData("Deck BPM display:  \r\nShows the BPM of the track", "Deck BPM display:")]
    [InlineData("Sync Master:\nThe master deck", "Sync Master:")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void AHelpTextKeepsOnlyItsFirstLine(string? help, string? firstLine) => Assert.Equal(firstLine, UiaNode.FirstLine(help));
}
