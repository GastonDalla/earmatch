using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Panes;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Dialogs;

public sealed class AboutWindow : DialogWindow
{
    public AboutWindow(Window? owner) : base(MenuText.About, owner)
    {
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var icon = new Image { Source = AppIcon.Large, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var name = Ui.Text("Earmatch", new TypeStyle(15, FontWeights.Bold), WindowTheme.PrimaryText);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.Margin = new Thickness(0, Spacing.Small + 4, 0, 0);
        var version = Ui.Text(Version, new TypeStyle(11, FontWeights.Normal), WindowTheme.SecondaryText);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        version.Margin = new Thickness(0, Spacing.Tight, 0, Spacing.Medium);
        var credit = SetupPane.CreditLines();
        credit.HorizontalAlignment = HorizontalAlignment.Center;
        Content = new Border
        {
            Width = 300,
            Padding = new Thickness(Spacing.Large, Spacing.Large, Spacing.Large, Spacing.Large - Spacing.Tight),
            Child = Ui.VStack(0, icon, name, version, credit),
        };
    }

    public static string Version => L10n.Format("Version %@", SystemInfo.VersionWithBuild ?? WindowText.DevelopmentBuild);
}
