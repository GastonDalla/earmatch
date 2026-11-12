using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class LocalizationTests
{
    static readonly OverlayActivation[] Activations =
    [
        OverlayActivation.RekordboxNotRunning, OverlayActivation.MainWindowNotFound, OverlayActivation.RekordboxNotFrontmost,
        OverlayActivation.WrongMode(RekordboxMode.Export), OverlayActivation.WrongLayout("Browse"), OverlayActivation.WrongLayout("4Deck Horizontal"),
        OverlayActivation.WrongLayout(null), OverlayActivation.DecksNotFound, OverlayActivation.Active,
    ];

    static IEnumerable<OverlaySettings> SettingsVariants()
    {
        foreach (var enabled in new[] { true, false })
        {
            foreach (var limit in BPMLimit.AllCases)
            {
                foreach (var blind in new[] { true, false })
                {
                    foreach (var limitOn in new[] { true, false })
                    {
                        yield return new OverlaySettings(isEnabled: enabled, isBPMLimitEnabled: limitOn, bpmLimit: limit, isBlindModeEnabled: blind);
                    }
                }
            }
            foreach (var mode in WaveformMode.AllCases)
            {
                foreach (var meter in PhaseMeterType.AllCases)
                {
                    foreach (var meterOn in new[] { true, false })
                    {
                        yield return new OverlaySettings(isEnabled: enabled, waveformMode: mode, isPhaseMeterEnabled: meterOn, phaseMeterType: meter);
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryStatusLineGoesThroughTheLookup()
    {
        var unlocalized = Localized.Marked(() =>
        {
            var texts = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var activation in Activations)
            {
                foreach (var settings in SettingsVariants())
                {
                    foreach (var version in new[] { "7.2.19.0342", null })
                    {
                        foreach (var layout in new[] { null, "2Deck Vertical", "4Deck Horizontal" })
                        {
                            var context = new StatusContext(activation, settings, rekordboxVersion: version, layoutName: layout);
                            foreach (var line in new[] { StatusText.Connection(context), StatusText.BPMPrecision(context), StatusText.Waveform(context),
                                StatusText.PhaseMeter(context) })
                            {
                                if (!Localized.IsLocalized(line.Text)) texts.Add(line.Text);
                            }
                        }
                    }
                }
            }
            return texts;
        });
        Assert.True(unlocalized.Count == 0, "not localized: " + string.Join(" | ", unlocalized));
    }

    [Fact]
    public void ACaptureProblemFromTheAppIsShownAsItCame()
    {
        var context = new StatusContext(OverlayActivation.Active, new OverlaySettings(), rekordboxVersion: null, captureProblem: "⟦Capture failed⟧");

        Assert.Equal(new StatusLine(StatusTone.Waiting, "⟦Capture failed⟧"), Localized.Marked(() => StatusText.Waveform(context)));
    }

    [Fact]
    public void EveryActivationMessageGoesThroughTheLookup()
    {
        var messages = Localized.Marked(() => Activations.Select(activation => activation.Message).ToList());
        Assert.All(messages, message => Assert.True(Localized.IsLocalized(message), message));
    }

    [Fact]
    public void EveryOptionNameAndGuideGoesThroughTheLookup()
    {
        var texts = Localized.Marked(() =>
            BPMLimit.AllCases.SelectMany(limit => new[] { limit.DisplayName, limit.Explanation })
                .Concat(WaveformMode.AllCases.SelectMany(mode => new[] { mode.DisplayName, mode.Explanation }))
                .Concat(PhaseMeterType.AllCases.SelectMany(type => new[] { type.DisplayName, type.Title, type.Explanation }))
                .Concat(CounterStyle.AllCases.SelectMany(style => new[] { style.DisplayName, style.Title, style.Explanation }))
                .Concat(BoothPreset.AllCases.Select(preset => preset.Explanation))
                .Concat([BoothPreset.CustomExplanation])
                .Concat([OptionText.BoothTitle, OptionText.BoothSectionTitle, OptionText.BoothSubtitle, OptionText.CounterTitle,
                    OptionText.BlindModeTitle, OptionText.BlindModeSubtitle, OptionText.BlindModeMenuTitle,
                    OptionText.CounterMenuTitle, OptionText.BoothMenuTitle, OptionText.PhaseMeterStyleMenuTitle,
                    OptionText.MixTrainerTitle, OptionText.MixTrainerSubtitle, OptionText.MixTrainerMenuTitle, OptionText.NoOtherMaster])
                .ToList());
        var unlocalized = texts.Where(text => !Localized.IsLocalized(text)).ToList();
        Assert.True(unlocalized.Count == 0, "not localized: " + string.Join(" | ", unlocalized));
    }

    [Fact]
    public void HardwareLabelsStayInEnglishAsThePlayersPrintThem()
    {
        Assert.Equal(["CDJ-3000", "CDJ-2000NXS2", "XDJ-1000"], Localized.Marked(() => BoothPreset.AllCases.Select(preset => preset.DisplayName).ToList()));
    }

    [Fact]
    public void TheSpanishCatalogSpeaksToTheUserInTheSameRegisterAsMacOS()
    {
        var spanish = LocalizationCatalog.Load("es");
        var (waiting, wrongMode, connected, live) = L10n.WithOverride(spanish, () => (
            OverlayActivation.RekordboxNotRunning.Message,
            OverlayActivation.WrongMode(RekordboxMode.Export).Message,
            StatusText.Connection(new StatusContext(OverlayActivation.Active, new OverlaySettings(), "7.2.19.0342", layoutName: "2Deck Horizontal")).Text,
            StatusText.PhaseMeter(new StatusContext(OverlayActivation.Active, new OverlaySettings(phaseMeterType: PhaseMeterType.Box), null)).Text));

        Assert.Equal("Esperando rekordbox 7", waiting);
        Assert.Equal("Cambia rekordbox al modo PERFORMANCE", wrongMode);
        Assert.Equal("rekordbox 7.2.19.0342 conectado · 2Deck Horizontal", connected);
        Assert.Equal("En vivo: Cajas (CDJ-2000NXS / XDJ-1000)", live);
    }
}
