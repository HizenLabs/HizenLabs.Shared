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

    // Point anchors: both corners on one spot of the parent, so the element has zero natural
    // size - place and size it with offsets. Rows top to bottom, columns left to right
    // (anchor space is 0-1 with y up, so "top" is y = 1).

    public static readonly MenuPosition TopLeft = new(0f, 1f, 0f, 1f);
    public static readonly MenuPosition TopCenter = new(0.5f, 1f, 0.5f, 1f);
    public static readonly MenuPosition TopRight = new(1f, 1f, 1f, 1f);

    public static readonly MenuPosition MiddleLeft = new(0f, 0.5f, 0f, 0.5f);
    /// <summary>Both corners on the parent's center - size the element with offsets.</summary>
    public static readonly MenuPosition Center = new(0.5f, 0.5f, 0.5f, 0.5f);
    public static readonly MenuPosition MiddleRight = new(1f, 0.5f, 1f, 0.5f);

    public static readonly MenuPosition BottomLeft = new(0f, 0f, 0f, 0f);
    public static readonly MenuPosition BottomCenter = new(0.5f, 0f, 0.5f, 0f);
    public static readonly MenuPosition BottomRight = new(1f, 0f, 1f, 0f);
}
