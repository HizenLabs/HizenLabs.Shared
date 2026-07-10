namespace HizenLabs.Shared.UI;

/// <summary>
/// Pixel offsets applied to the anchor corners: (XMin, YMin) moves the bottom-left corner,
/// (XMax, YMax) the top-right. With <see cref="MenuPosition.Full"/> anchors, positive mins and
/// negative maxes inset the element from the parent's edges.
/// </summary>
public readonly struct MenuOffset
{
    public readonly float XMin;
    public readonly float YMin;
    public readonly float XMax;
    public readonly float YMax;

    public MenuOffset(float xMin, float yMin, float xMax, float yMax)
    {
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
    }

    public static readonly MenuOffset Zero = new(0f, 0f, 0f, 0f);
}
