using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CloudBerry.App;

/// <summary>A keyboard-accessible reference that uses the active application theme.</summary>
public sealed class QuickGuideWindow : Window
{
    public QuickGuideWindow()
    {
        Title = "Getting started with Cirra";
        Width = 720;
        Height = 720;
        MinWidth = 460;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "AppBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");

        var sections = new StackPanel { Margin = new Thickness(28) };
        AddText(sections, "Make yourself at home", 26, true);
        AddText(sections, "Your games, artwork, manuals, and play history in one place. Start with a few games; you can organize the rest later.");
        AddSection(sections, "1. Add your games",
            "Use Add Folder to scan a games or ROM folder. Review the checked results before importing. Already-owned games stay unchecked. Add Game lets you choose one title manually.");
        AddSection(sections, "2. Make the library yours",
            "Search by title, switch between list and grid, or choose a collection. More Filters opens platform and achievement filters. Clear Filters brings hidden games back without removing anything.");
        AddSection(sections, "3. Fill in the details",
            "Select a game and choose Edit Selected to find metadata or artwork. Review the match before applying it. Guides & Manuals can discover available PDFs or attach your own local PDF.");
        AddSection(sections, "4. Set up emulation",
            "Open Emulation, find installed emulators, then apply the detected mappings. Browse lets you choose an emulator yourself. RetroArch needs a compatible core for each system; detection alone does not guarantee a game is ready to launch.");
        AddSection(sections, "Desktop shortcuts",
            "Ctrl+K — find a game, page, theme, or action\nCtrl+F — search your library\nF11 — enter Horizon\nCtrl+B — show or hide the sidebar\nCtrl+1 through Ctrl+8 — switch pages\nF1 — open this guide");
        AddSection(sections, "Horizon from the couch",
            "Use the D-pad or left stick to browse, A to open or confirm, B to go back, and bumpers to change pages. On the Library page, X switches the grid. Start returns to desktop. Release the controls after returning from a game before navigating again.");
        AddSection(sections, "Reading original manuals",
            "Left/Right or Page Up/Page Down turn pages. Home/End jump to the first or last page. Enter a PDF page number to jump directly; facing-page mode displays its matching spread. Escape closes the reader.");
        AddSection(sections, "If something needs attention",
            "For missing games, clear filters and check that the selected folder contains the install or ROM. Review scan warnings for unreadable paths. For a failed launch, check the target and emulator mapping. If a guide cannot open, retry or use its Source page. Keep a library backup before large cleanup operations.");
        var close = new Button { Content = "Got it", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        close.SetResourceReference(StyleProperty, "PrimaryButtonStyle");
        close.Click += (_, _) => Close();
        sections.Children.Add(close);
        Content = new ScrollViewer { Content = sections, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static void AddSection(Panel parent, string title, string text)
    {
        AddText(parent, title, 17, true, 22);
        AddText(parent, text);
    }

    private static void AddText(Panel parent, string text, double size = 14, bool heading = false, double top = 8)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5, Margin = new Thickness(0, top, 0, 0), FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal };
        block.SetResourceReference(TextBlock.ForegroundProperty, heading ? "TextPrimaryBrush" : "TextSecondaryBrush");
        parent.Children.Add(block);
    }
}
