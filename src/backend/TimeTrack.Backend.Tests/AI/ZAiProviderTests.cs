using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Providers;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class ZAiProviderTests
{
    private static (ZAiProvider provider, Mock<HttpMessageHandler> handlerMock) CreateProvider(
        ZAiOptions? options = null)
    {
        var opts = options ?? new ZAiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.test.com/",
            Model = "glm-4.5-flash",
            MaxRetries = 2,
            CircuitBreakerFailures = 10,
            CircuitBreakerDurationSeconds = 30,
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri(opts.BaseUrl),
        };

        var loggerMock = new Mock<ILogger<ZAiProvider>>();
        var provider = new ZAiProvider(httpClient, Options.Create(opts), loggerMock.Object);

        return (provider, handlerMock);
    }

    private static void SetupHandler(Mock<HttpMessageHandler> handlerMock, HttpStatusCode statusCode, string body)
    {
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }));
    }

    private static string BuildSuccessResponse(string content, int? tokens = null)
    {
        var response = new Dictionary<string, object>
        {
            ["choices"] = new[]
            {
                new { message = new { role = "assistant", content } }
            },
        };

        if (tokens.HasValue)
            response["usage"] = new { total_tokens = tokens.Value };

        return JsonSerializer.Serialize(response);
    }

    [Fact]
    public async Task SendChatAsync_Success_ReturnsContent()
    {
        var (provider, handlerMock) = CreateProvider();
        SetupHandler(handlerMock, HttpStatusCode.OK,
            BuildSuccessResponse("Classification: productive", 150));

        var result = await provider.SendChatAsync("system", "user", "glm-4.5-flash", CancellationToken.None);

        result.Content.Should().Be("Classification: productive");
        result.TokensUsed.Should().Be(150);
        result.ModelUsed.Should().Be("glm-4.5-flash");
        result.LatencyMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task SendChatAsync_SendsAuthorizationHeader()
    {
        var (provider, handlerMock) = CreateProvider();
        HttpRequestMessage? capturedRequest = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildSuccessResponse("ok"), Encoding.UTF8, "application/json"),
            });

        await provider.SendChatAsync("system", "user", "glm-4.5-flash", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task SendChatAsync_SendsCorrectModelAndMessages()
    {
        var (provider, handlerMock) = CreateProvider();
        string? capturedBody = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedBody = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildSuccessResponse("ok"), Encoding.UTF8, "application/json"),
            });

        await provider.SendChatAsync("You are helpful", "Classify this", "glm-4.5-flash", CancellationToken.None);

        capturedBody.Should().NotBeNull();
        var parsed = JsonDocument.Parse(capturedBody!);
        parsed.RootElement.GetProperty("model").GetString().Should().Be("glm-4.5-flash");

        var messages = parsed.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("You are helpful");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("Classify this");
    }

    [Fact]
    public async Task SendChatAsync_ResponseWithoutUsage_TokensIsNull()
    {
        var (provider, handlerMock) = CreateProvider();
        var response = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = "ok" } } },
        });
        SetupHandler(handlerMock, HttpStatusCode.OK, response);

        var result = await provider.SendChatAsync("system", "user", "glm-4.5-flash", CancellationToken.None);

        result.Content.Should().Be("ok");
        result.TokensUsed.Should().BeNull();
    }

    [Fact]
    public async Task SendChatAsync_HttpError_ThrowsHttpRequestException()
    {
        var (provider, handlerMock) = CreateProvider(new ZAiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.test.com/",
            MaxRetries = 1,
            CircuitBreakerFailures = 10,
            CircuitBreakerDurationSeconds = 30,
        });
        SetupHandler(handlerMock, HttpStatusCode.InternalServerError, "server error");

        var act = () => provider.SendChatAsync("system", "user", "glm-4.5-flash", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendChatAsync_Retries_OnServerError()
    {
        var opts = new ZAiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.test.com/",
            MaxRetries = 2,
            CircuitBreakerFailures = 10,
            CircuitBreakerDurationSeconds = 30,
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        var callCount = 0;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("error"),
                    });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildSuccessResponse("recovered"), Encoding.UTF8, "application/json"),
                });
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri(opts.BaseUrl),
        };
        var loggerMock = new Mock<ILogger<ZAiProvider>>();
        var provider = new ZAiProvider(httpClient, Options.Create(opts), loggerMock.Object);

        var result = await provider.SendChatAsync("system", "user", "glm-4.5-flash", CancellationToken.None);

        result.Content.Should().Be("recovered");
        callCount.Should().Be(2);
    }

    [Fact]
    public void ProviderName_ReturnsZAi()
    {
        var (provider, _) = CreateProvider();
        provider.ProviderName.Should().Be("z.ai");
    }
}
