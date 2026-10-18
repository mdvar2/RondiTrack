using Xunit;

namespace RondiTrack.Tests;

public class CiFailureDemoTests
{
    [Fact]
    public void Ci_Should_Detect_A_Failing_Test()
    {
        Assert.True(false, "Intentional failure to verify GitHub Actions CI.");
    }
}