namespace Earmatch.Core;

public static class HardwareModel
{
    private static readonly string[] Placeholders =
    [
        "System manufacturer",
        "System Product Name",
        "To Be Filled By O.E.M.",
        "Default string",
        "Not Applicable",
        "None",
    ];

    public static string? Describe(string? manufacturer, string? product)
    {
        var maker = Meaningful(manufacturer);
        var name = Meaningful(product);
        if (name is null) return maker;
        if (maker is null || name.StartsWith(maker, StringComparison.OrdinalIgnoreCase)) return name;
        return $"{maker} {name}";
    }

    private static string? Meaningful(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        foreach (var placeholder in Placeholders)
        {
            if (string.Equals(trimmed, placeholder, StringComparison.OrdinalIgnoreCase)) return null;
        }
        return trimmed;
    }
}
