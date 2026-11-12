using Earmatch.Core;

namespace Earmatch.Core.Tests;

static class Fixture
{
    public const double Scale = 2;

    public static string PathOf(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static RekordboxWindowSnapshot Snapshot(string name) => UiaDump.Parse(File.ReadAllText(PathOf(name)), Scale);

    public static RekordboxLayout Layout(string name) => RekordboxLayout.Locate(Snapshot(name));

    public static RekordboxWindowSnapshot Changing(this RekordboxWindowSnapshot snapshot, Func<UiaElementSnapshot, bool> matches,
        Func<UiaElementSnapshot, UiaElementSnapshot> change) =>
        snapshot with { Elements = snapshot.Elements.Select(element => matches(element) ? change(element) : element).ToList() };

    public static RekordboxWindowSnapshot Removing(this RekordboxWindowSnapshot snapshot, Func<UiaElementSnapshot, bool> matches) =>
        snapshot with { Elements = snapshot.Elements.Where(element => !matches(element)).ToList() };

    public static ScreenRect Rect(double x, double y, double width, double height) => new(x, y, width, height);
}
