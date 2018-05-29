using System.Collections.Generic;

namespace Lib.Configuration
{
    public interface IConfigurationDescription
    {
        IEnumerable<CfgDesc> Build();
        IReadOnlyList<CfgDesc> Describe();
        CfgDesc Describe(string key);
    }
}
