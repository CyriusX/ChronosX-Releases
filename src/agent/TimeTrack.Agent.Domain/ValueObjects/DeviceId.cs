using System.Security.Cryptography;
using System.Text.Json;
using TimeTrack.Agent.Domain.Common;

namespace TimeTrack.Agent.Domain.ValueObjects;

/// <summary>
/// Identificador único do device that persists between sessions
/// Used for generating deterministic idempotency keys.
/// </summary>
public sealed class DeviceId : ValueObject
{
    private static DeviceId? _instance;
    private static readonly object _fileLock = new();
    private static readonly string _storageFileName = "device_id.json";

    /// <summary>
    /// Value of the device ID
    /// </summary>
    public string Value { get; }

    private DeviceId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or creates the singleton instance for the device
    /// </summary>
    public static DeviceId Current
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            lock (_fileLock)
            {
                _instance ??= ReadOrCreate();
                return _instance;
            }
        }
    }

    /// <summary>
    /// Loads from storage or creates if not exists
    /// </summary>
    private static DeviceId ReadOrCreate()
    {
        var filePath = GetFilePath();
        EnsureDirectoryExists();

        // Try to read existing
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var dto = JsonSerializer.Deserialize<DeviceIdDto>(json);
            if (dto?.Value != null)
            {
                _instance = new DeviceId(dto.Value);
                return _instance;
            }
        }

        // Create new
        var newDeviceId = new DeviceId(Guid.NewGuid().ToString("N"));
        SaveToFile(newDeviceId);
        _instance = newDeviceId;
        return newDeviceId;
    }

    /// <summary>
    /// Gets the file path for device ID storage
    /// </summary>
    private static string GetFilePath()
    {
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "Cyrius", "TimeTrack", _storageFileName);
    }

    /// <summary>
    /// Ensures the directory exists
    /// </summary>
    private static void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(GetFilePath());
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Saves device ID to file
    /// </summary>
    private static void SaveToFile(DeviceId deviceId)
    {
        var filePath = GetFilePath();
        EnsureDirectoryExists();

        var dto = new DeviceIdDto { Value = deviceId.Value };
        var json = JsonSerializer.Serialize(dto);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// DTO for JSON serialization
    /// </summary>
    private sealed class DeviceIdDto
    {
        public string Value { get; set; } = string.Empty;
    }

    #region ValueObject

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    #endregion
}
