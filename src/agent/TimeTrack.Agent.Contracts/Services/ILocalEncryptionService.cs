namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Interface para criptografia local de arquivos de evidência
/// Usa AES-256-GCM com chave derivada via DPAPI (Windows)
/// </summary>
public interface ILocalEncryptionService
{
    byte[] Encrypt(byte[] plaintext, out byte[] iv);
    byte[] Decrypt(byte[] ciphertext, byte[] iv);
}
