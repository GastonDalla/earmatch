using System.Text.Json;
using System.Text.Json.Serialization;

namespace Earmatch.Core;

public enum BPMLimit
{
    OneDecimal,
    NoDecimals,
    CoverFully,
}

public enum WaveformMode
{
    Normal,
    Split,
    HideBoth,
}

public enum PhaseMeterType
{
    Box,
    Line,
    Cdj3000PhaseMeter,
    Cdj3000Waveform,
}

public enum CounterStyle
{
    CueCountdown,
    BarPosition,
}

public static class BPMLimitMembers
{
    extension(BPMLimit)
    {
        public static IReadOnlyList<BPMLimit> AllCases => Enum.GetValues<BPMLimit>();
    }
}

public static class WaveformModeMembers
{
    extension(WaveformMode)
    {
        public static IReadOnlyList<WaveformMode> AllCases => Enum.GetValues<WaveformMode>();
    }
}

public static class PhaseMeterTypeMembers
{
    extension(PhaseMeterType type)
    {
        public static IReadOnlyList<PhaseMeterType> AllCases => Enum.GetValues<PhaseMeterType>();

        public bool IsCDJ3000 => type is PhaseMeterType.Cdj3000PhaseMeter or PhaseMeterType.Cdj3000Waveform;
    }
}

public static class CounterStyleMembers
{
    extension(CounterStyle)
    {
        public static IReadOnlyList<CounterStyle> AllCases => Enum.GetValues<CounterStyle>();
    }
}

[JsonConverter(typeof(OverlaySettingsJsonConverter))]
public record struct OverlaySettings
{
    public bool IsEnabled { get; set; }
    public bool IsBPMLimitEnabled { get; set; }
    public BPMLimit BPMLimit { get; set; }
    public WaveformMode WaveformMode { get; set; }
    public bool IsPhaseMeterEnabled { get; set; }
    public PhaseMeterType PhaseMeterType { get; set; }
    public CounterStyle CounterStyle { get; set; }
    public bool IsBlindModeEnabled { get; set; }
    public BoothPreset? BoothPreset { get; set; }
    public bool IsMixTrainerEnabled { get; set; }

    public OverlaySettings() : this(isEnabled: true)
    {
    }

    public OverlaySettings(
        bool isEnabled = true,
        bool isBPMLimitEnabled = true,
        BPMLimit bpmLimit = BPMLimit.OneDecimal,
        WaveformMode waveformMode = WaveformMode.Split,
        bool isPhaseMeterEnabled = true,
        PhaseMeterType phaseMeterType = PhaseMeterType.Line,
        CounterStyle counterStyle = CounterStyle.BarPosition,
        bool isBlindModeEnabled = false,
        BoothPreset? boothPreset = null,
        bool isMixTrainerEnabled = true)
    {
        IsEnabled = isEnabled;
        IsBPMLimitEnabled = isBPMLimitEnabled;
        BPMLimit = bpmLimit;
        WaveformMode = waveformMode;
        IsPhaseMeterEnabled = isPhaseMeterEnabled;
        PhaseMeterType = phaseMeterType;
        CounterStyle = counterStyle;
        IsBlindModeEnabled = isBlindModeEnabled;
        BoothPreset = boothPreset;
        IsMixTrainerEnabled = isMixTrainerEnabled;
    }
}

public static class EnumRawValue
{
    public static string Of<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static T? Parse<T>(string? raw) where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (Of(value) == raw) return value;
        }
        return null;
    }
}

public sealed class OverlaySettingsJsonConverter : JsonConverter<OverlaySettings>
{
    public override OverlaySettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var json = document.RootElement;
        var defaults = new OverlaySettings();
        if (json.ValueKind != JsonValueKind.Object) throw new JsonException("Overlay settings must be a JSON object.");

        bool Flag(string key, bool fallback) =>
            json.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

        T? Option<T>(string key) where T : struct, Enum =>
            json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? EnumRawValue.Parse<T>(value.GetString()) : null;

        return new OverlaySettings(
            isEnabled: Flag("isEnabled", defaults.IsEnabled),
            isBPMLimitEnabled: Flag("isBPMLimitEnabled", defaults.IsBPMLimitEnabled),
            bpmLimit: Option<BPMLimit>("bpmLimit") ?? defaults.BPMLimit,
            waveformMode: Option<WaveformMode>("waveformMode") ?? defaults.WaveformMode,
            isPhaseMeterEnabled: Flag("isPhaseMeterEnabled", defaults.IsPhaseMeterEnabled),
            phaseMeterType: Option<PhaseMeterType>("phaseMeterType") ?? defaults.PhaseMeterType,
            counterStyle: Option<CounterStyle>("counterStyle") ?? defaults.CounterStyle,
            isBlindModeEnabled: Flag("isBlindModeEnabled", defaults.IsBlindModeEnabled),
            boothPreset: Option<BoothPreset>("boothPreset"),
            isMixTrainerEnabled: Flag("isMixTrainerEnabled", defaults.IsMixTrainerEnabled));
    }

    public override void Write(Utf8JsonWriter writer, OverlaySettings settings, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isEnabled", settings.IsEnabled);
        writer.WriteBoolean("isBPMLimitEnabled", settings.IsBPMLimitEnabled);
        writer.WriteString("bpmLimit", EnumRawValue.Of(settings.BPMLimit));
        writer.WriteString("waveformMode", EnumRawValue.Of(settings.WaveformMode));
        writer.WriteBoolean("isPhaseMeterEnabled", settings.IsPhaseMeterEnabled);
        writer.WriteString("phaseMeterType", EnumRawValue.Of(settings.PhaseMeterType));
        writer.WriteString("counterStyle", EnumRawValue.Of(settings.CounterStyle));
        writer.WriteBoolean("isBlindModeEnabled", settings.IsBlindModeEnabled);
        if (settings.BoothPreset is { } preset) writer.WriteString("boothPreset", EnumRawValue.Of(preset));
        writer.WriteBoolean("isMixTrainerEnabled", settings.IsMixTrainerEnabled);
        writer.WriteEndObject();
    }
}
