namespace HizenLabs.Demo.Shared;

public interface IWidget
{
    void Do();
}

public sealed class Widget : IWidget
{
    public void Do()
    {
    }

    public void Extra()
    {
    }

    private void Hidden()
    {
    }
}
