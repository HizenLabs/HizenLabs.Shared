using Facepunch;
using System;

namespace HizenLabs.Shared.Lang;

/// <summary>
/// Pooled holder for string.Format arguments: the holder comes from Facepunch.Pool, the object[]
/// from a shared ArrayPool, and Dispose returns both - so formatting allocates nothing but the
/// result string. Trailing null args shrink the rented array, so <c>Create(x, null, null)</c>
/// rents length 1.
/// <code>
/// using var args = TempArguments.Create(arg1, arg2, arg3);
/// var text = args.StringFormat(format);
/// </code>
/// </summary>
public class TempArguments : IDisposable, Pool.IPooled
{
    private static readonly ArrayPool<object> _argsPool = new(4);

    private object[] _args;

    public static TempArguments Create(object arg1)
    {
        var instance = Pool.Get<TempArguments>();
        if (arg1 == null)
        {
            return instance;
        }

        instance._args = _argsPool.Rent(1);
        instance._args[0] = arg1;
        return instance;
    }

    public static TempArguments Create(object arg1, object arg2)
    {
        if (arg2 == null)
        {
            return Create(arg1);
        }

        var instance = Pool.Get<TempArguments>();
        instance._args = _argsPool.Rent(2);
        instance._args[0] = arg1;
        instance._args[1] = arg2;
        return instance;
    }

    public static TempArguments Create(object arg1, object arg2, object arg3)
    {
        if (arg3 == null)
        {
            return Create(arg1, arg2);
        }

        var instance = Pool.Get<TempArguments>();
        instance._args = _argsPool.Rent(3);
        instance._args[0] = arg1;
        instance._args[1] = arg2;
        instance._args[2] = arg3;
        return instance;
    }

    /// <summary>Formats with the held args; a no-arg holder returns the format unchanged.</summary>
    public string StringFormat(string format)
    {
        if (_args == null)
        {
            return format;
        }

        return string.Format(format, _args);
    }

    public void Dispose()
    {
        var obj = this;
        Pool.Free(ref obj);
    }

    public void EnterPool()
    {
        if (_args != null)
        {
            _argsPool.Return(_args);
            _args = null;
        }
    }

    public void LeavePool()
    {
    }

    public static implicit operator object[](TempArguments instance)
    {
        return instance._args;
    }
}
