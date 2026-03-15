namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Tipo de identificador de aplicativo
///
/// SRP: Define apenas o tipo de identificador (exe vs domain)
/// </summary>
public enum AppIdentifierType
{
    /// <summary>
    /// Executável de desktop (ex: whatsapp.exe, code.exe)
    /// </summary>
    Exe = 1,

    /// <summary>
    /// Domínio web (ex: youtube.com, github.com)
    /// </summary>
    Domain = 2
}
