namespace HizenLabs.Shared.UI;

/// <summary>
/// Anchor rectangle (fractions of the parent, 0-1): where the element's corners attach.
/// Pair with a <see cref="MenuOffset"/> for pixel adjustments from those anchors.
/// </summary>
public readonly struct MenuPosition
{
    public readonly float XMin;
    public readonly float YMin;
    public readonly float XMax;
    public readonly float YMax;

    public MenuPosition(float xMin, float yMin, float xMax, float yMax)
    {
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
    }

    /// <summary>Anchored to the parent's full rect.</summary>
    public static readonly MenuPosition Full = new(0f, 0f, 1f, 1f);

    /// <summary>Both corners on the parent's center - size the element with offsets.</summary>
    public static readonly MenuPosition Center = new(0.5f, 0.5f, 0.5f, 0.5f);
}
