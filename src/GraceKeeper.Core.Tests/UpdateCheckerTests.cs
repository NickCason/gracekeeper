using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GraceKeeper.Core;
using Moq;
using Moq.Protected;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class UpdateCheckerTests
{
    private static HttpClient MakeClient(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = status, Content = new StringContent(body) });
        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task CheckAsync_NewerAvailable_ReturnsUpdateInfo()
    {
        var client = MakeClient(HttpStatusCode.OK, "{\"tag_name\":\"v0.2.0\",\"html_url\":\"https://github.com/x/y/releases/v0.2.0\"}");
        var checker = new UpdateChecker(client, "https://api.github.com/repos/x/y/releases/latest");

        var result = await checker.CheckAsync(new Version("0.1.0"));

        Assert.NotNull(result);
        Assert.Equal("0.2.0", result.Version.ToString());
    }

    [Fact]
    public async Task CheckAsync_SameVersion_ReturnsNull()
    {
        var client = MakeClient(HttpStatusCode.OK, "{\"tag_name\":\"v0.1.0\",\"html_url\":\"...\"}");
        var checker = new UpdateChecker(client, "https://api.github.com/repos/x/y/releases/latest");

        var result = await checker.CheckAsync(new Version("0.1.0"));

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_HttpError_ReturnsNullSilently()
    {
        var client = MakeClient(HttpStatusCode.InternalServerError, "");
        var checker = new UpdateChecker(client, "https://api.github.com/repos/x/y/releases/latest");

        var result = await checker.CheckAsync(new Version("0.1.0"));

        Assert.Null(result);
    }
}
