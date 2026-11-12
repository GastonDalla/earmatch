using System.Windows.Automation;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

/// Reads rekordbox's main window through UI Automation (AccessibilityReader.swift): the whole raw-view tree in one
/// cached request, as the nodes dump-uia.ps1 writes and the snapshot the locator reads, then the values of a few
/// elements (header BPMs, times) between full reads. It only reads: it never invokes, focuses or sets anything.
internal sealed class UiaWindowReader
{
    private readonly CacheRequest _request = MakeRequest();
    private List<(int Index, AutomationElement Element)> _watched = [];

    /// Whether a watched element went away since the last full read (rekordbox rebuilt part of its window).
    public bool IsStale { get; private set; }

    /// A full read: the nodes, the snapshot and the UI Automation element of each snapshot element.
    public sealed record Read(IReadOnlyList<UiaNode> Nodes, RekordboxWindowSnapshot Snapshot, IReadOnlyList<AutomationElement> Elements);

    /// The window's nodes and snapshot; Scale is the window's DPI / 96. Throws the UI Automation exceptions
    /// (ElementNotAvailableException, TimeoutException, COMException...) when rekordbox does not answer. The read is
    /// only kept for ReadWatched once it is adopted.
    public static Read ReadWindow(IntPtr window, CacheRequest request)
    {
        var root = AutomationElement.FromHandle(window).GetUpdatedCache(request);
        var nodes = new List<UiaNode>();
        var references = new List<AutomationElement>();
        var pending = new Stack<(AutomationElement Element, int Depth, int Parent)>();
        pending.Push((root, 0, -1));
        while (pending.TryPop(out var item))
        {
            var index = nodes.Count;
            nodes.Add(Node(item.Element, index, item.Depth, item.Parent));
            references.Add(item.Element);
            var children = item.Element.CachedChildren;
            for (var child = children.Count - 1; child >= 0; child--) pending.Push((children[child], item.Depth + 1, index));
        }
        var dpi = NativeMethods.GetDpiForWindow(window);
        var snapshot = UiaDump.Snapshot(nodes, dpi == 0 ? 1 : dpi / 96.0, out var positions);
        return new Read(nodes, snapshot, positions.Select(position => references[position]).ToArray());
    }

    public Read ReadWindow(IntPtr window) => ReadWindow(window, _request);

    /// Keeps the read's elements and watches some of them (indices in its snapshot) for ReadWatched.
    public void Adopt(Read read, IReadOnlyList<int> watched)
    {
        _watched = watched.Where(index => index < read.Elements.Count).Select(index => (index, read.Elements[index])).ToList();
        IsStale = false;
    }

    /// The current values of the watched elements; an element that cannot be read is left out.
    public Dictionary<int, string> ReadWatched()
    {
        var values = new Dictionary<int, string>();
        foreach (var (index, element) in _watched)
        {
            try
            {
                if (element.GetCurrentPropertyValue(ValuePattern.ValueProperty, true) is string value) values[index] = value;
            }
            catch (ElementNotAvailableException)
            {
                IsStale = true;
            }
        }
        return values;
    }

    public void Reset()
    {
        _watched = [];
        IsStale = false;
    }

    private static UiaNode Node(AutomationElement element, int index, int depth, int parent)
    {
        var type = element.GetCachedPropertyValue(AutomationElement.ControlTypeProperty, true) as ControlType;
        var rect = element.GetCachedPropertyValue(AutomationElement.BoundingRectangleProperty, true) is System.Windows.Rect bounds && !bounds.IsEmpty
            ? new ScreenRect(bounds.X, bounds.Y, bounds.Width, bounds.Height)
            : (ScreenRect?)null;
        return new UiaNode(index, parent, depth, type?.ProgrammaticName,
            element.GetCachedPropertyValue(AutomationElement.NameProperty, true) as string,
            UiaNode.FirstLine(element.GetCachedPropertyValue(AutomationElement.HelpTextProperty, true) as string),
            element.GetCachedPropertyValue(ValuePattern.ValueProperty, true) as string,
            rect);
    }

    public static CacheRequest MakeRequest()
    {
        var request = new CacheRequest
        {
            TreeFilter = Automation.RawViewCondition,
            TreeScope = TreeScope.Element | TreeScope.Descendants,
            AutomationElementMode = AutomationElementMode.Full,
        };
        request.Add(AutomationElement.ControlTypeProperty);
        request.Add(AutomationElement.NameProperty);
        request.Add(AutomationElement.HelpTextProperty);
        request.Add(AutomationElement.BoundingRectangleProperty);
        request.Add(ValuePattern.ValueProperty);
        return request;
    }
}
