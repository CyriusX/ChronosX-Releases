using System.Security.Cryptography;
using System.Text;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Services
{
    /// <summary>
    /// Generates deterministic idempotency keys
    /// SHA-256(deviceId + rangeStartUtc + entityType)
    /// </summary>
    public interface IIdempotencyKeyGenerator
    {
        /// <summary>
        /// Generates an idempotency key
        /// </summary>
        string Generate(string entityType, Guid entityId, DateTime rangeStartUtc);
    }

    /// <summary>
    /// Implementation of idempotency key generator
    /// </summary>
    public sealed class IdempotencyKeyGenerator : IIdempotencyKeyGenerator
    {
        /// <summary>
        /// Generates an idempotency key using SHA-256
        /// Format: SHA-256(deviceId + "|" + rangeStartUtc + "|" + entityType)
        /// </summary>
        public string Generate(string entityType, Guid entityId, DateTime rangeStartUtc)
        {
            var deviceId = DeviceId.Current.Value;
            var input = $"{deviceId}|{rangeStartUtc:O}|{entityType}";

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
