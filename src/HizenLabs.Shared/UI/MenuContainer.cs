namespace HizenLabs.Shared.UI;

/// <summary>
/// Handle to a menu element. The id IS the element's client-side name - the one address that
/// works for creating children and updating across sends, on both platforms. Declare stable ids
/// as constants; the implicit conversion lifts them into handles with no allocation.
/// </summary>
public readonly struct MenuContainer
{
    public readonly string Id;

    public MenuContainer(string id)
    {
        Id = id;
    }

    public static implicit operator MenuContainer(string id) => new(id);
}
