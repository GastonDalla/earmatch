using System.Globalization;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

[CollectionDefinition(nameof(L10nInstallCollection), DisableParallelization = true)]
public class L10nInstallCollection;

[Collection(nameof(L10nInstallCollection))]
public class L10nInstallTests
{
    [Fact]
    public async Task TheInstalledLanguageAnswersEveryThreadUntilTheNextInstall()
    {
        L10n.Lookup spanish = key => key == "Capture stopped" ? "Captura detenida" : key;
        L10n.Install(spanish, CultureInfo.GetCultureInfo("es-ES"));
        try
        {
            var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                new[] { L10n.Text("Capture stopped"), L10n.Format("%.1f ms", 2.5), L10n.Culture?.Name ?? "-" })));
            Assert.All(answers, answer => Assert.Equal(["Captura detenida", "2,5 ms", "es-ES"], answer));
            Assert.Equal(["Capture stopped", "2.5 ms"], L10n.English(() => new[] { L10n.Text("Capture stopped"), L10n.Format("%.1f ms", 2.5) }));
        }
        finally
        {
            L10n.Install(key => key, CultureInfo.InvariantCulture);
        }
        Assert.Equal("Capture stopped", L10n.Text("Capture stopped"));
        Assert.Equal("2.5 ms", L10n.Format("%.1f ms", 2.5));
    }
}
