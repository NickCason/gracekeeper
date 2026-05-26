using GraceKeeper.Core;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ServiceControllerContractTests
{
    [Fact]
    public async Task IServiceController_StopAndStart_AreAwaitable()
    {
        var mock = new Mock<IServiceController>();
        mock.Setup(m => m.StopAsync("Foo", It.IsAny<System.TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(m => m.StartAsync("Foo", It.IsAny<System.TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(m => m.Exists("Foo")).Returns(true);

        var stopped = await mock.Object.StopAsync("Foo", System.TimeSpan.FromSeconds(10), default);
        var started = await mock.Object.StartAsync("Foo", System.TimeSpan.FromSeconds(10), default);
        Assert.True(stopped);
        Assert.True(started);
        Assert.True(mock.Object.Exists("Foo"));
    }
}
