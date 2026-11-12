using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class UpdateFeedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutAnOverrideUpdatesComeFromTheGitHubReleases(string? value)
    {
        var feed = UpdateFeed.Resolve(value);
        Assert.Equal(UpdateFeedKind.GitHub, feed.Kind);
        Assert.Equal("https://github.com/GastonDalla/earmatch", feed.Location);
        Assert.Equal("github.com/GastonDalla/earmatch", feed.DisplayName);
    }

    [Theory]
    [InlineData(@"C:\Users\dj\earmatch-feed", @"C:\Users\dj\earmatch-feed")]
    [InlineData(@"  D:/feeds/earmatch  ", @"D:/feeds/earmatch")]
    [InlineData(@"\\mac\share\feed", @"\\mac\share\feed")]
    public void AFullFolderPathInTheOverrideIsALocalTestFeed(string value, string folder)
    {
        var feed = UpdateFeed.Resolve(value);
        Assert.Equal(UpdateFeedKind.LocalFolder, feed.Kind);
        Assert.Equal(folder, feed.Location);
        Assert.Equal(folder, feed.DisplayName);
    }

    [Theory]
    [InlineData("https://github.com/someone/else")]
    [InlineData("http://example.com/feed")]
    [InlineData("HTTPS://EXAMPLE.COM/FEED")]
    [InlineData("ftp://example.com/feed")]
    [InlineData("file://server/share/earmatch-feed")]
    [InlineData("//example.com/feed")]
    public void AWebAddressInTheOverrideIsNeverUsedAsAFeed(string value)
    {
        Assert.Equal(new UpdateFeed(UpdateFeedKind.Invalid, value), UpdateFeed.Resolve(value));
    }

    [Theory]
    [InlineData(@"feeds\earmatch")]
    [InlineData(@"C:feeds")]
    [InlineData(@"\feeds")]
    public void AnythingElseInTheOverrideTurnsUpdatesOffInsteadOfReadingAnotherFeed(string value)
    {
        var feed = UpdateFeed.Resolve(value);
        Assert.Equal(UpdateFeedKind.Invalid, feed.Kind);
        Assert.Equal(value.Trim(), feed.Location);
    }

    [Fact]
    public void TheOverrideIsReadFromEarmatchUpdateFeed()
    {
        Assert.Equal("EARMATCH_UPDATE_FEED", UpdateFeed.OverrideVariable);
    }
}
