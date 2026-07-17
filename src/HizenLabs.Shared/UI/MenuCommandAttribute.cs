/// <summary>
/// Marks the handler of a menu button command. Carbon resolves it through the
/// protected-command table (buttons emit Community.Protect of the same string); Oxide runs it
/// as a plain console command. The platform split lives in the base list, NOT as two class
/// declarations: the bundler parses shared sources with one platform's defines, and a type
/// declared once per #if branch collapses to a single branch in the emitted plugin - the
/// buttons then protect while the handler registers raw, and every click dies silently.
/// Directives inside a declaration ride through as trivia, so this form ships intact.
/// </summary>
public class MenuCommandAttribute :
#if CARBON
    ProtectedCommandAttribute
#else
    ConsoleCommandAttribute
#endif
{
    public MenuCommandAttribute(string command) : base(command) { }
}
