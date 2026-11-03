<#
.SYNOPSIS
    Drives a development OmniDeck through UI Automation, without the mouse or the keyboard.

.DESCRIPTION
    For checking a Debug build by hand, beside an installed OmniDeck: run the Debug build with its own
    data root ($env:OMNIDECK_DATA_ROOT, see CONTRIBUTING.md), then point this script at its process.
    It never moves the mouse or types: it invokes elements through UI Automation and posts keys to
    OmniDeck's own windows. Not part of CI.

    Actions:
      windows   the process's top-level windows
      tree      the UI Automation tree (control view) down to -Depth
      audit     what screen readers would trip on: controls without a name, names that are a type's or
                a record's, list items that can't be invoked or selected, sibling controls sharing a name
      invoke    invokes, selects or toggles the element named -Name (within the element named -Within)
      open      opens -Name through the command palette, as Ctrl+K does
      capture   saves a PNG of the main window to -Path (PrintWindow: it need not be in front)
      minimize, restore, state   the main window's state

.EXAMPLE
    $env:OMNIDECK_DATA_ROOT = "$env:TEMP\OmniDeck.Dev"
    $app = Start-Process src\OmniDeck.App\bin\Debug\net10.0-windows\OmniDeck.exe -PassThru
    pwsh scripts/od-drive.ps1 -ProcessId $app.Id -Action open -Name Weather
    pwsh scripts/od-drive.ps1 -ProcessId $app.Id -Action audit
#>
param(
    [Parameter(Mandatory)] [int] $ProcessId,
    [Parameter(Mandatory)] [ValidateSet('windows', 'tree', 'audit', 'invoke', 'open', 'capture', 'minimize', 'restore', 'state')] [string] $Action,
    [string] $Name,
    [string] $Within,
    [string] $Path,
    [int] $Depth = 6
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -Namespace OdDrive -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
'@

$ae = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$windows = $ae::RootElement.FindAll($scope::Children, (New-Object System.Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $ProcessId)))
$main = (Get-Process -Id $ProcessId).MainWindowHandle

function Named([string] $text) { New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $text) }

function Patterns($element) { ($element.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName -replace 'PatternIdentifiers.Pattern', '' }) -join ',' }

function Children($element) {
    $child = $walker.GetFirstChild($element)
    while ($null -ne $child) {
        $child
        $child = $walker.GetNextSibling($child)
    }
}

function Show-Tree($element, [int] $level) {
    if ($level -gt $Depth) { return }
    foreach ($child in Children $element) {
        $c = $child.Current
        '{0}{1} "{2}" [{3}] {4}' -f ('  ' * $level), $c.ControlType.ProgrammaticName.Replace('ControlType.', ''), $c.Name, $c.AutomationId, (Patterns $child)
        Show-Tree $child ($level + 1)
    }
}

$needsName = 'Button', 'CheckBox', 'RadioButton', 'ComboBox', 'Edit', 'Hyperlink', 'ListItem', 'DataItem', 'TabItem', 'MenuItem', 'Slider', 'Spinner', 'TreeItem', 'SplitButton', 'Image'
function Audit($element, [string] $path) {
    $kids = @(Children $element)
    $kids | Where-Object { $_.Current.Name } |
        Group-Object { $_.Current.ControlType.ProgrammaticName + ' "' + $_.Current.Name + '"' } |
        Where-Object { $_.Count -gt 1 -and $_.Name -notmatch '^ControlType\.(Text|Separator)' } |
        ForEach-Object { 'SHARED    {0} x{1}  @ {2}' -f $_.Name.Replace('ControlType.', ''), $_.Count, $path }
    foreach ($kid in $kids) {
        $c = $kid.Current
        $type = $c.ControlType.ProgrammaticName.Replace('ControlType.', '')
        if ($c.IsOffscreen -and $type -ne 'TabItem') { continue }
        $patterns = Patterns $kid
        if ($type -in $needsName -and [string]::IsNullOrWhiteSpace($c.Name)) { 'NO NAME   {0} [{1}] {2}  @ {3}' -f $type, $c.AutomationId, $patterns, $path }
        if ($c.Name -match '\{ |^Od[A-Z]|^System\.|^Wpf\.|^OmniDeck\.|\d24$|Regular$') { 'BAD NAME  {0} "{1}"  @ {2}' -f $type, $c.Name, $path }
        if ($type -in 'DataItem', 'ListItem' -and $patterns -notmatch 'Invoke|SelectionItem|Toggle|ExpandCollapse') { 'NO ACTION {0} "{1}"  @ {2}' -f $type, $c.Name, $path }
        $label = if ($c.Name) { $c.Name.Substring(0, [Math]::Min(30, $c.Name.Length)) } else { $type }
        Audit $kid "$path > $label"
    }
}

function Press($element) {
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref] $pattern)) { $pattern.Invoke(); return 'invoked' }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref] $pattern)) { $pattern.Select(); return 'selected' }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref] $pattern)) { $pattern.Toggle(); return 'toggled' }
    'found, but it has no Invoke, SelectionItem or Toggle pattern'
}

switch ($Action) {
    'windows' {
        foreach ($w in $windows) { '"{0}" hwnd={1} class={2}' -f $w.Current.Name, $w.Current.NativeWindowHandle, $w.Current.ClassName }
    }
    'tree' {
        foreach ($w in $windows) { '== "{0}"' -f $w.Current.Name; Show-Tree $w 1 }
    }
    'audit' {
        foreach ($w in $windows) { '== "{0}"' -f $w.Current.Name; Audit $w $w.Current.Name }
    }
    'invoke' {
        foreach ($w in $windows) {
            $root = if ($Within) { $w.FindFirst($scope::Descendants, (Named $Within)) } else { $w }
            if ($null -eq $root) { continue }
            $target = $root.FindFirst($scope::Descendants, (Named $Name))
            if ($null -ne $target) { "$(Press $target) '$Name'"; return }
        }
        "not found: '$Name'"
    }
    'open' {
        # The title bar's search box opens the palette; Enter is posted to the palette's own window.
        $shell = $windows | Where-Object { $_.Current.NativeWindowHandle -eq [int] $main } | Select-Object -First 1
        # By automation ID, which no language changes.
        $search = New-Object System.Windows.Automation.PropertyCondition($ae::AutomationIdProperty, 'PaletteButton')
        $shell.FindFirst($scope::Descendants, $search).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

        # The palette is a window the main one owns: UI Automation lists it beside the main window or under it.
        $palette = $null
        for ($try = 0; $try -lt 20 -and $null -eq $palette; $try++) {
            Start-Sleep -Milliseconds 150
            $palette = $ae::RootElement.FindAll($scope::Children, (New-Object System.Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $ProcessId))) |
                Where-Object { $_.Current.NativeWindowHandle -ne [int] $main } | Select-Object -First 1
            if ($null -eq $palette) {
                $palette = $shell.FindFirst($scope::Children, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
            }
        }
        if ($null -eq $palette) { throw 'The palette did not open.' }
        $edit = $palette.FindFirst($scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
        $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Name)
        Start-Sleep -Milliseconds 400
        $hwnd = [IntPtr] $palette.Current.NativeWindowHandle
        [OdDrive.Native]::PostMessage($hwnd, 0x0100, [IntPtr] 0x0D, [IntPtr] 0x001C0001) | Out-Null
        [OdDrive.Native]::PostMessage($hwnd, 0x0101, [IntPtr] 0x0D, [IntPtr] 0xC01C0001) | Out-Null
        "opened '$Name' through the palette"
    }
    'capture' {
        if (-not $Path) { throw 'capture needs -Path.' }
        $rect = New-Object OdDrive.Native+RECT
        [OdDrive.Native]::GetWindowRect($main, [ref] $rect) | Out-Null
        $bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        [OdDrive.Native]::PrintWindow($main, $hdc, 2) | Out-Null
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        "saved $Path"
    }
    'minimize' { [OdDrive.Native]::ShowWindow($main, 7) | Out-Null; 'minimized' }
    'restore' { [OdDrive.Native]::ShowWindow($main, 4) | Out-Null; 'restored' }
    'state' { "hwnd=$main iconic=$([OdDrive.Native]::IsIconic($main)) visible=$([OdDrive.Native]::IsWindowVisible($main))" }
}
