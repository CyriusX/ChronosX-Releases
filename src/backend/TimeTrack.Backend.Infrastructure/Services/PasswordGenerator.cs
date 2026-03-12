using System.Security.Cryptography;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Services;

/// <summary>
/// Gerador de senhas temporárias seguras
/// </summary>
public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Special = "!@#$%^&*";
    private const string AllChars = Uppercase + Lowercase + Digits + Special;

    public string GenerateTemporaryPassword(int length = 12)
    {
        if (length < 8)
            length = 8;

        var password = new char[length];
        var random = RandomNumberGenerator.Create();

        // Garantir pelo menos um de cada tipo
        password[0] = GetRandomChar(Uppercase, random);
        password[1] = GetRandomChar(Lowercase, random);
        password[2] = GetRandomChar(Digits, random);
        password[3] = GetRandomChar(Special, random);

        // Preencher o resto aleatoriamente
        for (int i = 4; i < length; i++)
        {
            password[i] = GetRandomChar(AllChars, random);
        }

        // Embaralhar a senha
        Shuffle(password, random);

        return new string(password);
    }

    private static char GetRandomChar(string chars, RandomNumberGenerator random)
    {
        var bytes = new byte[1];
        random.GetBytes(bytes);
        return chars[bytes[0] % chars.Length];
    }

    private static void Shuffle(char[] array, RandomNumberGenerator random)
    {
        var bytes = new byte[array.Length * 4];
        random.GetBytes(bytes);

        for (int i = array.Length - 1; i > 0; i--)
        {
            var randomIndex = BitConverter.ToInt32(bytes, i * 4) & 0x7FFFFFFF;
            var j = randomIndex % (i + 1);

            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
