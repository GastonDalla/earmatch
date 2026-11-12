using Earmatch.Core;

namespace Earmatch.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), "earmatch-settings-" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_folder, "Earmatch", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void AMissingFileGivesTheDefaults()
    {
        var store = new SettingsStore(FilePath);
        Assert.Equal(new OverlaySettings(), store.Settings);
        Assert.Null(store.Value(AppLanguageChoice.StorageKey));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void SettingsAndValuesSurviveARestart()
    {
        var store = new SettingsStore(FilePath);
        store.Update((ref OverlaySettings settings) => BoothPreset.Xdj1000.Apply(ref settings));
        store.SetValue(AppLanguageChoice.StorageKey, "es");
        store.SetValue(MainWindowPane.StorageKey, "practice");

        var reopened = new SettingsStore(FilePath);
        Assert.Equal(store.Settings, reopened.Settings);
        Assert.Equal(BoothPreset.Xdj1000, reopened.Settings.BoothPreset);
        Assert.Equal("es", reopened.Value(AppLanguageChoice.StorageKey));
        Assert.Equal("practice", reopened.Value(MainWindowPane.StorageKey));
    }

    [Fact]
    public void TheFileUsesTheMacOSKeys()
    {
        var store = new SettingsStore(FilePath);
        store.Update((ref OverlaySettings settings) => settings.WaveformMode = WaveformMode.HideBoth);
        var json = File.ReadAllText(FilePath);
        Assert.Contains("\"overlaySettings\"", json);
        Assert.Contains("\"waveformMode\": \"hideBoth\"", json);
    }

    [Fact]
    public void AChangeIsAnnouncedOnceAndAnUnchangedValueIsNot()
    {
        var store = new SettingsStore(FilePath);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Update((ref OverlaySettings settings) => settings.IsEnabled = false);
        store.Update((ref OverlaySettings settings) => settings.IsEnabled = false);
        store.Settings = store.Settings;

        Assert.Equal(1, changes);
        Assert.False(store.Settings.IsEnabled);
    }

    [Fact]
    public void ADamagedFileGivesTheDefaultsAndIsReplacedOnTheNextChange()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        var store = new SettingsStore(FilePath);
        Assert.Equal(new OverlaySettings(), store.Settings);

        store.Update((ref OverlaySettings settings) => settings.IsBlindModeEnabled = true);
        Assert.True(new SettingsStore(FilePath).Settings.IsBlindModeEnabled);
    }

    [Fact]
    public void AnUnreadableSettingFallsBackWithoutLosingTheOthers()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "overlaySettings": { "waveformMode": "sideways", "isEnabled": false }, "appLanguage": "en" }""");

        var store = new SettingsStore(FilePath);
        Assert.Equal(WaveformMode.Split, store.Settings.WaveformMode);
        Assert.False(store.Settings.IsEnabled);
        Assert.Equal("en", store.Value(AppLanguageChoice.StorageKey));
    }

    [Fact]
    public void ClearingAValueRemovesIt()
    {
        var store = new SettingsStore(FilePath);
        store.SetValue(AppLanguageChoice.StorageKey, "es");
        store.SetValue(AppLanguageChoice.StorageKey, null);
        Assert.Null(new SettingsStore(FilePath).Value(AppLanguageChoice.StorageKey));
    }
}
