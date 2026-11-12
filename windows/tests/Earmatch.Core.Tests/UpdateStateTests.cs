using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class UpdateStateTests
{
    static readonly DateTimeOffset CheckedAt = new(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(-3));
    static readonly UpdateState Installed = UpdateState.Ready(channel: "win-arm64", feed: "github.com/GastonDalla/earmatch");

    static string Spanish(Func<string> text) => L10n.WithOverride(LocalizationCatalog.Load("es"), text);

    [Fact]
    public void ADevelopmentBuildHasNoUpdateItemAndTheDiagnosticsSayWhy()
    {
        var state = UpdateState.Off("development build, not installed with Earmatch's Setup");
        Assert.Equal(UpdatePhase.Off, state.Phase);
        Assert.False(state.ShowsMenuItem);
        Assert.Equal("Updates: off (development build, not installed with Earmatch's Setup)", state.DiagnosticsLine);
    }

    [Fact]
    public void AnInstalledCopyOffersCheckForUpdatesBeforeItHasChecked()
    {
        Assert.True(Installed.ShowsMenuItem);
        Assert.True(Installed.IsMenuItemEnabled);
        Assert.Equal("Check for Updates…", Installed.MenuTitle);
        Assert.Equal("Buscar actualizaciones…", Spanish(() => Installed.MenuTitle));
        Assert.Equal("Updates: win-arm64 channel from github.com/GastonDalla/earmatch; not checked yet", Installed.DiagnosticsLine);
    }

    [Fact]
    public void WhileCheckingTheItemStaysAvailable()
    {
        var state = Installed.Checking();
        Assert.Equal(UpdatePhase.Checking, state.Phase);
        Assert.True(state.IsMenuItemEnabled);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.EndsWith("; checking now", state.DiagnosticsLine);
    }

    [Fact]
    public void NoNewVersionIsUpToDate()
    {
        var state = Installed.Checking().Checked(CheckedAt, newVersion: null);
        Assert.Equal(UpdatePhase.UpToDate, state.Phase);
        Assert.False(state.HasUpdate);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.Equal("Updates: win-arm64 channel from github.com/GastonDalla/earmatch; up to date (checked 2026-11-12 10:30 -03:00)", state.DiagnosticsLine);
    }

    [Fact]
    public void AnUpdateFoundAndNotShownYetMarksTheMenus()
    {
        var state = Installed.Checking().Checked(CheckedAt, newVersion: "0.1.3");
        Assert.Equal(UpdatePhase.Available, state.Phase);
        Assert.True(state.HasUpdate);
        Assert.Equal("0.1.3", state.Version);
        Assert.Equal("Update Available…", state.MenuTitle);
        Assert.Equal("Actualización disponible…", Spanish(() => state.MenuTitle));
        Assert.EndsWith("; 0.1.3 available (checked 2026-11-12 10:30 -03:00)", state.DiagnosticsLine);
    }

    [Fact]
    public void AnUpdateTheUserPutOffGoesBackToCheckForUpdates()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Postponed();
        Assert.Equal(UpdatePhase.Postponed, state.Phase);
        Assert.True(state.HasUpdate);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.EndsWith("; 0.1.3 available, put off (checked 2026-11-12 10:30 -03:00)", state.DiagnosticsLine);
    }

    [Fact]
    public void WhileTheUpdateDownloadsAndInstallsTheItemIsDisabled()
    {
        var downloading = Installed.Checked(CheckedAt, "0.1.3").Downloading();
        Assert.False(downloading.IsMenuItemEnabled);
        Assert.Equal("Downloading Update…", downloading.MenuTitle);
        Assert.Equal("Descargando actualización…", Spanish(() => downloading.MenuTitle));
        Assert.EndsWith("; downloading 0.1.3", downloading.DiagnosticsLine);

        var restarting = downloading.Restarting();
        Assert.False(restarting.IsMenuItemEnabled);
        Assert.EndsWith("; restarting to install 0.1.3", restarting.DiagnosticsLine);
    }

    [Fact]
    public void AFailedCheckIsReportedWithItsTime()
    {
        var state = Installed.Checking().CheckFailed(CheckedAt, "No such host is known.");
        Assert.Equal(UpdatePhase.Failed, state.Phase);
        Assert.True(state.IsMenuItemEnabled);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.EndsWith("; last check failed (2026-11-12 10:30 -03:00): No such host is known.", state.DiagnosticsLine);
    }

    [Fact]
    public void AFailedDownloadKeepsTheVersionItFoundAndTheProblem()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Downloading().DownloadFailed("The remote server returned 404.");
        Assert.Equal(UpdatePhase.Failed, state.Phase);
        Assert.False(state.HasUpdate);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.EndsWith("; downloading 0.1.3 failed: The remote server returned 404.", state.DiagnosticsLine);
    }

    [Fact]
    public void ANewCheckForgetsTheLastProblem()
    {
        var state = Installed.CheckFailed(CheckedAt, "offline").Checking().Checked(CheckedAt.AddDays(1), null);
        Assert.Null(state.Problem);
        Assert.Null(state.Version);
        Assert.EndsWith("; up to date (checked 2026-11-13 10:30 -03:00)", state.DiagnosticsLine);
    }

    [Fact]
    public void TheDiagnosticsStayInEnglish()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3");
        Assert.Equal(state.DiagnosticsLine, Spanish(() => state.DiagnosticsLine));
    }

    [Fact]
    public void ASkippedVersionLeavesCheckForUpdatesInTheMenusAndIsNamedInTheDiagnostics()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Skipped();
        Assert.Equal(UpdatePhase.Skipped, state.Phase);
        Assert.False(state.HasUpdate);
        Assert.True(state.IsMenuItemEnabled);
        Assert.Equal("Check for Updates…", state.MenuTitle);
        Assert.EndsWith("; 0.1.3 available, skipped (checked 2026-11-12 10:30 -03:00)", state.DiagnosticsLine);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("The operation was canceled.")]
    [InlineData("Unable to read data from the transport connection.")]
    public void ADownloadCancelledOrClosedByTheUserIsPutOffNeverInstalledNorRetried(string? problem)
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Downloading().AfterDownload(wasCancelled: true, problem);
        Assert.Equal(UpdatePhase.Postponed, state.Phase);
        Assert.Null(state.Problem);
        Assert.True(state.HasUpdate);
    }

    [Fact]
    public void ADownloadThatFailedWithoutTheUserIsReportedSoItCanBeTriedAgain()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Downloading().AfterDownload(wasCancelled: false, "The remote server returned 404.");
        Assert.Equal(UpdatePhase.Failed, state.Phase);
        Assert.Equal("The remote server returned 404.", state.Problem);
    }

    [Fact]
    public void AFinishedDownloadNobodyCancelledRestartsEarmatchToInstall()
    {
        var state = Installed.Checked(CheckedAt, "0.1.3").Downloading().AfterDownload(wasCancelled: false, problem: null);
        Assert.Equal(UpdatePhase.Restarting, state.Phase);
    }
}
