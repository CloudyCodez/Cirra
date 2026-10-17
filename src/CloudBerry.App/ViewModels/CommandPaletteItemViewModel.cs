namespace CloudBerry.App.ViewModels;

public sealed class CommandPaletteItemViewModel
{
    public CommandPaletteItemViewModel(
        string id,
        string title,
        string subtitle,
        string category,
        string glyph,
        string searchText,
        GameEntryViewModel? game = null)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        Category = category;
        Glyph = glyph;
        SearchText = searchText;
        Game = game;
    }

    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Category { get; }
    public string Glyph { get; }
    public string SearchText { get; }
    public GameEntryViewModel? Game { get; }
    public bool IsGame => Game is not null;
    public string CoverImagePath => Game?.DisplayCoverImagePath ?? string.Empty;
}
