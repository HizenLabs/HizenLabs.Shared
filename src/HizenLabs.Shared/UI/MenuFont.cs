namespace HizenLabs.Shared.UI;

/// <summary>The game's UI fonts (same asset names on both platforms).</summary>
public enum MenuFont
{
    RobotoCondensedRegular,
    RobotoCondensedBold,
    PermanentMarker,
    DroidSansMono,
    Poxel,
    LCD,
    PressStart,
}

internal static class MenuFontNames
{
    public static string Get(MenuFont font) => font switch
    {
        MenuFont.RobotoCondensedBold => "robotocondensed-bold.ttf",
        MenuFont.PermanentMarker => "permanentmarker.ttf",
        MenuFont.DroidSansMono => "droidsansmono.ttf",
        MenuFont.Poxel => "poxel.otf",
        MenuFont.LCD => "lcd.ttf",
        MenuFont.PressStart => "pressstart2p-regular.ttf",
        _ => "robotocondensed-regular.ttf",
    };
}
