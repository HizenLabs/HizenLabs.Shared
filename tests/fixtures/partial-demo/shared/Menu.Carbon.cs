namespace HizenLabs.Demo.Shared;

public partial class Menu
{
#if CARBON

    private partial void DisposeCore()
    {
        CarbonOnly();
    }

    private static void CarbonOnly()
    {
    }

#endif
}
