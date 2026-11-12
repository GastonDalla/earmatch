using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;

namespace Earmatch.App.Controls;

internal sealed class AccentSwitchAutomationPeer(AccentSwitch owner) : FrameworkElementAutomationPeer(owner), IToggleProvider
{
    public ToggleState ToggleState => owner.IsOn ? ToggleState.On : ToggleState.Off;

    public void Toggle()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        owner.ToggleByUser();
    }

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

    internal void RaiseToggled(bool wasOn, bool isOn) => RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
        wasOn ? ToggleState.On : ToggleState.Off, isOn ? ToggleState.On : ToggleState.Off);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetLocalizedControlTypeCore() => WindowText.ToggleSwitch;

    protected override string GetClassNameCore() => nameof(AccentSwitch);

    protected override List<AutomationPeer> GetChildrenCore() => [];
}

internal sealed class ActionButtonAutomationPeer(ActionButton owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    public void Invoke()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        owner.InvokeByUser();
    }

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        owner.Kind == ActionKind.Link ? AutomationControlType.Hyperlink : AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(ActionButton);

    protected override List<AutomationPeer> GetChildrenCore() => [];
}

public sealed class SpokenGroup : Decorator
{
    public SpokenGroup(UIElement child, Func<string> describe)
    {
        Child = child;
        Describe = describe;
    }

    public Func<string> Describe { get; set; }

    public Func<string>? DescribeValue { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new SpokenGroupAutomationPeer(this);
}

internal sealed class SpokenGroupAutomationPeer(SpokenGroup owner) : FrameworkElementAutomationPeer(owner), IValueProvider
{
    public bool IsReadOnly => true;

    public string Value => owner.DescribeValue?.Invoke() ?? "";

    public void SetValue(string value) => throw new InvalidOperationException();

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value && owner.DescribeValue is not null ? this : base.GetPattern(patternInterface);

    protected override string GetNameCore() => owner.Describe();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(SpokenGroup);

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer> GetChildrenCore() => [];
}

internal interface ISegmentedOptions
{
    int OptionCount { get; }
    int SelectedOptionIndex { get; }
    string OptionLabel(int index);
    string OptionHelp(int index);
    Rect OptionBounds(int index);
    void SelectOption(int index);
}

internal sealed class SegmentedPickerAutomationPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    private readonly List<SegmentAutomationPeer> _segments = [];

    internal ISegmentedOptions Options => (ISegmentedOptions)Owner;

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => true;

    public IRawElementProviderSimple[] GetSelection()
    {
        var index = Options.SelectedOptionIndex;
        var segments = Segments();
        return index >= 0 && index < segments.Count ? [ProviderFromPeer(segments[index])] : [];
    }

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    internal void RaiseSelected(int index)
    {
        var segments = Segments();
        if (index >= 0 && index < segments.Count) segments[index].RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }

    internal IRawElementProviderSimple Provider() => ProviderFromPeer(this);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string GetClassNameCore() => "SegmentedPicker";

    protected override List<AutomationPeer> GetChildrenCore() => [.. Segments()];

    private List<SegmentAutomationPeer> Segments()
    {
        var count = Options.OptionCount;
        while (_segments.Count < count) _segments.Add(new SegmentAutomationPeer(this, _segments.Count));
        if (_segments.Count > count) _segments.RemoveRange(count, _segments.Count - count);
        return _segments;
    }
}

internal sealed class SegmentAutomationPeer(SegmentedPickerAutomationPeer picker, int index) : AutomationPeer, ISelectionItemProvider
{
    private FrameworkElement Owner => picker.Owner as FrameworkElement ?? throw new InvalidOperationException();

    public bool IsSelected => picker.Options.SelectedOptionIndex == index;

    public IRawElementProviderSimple SelectionContainer => picker.Provider();

    public void Select()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        picker.Options.SelectOption(index);
    }

    public void AddToSelection()
    {
        if (!IsSelected) Select();
    }

    public void RemoveFromSelection()
    {
        if (IsSelected) throw new InvalidOperationException();
    }

    public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : null!;

    protected override string GetAcceleratorKeyCore() => "";

    protected override string GetAccessKeyCore() => "";

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetAutomationIdCore() => "";

    protected override Rect GetBoundingRectangleCore()
    {
        var owner = Owner;
        if (PresentationSource.FromVisual(owner) is null) return Rect.Empty;
        var bounds = picker.Options.OptionBounds(index);
        if (bounds.IsEmpty) return Rect.Empty;
        return new Rect(owner.PointToScreen(bounds.TopLeft), owner.PointToScreen(bounds.BottomRight));
    }

    protected override List<AutomationPeer> GetChildrenCore() => [];

    protected override string GetClassNameCore() => "SegmentedPickerOption";

    protected override Point GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return bounds.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    protected override string GetHelpTextCore() => picker.Options.OptionHelp(index);

    protected override string GetItemStatusCore() => "";

    protected override string GetItemTypeCore() => "";

    protected override AutomationPeer GetLabeledByCore() => null!;

    protected override string GetNameCore() => picker.Options.OptionLabel(index);

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override int GetPositionInSetCore() => index + 1;

    protected override int GetSizeOfSetCore() => picker.Options.OptionCount;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => Owner.IsEnabled;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool IsOffscreenCore() => picker.IsOffscreen();

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override void SetFocusCore()
    {
    }
}

public sealed class SelectionList : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectionListAutomationPeer(this);

    internal IEnumerable<SelectionRow> Rows()
    {
        var pending = new Stack<DependencyObject>();
        pending.Push(this);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            for (var index = VisualTreeHelper.GetChildrenCount(current) - 1; index >= 0; index--)
            {
                var child = VisualTreeHelper.GetChild(current, index);
                if (child is SelectionRow row) yield return row;
                else pending.Push(child);
            }
        }
    }

    internal void RaiseSelected(SelectionRow row)
    {
        if (UIElementAutomationPeer.FromElement(row) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }
}

internal sealed class SelectionListAutomationPeer(SelectionList owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => true;

    public IRawElementProviderSimple[] GetSelection()
    {
        foreach (var row in owner.Rows())
        {
            if (row.IsSelected() && UIElementAutomationPeer.CreatePeerForElement(row) is { } peer) return [ProviderFromPeer(peer)];
        }
        return [];
    }

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    internal IRawElementProviderSimple Provider() => ProviderFromPeer(this);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string GetClassNameCore() => nameof(SelectionList);
}

public sealed class SelectionRow(Func<bool> isSelected, Action select) : Border
{
    internal bool IsSelected() => isSelected();

    internal void Select() => select();

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectionRowAutomationPeer(this);
}

internal sealed class SelectionRowAutomationPeer(SelectionRow owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider
{
    public bool IsSelected => owner.IsSelected();

    public IRawElementProviderSimple SelectionContainer
    {
        get
        {
            DependencyObject? current = owner;
            while (current is not null && current is not SelectionList) current = VisualTreeHelper.GetParent(current);
            return current is SelectionList list && UIElementAutomationPeer.CreatePeerForElement(list) is SelectionListAutomationPeer peer
                ? peer.Provider()
                : null!;
        }
    }

    public void Select()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        owner.Select();
    }

    public void AddToSelection()
    {
        if (!IsSelected) Select();
    }

    public void RemoveFromSelection()
    {
        if (IsSelected) throw new InvalidOperationException();
    }

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => nameof(SelectionRow);

    protected override List<AutomationPeer> GetChildrenCore() => [];
}
