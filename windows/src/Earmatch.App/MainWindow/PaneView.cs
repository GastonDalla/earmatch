using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Earmatch.App.Controls;
using Earmatch.App.Theme;

namespace Earmatch.App.Panes;

/// A pane of the main window: its title (with an optional accessory on the right) above its content
/// (PaneScaffold.swift). Refresh brings every control up to date with the model.
public abstract class PaneView : Grid
{
    protected PaneView(MainWindowModel model, string title)
    {
        Model = model;
        Margin = new Thickness(Spacing.Large, 14, Spacing.Large, 20);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(Spacing.Medium) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var heading = Ui.Text(title, TypeStyle.PaneTitle, WindowTheme.PrimaryText);
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        Header = Ui.Row(heading, null, Spacing.Medium);
        Header.MinHeight = 30;
        // The header sits in the title band like on macOS; its right end stops short of the caption buttons.
        Header.Margin = new Thickness(0, 0, CaptionButtons.StripWidth + Spacing.Small - Spacing.Large, 0);
        Children.Add(Header);
    }

    protected MainWindowModel Model { get; }

    protected Grid Header { get; }

    protected void SetAccessory(UIElement accessory)
    {
        if (accessory is FrameworkElement element)
        {
            element.Margin = new Thickness(Spacing.Medium, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
        }
        Grid.SetColumn(accessory, 1);
        Header.Children.Add(accessory);
    }

    protected void SetContent(UIElement content)
    {
        SetRow(content, 2);
        Children.Add(content);
    }

    public abstract void Refresh();

    /// Called on every frame while the pane is shown, for the animated previews.
    public virtual void Tick(DateTimeOffset now)
    {
    }

    /// Two equal columns 12 apart, each a stack of cards (PaneColumns.swift).
    protected static Grid Columns(UIElement leading, UIElement trailing)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(leading);
        SetColumn(trailing, 2);
        grid.Children.Add(trailing);
        return grid;
    }

    protected static Stack Column(params UIElement?[] cards)
    {
        var stack = Ui.VStack(12, cards);
        stack.VerticalAlignment = VerticalAlignment.Top;
        return stack;
    }
}
