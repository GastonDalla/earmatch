# Dumps the UI Automation tree of rekordbox's windows to JSON: the fixture format of Earmatch for Windows' layout tests
# (tests/Earmatch.Core.Tests/Fixtures/uia-*.json). Run it in Windows with rekordbox open in the layout to capture.
# Usage: powershell.exe -NoProfile -ExecutionPolicy Bypass -File dump-uia.ps1 -Out C:\path\uia.json [-MaxNodes 20000] [-FullHelp] [-Fixture]
#
# Nodes follow UI Automation's raw view: `parent` and `depth` are what the TreeWalker reports, which JUCE keeps
# flat in places (most browser controls hang directly under the window's content). `helpText` keeps only its
# first line, the title the layout locator matches (for example "Deck BPM display:"); -FullHelp keeps it whole.
# `scale` is the rekordbox window's DPI / 96 (GetDpiForWindow), the factor between macOS points and these pixels.
# -Fixture keeps only the fields Earmatch.Core reads (index, parent, depth, controlType, name, helpText, value, frame),
# which is how tests/Earmatch.Core.Tests/Fixtures/uia-*.json are captured.
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$MaxNodes = 20000,
    [string]$ProcessName = "rekordbox",
    [switch]$FullHelp,
    [switch]$Fixture
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace EarmatchDump -Name Native -MemberDefinition '[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);'

$procs = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
if ($procs.Count -eq 0) { Write-Error "no $ProcessName process"; exit 2 }
$pids = $procs | ForEach-Object { $_.Id }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
$script:count = 0
$nodes = New-Object System.Collections.ArrayList

function Get-Value($element) {
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        try { return $pattern.Current.Value } catch { return $null }
    }
    return $null
}

function Add-Node($element, [int]$depth, [int]$parent) {
    if ($script:count -ge $MaxNodes) { return }
    $c = $element.Current
    $rect = $c.BoundingRectangle
    $index = $script:count
    $script:count++
    $node = [ordered]@{
        index        = $index
        parent       = $parent
        depth        = $depth
        controlType  = $c.ControlType.ProgrammaticName
        name         = $c.Name
        automationId = $c.AutomationId
        className    = $c.ClassName
        framework    = $c.FrameworkId
        helpText     = if ($FullHelp -or -not $c.HelpText) { $c.HelpText } else { ($c.HelpText -split "`r?`n")[0].TrimEnd() }
        value        = (Get-Value $element)
        offscreen    = $c.IsOffscreen
        enabled      = $c.IsEnabled
        frame        = if ($rect.IsEmpty) { $null } else { @([math]::Round($rect.X, 1), [math]::Round($rect.Y, 1), [math]::Round($rect.Width, 1), [math]::Round($rect.Height, 1)) }
    }
    if ($Fixture) { foreach ($k in "automationId", "className", "framework", "offscreen", "enabled") { $node.Remove($k) } }
    [void]$nodes.Add($node)
    $child = $walker.GetFirstChild($element)
    while ($child -ne $null -and $script:count -lt $MaxNodes) {
        try { Add-Node $child ($depth + 1) $index } catch { }
        $child = $walker.GetNextSibling($child)
    }
}

$scale = $null
$top = $walker.GetFirstChild($root)
while ($top -ne $null) {
    try {
        if ($pids -contains $top.Current.ProcessId) {
            if ($scale -eq $null -and $top.Current.NativeWindowHandle -ne 0) {
                $dpi = [EarmatchDump.Native]::GetDpiForWindow([IntPtr]$top.Current.NativeWindowHandle)
                if ($dpi -gt 0) { $scale = [math]::Round($dpi / 96.0, 4) }
            }
            Add-Node $top 0 -1
        }
    } catch { }
    $top = $walker.GetNextSibling($top)
}

$result = [ordered]@{
    capturedAt = (Get-Date).ToString("o")
    os         = [System.Environment]::OSVersion.VersionString
    arch       = $env:PROCESSOR_ARCHITECTURE
    scale      = $scale
    process    = ($procs | Select-Object -First 1 | ForEach-Object { @{ id = $_.Id; path = $_.Path; version = $_.MainModule.FileVersionInfo.ProductVersion } })
    truncated  = ($script:count -ge $MaxNodes)
    nodes      = $nodes
}
$result | ConvertTo-Json -Depth 6 -Compress | Set-Content -Path $Out -Encoding UTF8
Write-Output "wrote $($script:count) nodes to $Out"
