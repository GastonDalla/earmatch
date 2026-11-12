using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// Checks the English and Spanish catalogs against every L10n call in the Windows sources, like the macOS
/// LocalizationCatalogTests does against Localizable.strings.
public partial class LocalizationCatalogTests
{
    public static readonly TheoryData<string> Languages = new("en", "es");

    enum Kind { Text, Template, Format, Plural }

    sealed record Use(Kind Kind, string Key, string Place);

    static string SourceRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "src"));

    [GeneratedRegex("""L10n\.(Text|Template|Format|Plural)\(\s*("(?:[^"\\]|\\.)*")?""")]
    private static partial Regex Call();

    [GeneratedRegex("""%(?:(\d+)\$)?[-+ #0']*(?:\d+|\*)?(?:\.(?:\d+|\*))?(hh|h|ll|l|q|z|t|j|L)?([@dDiuUxXoOfeEgGcCsSpaA%])""")]
    private static partial Regex Specifier();

    static readonly Lazy<(List<Use> Uses, List<string> Problems)> Scan = new(() =>
    {
        var uses = new List<Use>();
        var problems = new List<string>();
        var root = SourceRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Order())
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")) continue;
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var place = $"{relative}:{index + 1}";
                foreach (Match match in Call().Matches(lines[index]))
                {
                    var kind = Enum.Parse<Kind>(match.Groups[1].Value);
                    if (!match.Groups[2].Success)
                    {
                        problems.Add($"{place}: the key is not a string literal");
                        continue;
                    }
                    var literal = match.Groups[2].Value[1..^1];
                    uses.Add(new Use(kind, Regex.Unescape(literal), place));
                }
            }
        }
        return (uses, problems);
    });

    static IReadOnlyList<string> Arguments(string format)
    {
        var byPosition = new SortedDictionary<int, string>();
        var next = 1;
        foreach (Match match in Specifier().Matches(format))
        {
            var conversion = match.Groups[3].Value;
            if (conversion == "%") continue;
            var position = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : next;
            next = position + 1;
            byPosition[position] = conversion switch
            {
                "@" => "object",
                "f" or "e" or "E" or "g" or "G" or "a" or "A" => "double",
                _ => "integer",
            };
        }
        return byPosition.Values.ToList();
    }

    [Fact]
    public void EveryKeyIsAStringLiteral()
    {
        Assert.Empty(Scan.Value.Problems);
        Assert.True(Scan.Value.Uses.Count > 50, $"only {Scan.Value.Uses.Count} keys found under {SourceRoot()}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryKeyInTheCodeIsTranslated(string language)
    {
        var catalog = LocalizationCatalog.Load(language);
        var missing = Scan.Value.Uses
            .Where(use => use.Kind == Kind.Plural ? !catalog.Plurals.ContainsKey(use.Key) : !catalog.Strings.ContainsKey(use.Key))
            .Select(use => $"{use.Key} ({use.Place})")
            .ToList();
        Assert.True(missing.Count == 0, $"{language} is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryTranslationIsUsed(string language)
    {
        var catalog = LocalizationCatalog.Load(language);
        var used = Scan.Value.Uses.Select(use => use.Key).ToHashSet();
        var unused = catalog.Strings.Keys.Concat(catalog.Plurals.Keys).Where(key => !used.Contains(key)).Order().ToList();
        Assert.True(unused.Count == 0, $"{language} has unused keys: {string.Join(", ", unused)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TranslatedFormatsReadTheSameArguments(string language)
    {
        var catalog = LocalizationCatalog.Load(language);
        var broken = Scan.Value.Uses
            .Where(use => use.Kind is Kind.Format or Kind.Template)
            .Select(use => use.Key)
            .Distinct()
            .Where(key => catalog.Strings.TryGetValue(key, out var translation) && !Arguments(translation).SequenceEqual(Arguments(key)))
            .Select(key => $"{key} → {catalog.Strings[key]}")
            .ToList();
        Assert.True(broken.Count == 0, $"{language}: {string.Join(" | ", broken)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TextKeysReadNoArguments(string language)
    {
        var catalog = LocalizationCatalog.Load(language);
        var reading = Scan.Value.Uses
            .Where(use => use.Kind == Kind.Text)
            .Select(use => use.Key)
            .Distinct()
            .Where(key => catalog.Strings.TryGetValue(key, out var translation) && translation.Contains("%@"))
            .ToList();
        Assert.True(reading.Count == 0, $"{language}: {string.Join(" | ", reading)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void PluralFormsReadTheCount(string language)
    {
        var catalog = LocalizationCatalog.Load(language);
        foreach (var use in Scan.Value.Uses.Where(use => use.Kind == Kind.Plural))
        {
            Assert.True(catalog.Plurals.ContainsKey(use.Key), $"{language}: {use.Key}");
        }
        foreach (var (key, forms) in catalog.Plurals)
        {
            Assert.True(forms.ContainsKey("one") && forms.ContainsKey("other"), $"{language}: {key}");
            foreach (var (form, text) in forms)
            {
                var reads = Arguments(text);
                Assert.True(reads.Count == 0 || reads.SequenceEqual(["integer"]), $"{language}: {key} {form}");
            }
        }
    }

    [Fact]
    public void TheLibraryShipsEveryLanguage()
    {
        Assert.Equal(["en", "es"], LocalizationCatalog.Languages);
        foreach (var language in LocalizationCatalog.Languages)
        {
            Assert.Equal(language, LocalizationCatalog.Load(language).Language);
        }
    }
}
