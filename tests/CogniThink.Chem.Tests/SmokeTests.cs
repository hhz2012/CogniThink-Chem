using CogniThink.Chem.Core;
using Xunit;

namespace CogniThink.Chem.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        Assert.Equal("CogniThink.Chem", ModuleInfo.Name);
        Assert.False(string.IsNullOrWhiteSpace(ModuleInfo.Version));
    }
}
