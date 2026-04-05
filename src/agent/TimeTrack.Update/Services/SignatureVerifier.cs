using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Update.Services;

/// <summary>
/// Verifies file checksums and Authenticode signatures
/// </summary>
public sealed class SignatureVerifier : ISignatureVerifier
{
    private readonly ILogger<SignatureVerifier> _logger;

    // Expected publisher certificate subject (adjust to your actual certificate)
    private const string ExpectedPublisher = "CN=Cyrius";

    public SignatureVerifier(ILogger<SignatureVerifier> logger)
    {
        _logger = logger;
    }

    public async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("File not found: {Path}", filePath);
            return false;
        }

        var actualChecksum = await CalculateSha256Async(filePath, cancellationToken);
        var isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

        if (isValid)
        {
            _logger.LogInformation("Checksum verified successfully");
        }
        else
        {
            _logger.LogError("Checksum mismatch. Expected: {Expected}, Actual: {Actual}", expectedChecksum, actualChecksum);
        }

        return isValid;
    }

    public async Task<string> CalculateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public SignatureVerificationResult VerifySignature(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "File not found"
                };
            }

            // Try to get the Authenticode signature
            var certificate = GetAuthenticodeCertificate(filePath);

            if (certificate == null)
            {
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "No Authenticode signature found"
                };
            }

            // Verify the certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            var chainValid = chain.Build(certificate);

            if (!chainValid)
            {
                var chainErrors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
                _logger.LogWarning("Certificate chain validation failed: {Errors}", chainErrors);

                // For development/testing, we might want to allow this
                // In production, this should return false
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Certificate chain validation failed: {chainErrors}",
                    Publisher = certificate.Subject
                };
            }

            // Verify the publisher
            var publisher = certificate.Subject;
            if (!publisher.Contains(ExpectedPublisher))
            {
                _logger.LogWarning("Unexpected publisher: {Publisher}", publisher);
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Unexpected publisher: {publisher}",
                    Publisher = publisher
                };
            }

            _logger.LogInformation("Signature verified. Publisher: {Publisher}", publisher);

            return new SignatureVerificationResult
            {
                IsValid = true,
                Publisher = publisher
            };
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error verifying signature");
            return new SignatureVerificationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature");
            return new SignatureVerificationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static X509Certificate2? GetAuthenticodeCertificate(string filePath)
    {
        try
        {
            // Use the native method to extract Authenticode signature
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            return new X509Certificate2(cert.Handle);
        }
        catch
        {
            return null;
        }
    }
}
