namespace TimeTrack.Update.Services;

/// <summary>
/// Verifies Authenticode signatures and file checksums
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Verify the SHA256 checksum of a file
    /// </summary>
    Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the Authenticode signature of a file
    /// </summary>
    SignatureVerificationResult VerifySignature(string filePath);

    /// <summary>
    /// Calculate the SHA256 hash of a file
    /// </summary>
    Task<string> CalculateSha256Async(string filePath, CancellationToken cancellationToken = default);
}
