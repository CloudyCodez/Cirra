using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

/// <summary>Thin display wrapper around a saved <see cref="LibraryFilterPreset"/> for the Library filter bar's preset chip list.</summary>
public sealed class LibraryFilterPresetViewModel
{
    public LibraryFilterPresetViewModel(LibraryFilterPreset preset)
    {
        Preset = preset;
    }

    public LibraryFilterPreset Preset { get; }

    public string Name => Preset.Name;

    public string Id => Preset.Id;

    /// <summary>Human-readable description of what this preset filters on (e.g. "PS3, Needs metadata"), for the chip's tooltip.</summary>
    public string Summary => string.IsNullOrWhiteSpace(Preset.Summary) ? "Shows every game (no filters saved)" : Preset.Summary;
}
