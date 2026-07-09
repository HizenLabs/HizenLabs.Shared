#if CARBON
public class MenuCommandAttribute : ProtectedCommandAttribute
{
    public MenuCommandAttribute(string name) : base(name) { }
}
#else
public class MenuCommandAttribute : ConsoleCommandAttribute
{
    public MenuCommandAttribute(string command) : base(command) { }
}
#endif