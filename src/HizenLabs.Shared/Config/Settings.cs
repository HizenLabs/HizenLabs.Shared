using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HizenLabs.Shared.Config;

public static class Settings
{
    public static void Init<T>() where T : BaseConfig, new()
    {

    }
}
