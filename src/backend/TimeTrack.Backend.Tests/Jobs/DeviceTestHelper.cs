using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

using Xunit;

namespace TimeTrack.Backend.Tests.Helpers;

/// <summary>
/// Helper methods for creating test entities
/// </summary>
public static class DeviceTestHelper
{
    public static Device CreateTestDevice(
        Guid orgId,
        Guid userId)
    {
        var deviceId = Guid.NewGuid();
        return Device.Create(
            deviceId,
            orgId,
            userId,
            "TEST-HOSTNAME",
            "1.0.0",
            DisplayMode.Background);
    }
}
