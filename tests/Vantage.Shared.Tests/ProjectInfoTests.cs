using Vantage.Shared;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class ProjectInfoTests
{
    [Fact]
    public void ProjectInfo_exposes_project_wiring_sentinel()
    {
        Assert.Equal("vantage", ProjectInfo.Name);
        Assert.Equal(60, ProjectInfo.PhysicsTicksPerSecond);
    }
}
