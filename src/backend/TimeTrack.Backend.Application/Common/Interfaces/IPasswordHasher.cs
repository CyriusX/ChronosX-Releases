namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para hash de senhas
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);
}
