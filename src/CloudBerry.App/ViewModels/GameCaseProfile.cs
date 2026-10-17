using System.Windows;

namespace CloudBerry.App.ViewModels;

/// <summary>Platform packaging proportions and materials, independent of the active UI theme.</summary>
public sealed record GameCaseProfile(string Label, double Width, double Height, string Shell,
    string Header, string HeaderText, double Radius = 5)
{
    public double RootWidth => Width + 14;
    public double RootHeight => Height + 8;
    public double SpineHeight => Height - 8;
    public Thickness CoverInset => new(5, 27, 5, 6);
    public CornerRadius Corners => new(Radius);
    public bool IsCartridge => Label is "Nintendo Entertainment System" or "SUPER NINTENDO" or "NINTENDO 64" or "GAME BOY" or "GAME BOY COLOR" or "GAME BOY ADVANCE";
    public bool IsDisc => !IsCartridge && Label is not "PC";

    public static GameCaseProfile Resolve(string? platform) => platform?.Trim().ToLowerInvariant() switch
    {
        "nintendo switch" or "switch" => new("NINTENDO SWITCH", 126, 244, "#AA2026", "#CF202B", "#FFFFFF"),
        "nintendo switch 2" => new("NINTENDO SWITCH 2", 126, 244, "#AA2026", "#CF202B", "#FFFFFF"),
        "nintendo ds" or "nds" => new("NINTENDO DS", 178, 160, "#363638", "#EAEAEA", "#171719"),
        "nintendo 3ds" or "3ds" => new("NINTENDO 3DS", 178, 160, "#EBEBE5", "#FAFAF6", "#B51C29"),
        "psp" or "playstation portable" => new("PSP", 138, 244, "#ECEDE8", "#F5F5F0", "#151515"),
        "ps vita" or "playstation vita" => new("PS VITA", 142, 184, "#1D6AB3", "#174D89", "#FFFFFF"),
        "playstation" or "ps1" or "psx" => new("PlayStation", 178, 158, "#65676B", "#202124", "#FFFFFF", 2),
        "playstation 2" or "ps2" => new("PlayStation 2", 174, 252, "#191A20", "#17171D", "#FFFFFF"),
        "playstation 3" or "ps3" => new("PS3", 174, 218, "#777B80", "#1B1B20", "#FFFFFF"),
        "playstation 4" or "ps4" => new("PS4", 174, 218, "#16569E", "#124D92", "#FFFFFF"),
        "playstation 5" or "ps5" => new("PS5", 174, 218, "#185EA9", "#F3F4F5", "#17191E"),
        "xbox" => new("XBOX", 174, 252, "#39822C", "#202C20", "#C7F49B"),
        "xbox 360" => new("XBOX 360", 174, 252, "#4B9526", "#EFF0E8", "#315D22"),
        "xbox one" or "xbox series x" or "xbox series x|s" => new("XBOX", 174, 218, "#28642D", "#225628", "#FFFFFF"),
        "gamecube" or "nintendo gamecube" => new("NINTENDO GAMECUBE", 174, 252, "#24212E", "#262231", "#FFFFFF"),
        "wii" => new("Wii", 174, 252, "#EEEEEA", "#FAFAF8", "#585D60"),
        "wii u" => new("Wii U", 174, 252, "#2593B7", "#13728E", "#FFFFFF"),
        "dreamcast" => new("Dreamcast", 178, 158, "#979CA1", "#EEEEEA", "#AF391C", 2),
        "sega saturn" or "saturn" => new("SEGA SATURN", 174, 248, "#313437", "#202529", "#FFFFFF", 2),
        "nes" => new("Nintendo Entertainment System", 174, 244, "#3F4246", "#292C30", "#FFFFFF", 1),
        "snes" => new("SUPER NINTENDO", 188, 140, "#46404F", "#322D39", "#FFFFFF", 1),
        "nintendo 64" or "n64" => new("NINTENDO 64", 188, 140, "#22282B", "#242B2E", "#FFFFFF", 1),
        "game boy" => new("GAME BOY", 162, 186, "#B6B9AE", "#E0E1D6", "#333A55", 1),
        "game boy color" => new("GAME BOY COLOR", 162, 186, "#D4D6CF", "#E8EAE4", "#383F5C", 1),
        "game boy advance" => new("GAME BOY ADVANCE", 188, 140, "#44435D", "#38364F", "#FFFFFF", 1),
        "mega drive" or "genesis" => new("SEGA GENESIS", 174, 244, "#252525", "#80212B", "#FFFFFF"),
        _ => new("PC", 174, 252, "#363B43", "#252A31", "#F2F5F8")
    };
}
