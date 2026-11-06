using System.Security.Cryptography;
using PlantPulse.Application.Common;

namespace PlantPulse.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA256 con sal aleatoria (solo BCL, sin paquetes). Formato: "PBKDF2$SHA256$iteraciones$sal$hash" en Base64.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iteraciones = 210_000;   // recomendación OWASP para PBKDF2-SHA256
    private const int BytesSal = 16;
    private const int BytesHash = 32;
    private const string Prefijo = "PBKDF2";
    private const string Algoritmo = "SHA256";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Prefijo}${Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;

        var partes = hash.Split('$');
        if (partes.Length != 5 || partes[0] != Prefijo || partes[1] != Algoritmo) return false;
        if (!int.TryParse(partes[2], out var iteraciones) || iteraciones <= 0) return false;

        try
        {
            var sal = Convert.FromBase64String(partes[3]);
            var esperado = Convert.FromBase64String(partes[4]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
