using System;
using System.Security.Cryptography;
using System.Text;

namespace Anorath.Infrastructure.Services
{
    // PBKDF2 + random salt per password.
    // Stored format: PBKDF2$<iterations>$<saltBase64>$<hashBase64>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;       // 128-bit salt
        private const int KeySize = 32;        // 256-bit hash
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash)) return false;

            var parts = storedHash.Split('$');

            // New salted format
            if (parts.Length == 4 && parts[0] == "PBKDF2")
            {
                var iterations = int.Parse(parts[1]);
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);

                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

                // Constant-time compare (prevents timing attacks)
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            // Old unsalted SHA256 format (so existing tenant users can still log in)
            var legacy = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(legacy), Encoding.UTF8.GetBytes(storedHash));
        }
    }
}