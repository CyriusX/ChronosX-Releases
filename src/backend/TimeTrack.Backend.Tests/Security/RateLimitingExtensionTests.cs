using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using TimeTrack.Api.Extensions;
using Xunit;

namespace TimeTrack.Backend.Tests.Security;

/// <summary>
/// Unit tests for Rate Limiting functionality
/// </summary>
public class RateLimitingExtensionTests
{
    [Fact]
    public void GetClientIpAddress_ReturnsDirectIp_WhenNoForwardedHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        // Act
        var result = httpContext.GetClientIpAddress();

        // Assert
        result.Should().Be("192.168.1.100");
    }

    [Fact]
    public void GetClientIpAddress_ReturnsFirstForwardedIp_WhenForwardedHeaderPresent()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 198.51.100.1";

        // Act
        var result = httpContext.GetClientIpAddress();

        // Assert
        result.Should().Be("203.0.113.50");
    }

    [Fact]
    public void GetClientIpAddress_ReturnsUnknown_WhenNoIpAvailable()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = null;

        // Act
        var result = httpContext.GetClientIpAddress();

        // Assert
        result.Should().Be("unknown");
    }

    [Fact]
    public void GetUserPartitionKey_ReturnsUserId_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new Claim("sub", userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var result = httpContext.GetUserPartitionKey();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetUserPartitionKey_ReturnsIpAddress_WhenNotAuthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal() // Not authenticated
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var result = httpContext.GetUserPartitionKey();

        // Assert
        result.Should().Be("192.168.1.1");
    }

    [Fact]
    public void GetDevicePartitionKey_ReturnsDeviceId_WhenPresent()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("device_id", deviceId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var result = httpContext.GetDevicePartitionKey();

        // Assert
        result.Should().Be(deviceId);
    }

    [Fact]
    public void GetDevicePartitionKey_ReturnsUserId_WhenDeviceIdNotPresent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new Claim("sub", userId)
            // No device_id claim
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var result = httpContext.GetDevicePartitionKey();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetDevicePartitionKey_ReturnsIpAddress_WhenNotAuthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal() // Not authenticated
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var result = httpContext.GetDevicePartitionKey();

        // Assert
        result.Should().Be("192.168.1.1");
    }
}

/// <summary>
/// Tests for rate limiting policy names
/// </summary>
public class RateLimitingPolicyNamesTests
{
    [Fact]
    public void PolicyNames_HasCorrectValues()
    {
        // Assert
        RateLimitingExtensions.PolicyNames.Auth.Should().Be("AuthPolicy");
        RateLimitingExtensions.PolicyNames.Ingest.Should().Be("IngestPolicy");
        RateLimitingExtensions.PolicyNames.Reports.Should().Be("ReportsPolicy");
        RateLimitingExtensions.PolicyNames.Default.Should().Be("DefaultPolicy");
    }
}
