using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Models;

namespace Clinic.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "schedule", Name = "Day schedule", Page = "AdminSchedule" },
            new Area { Key = "appointments", Name = "Appointments", Page = "AdminAppointments", Extra = "Cancel" },
            new Area { Key = "patients", Name = "Patients", Page = "AdminPatients" },
            new Area { Key = "providers", Name = "Providers", Page = "AdminProviders", Extra = "Time off" },
            new Area { Key = "services", Name = "Services", Page = "AdminServices" },
            new Area { Key = "rooms", Name = "Rooms", Page = "AdminRooms" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Area { Key = "users", Name = "Users", Page = "AdminUsers", AdminOnly = true }
        };

        public static Area? AreaOf(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(Staff u, string area)
        {
            if (u.Admin)
            {
                return "VEX";
            }
            Area? a = AreaOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string p = u.Perms.TryGetValue(area, out string? v) ? v ?? string.Empty : string.Empty;
            string r = string.Empty;
            if (p.Contains('V') || p.Contains('E') || p.Contains('X'))
            {
                r += "V";
            }
            if (p.Contains('E') && !a.NoEdit)
            {
                r += "E";
            }
            if (p.Contains('X') && p.Contains('E') && a.Extra != string.Empty)
            {
                r += "X";
            }
            return r;
        }

        public static bool Can(Staff u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static string Clean(string area, string value)
        {
            Area? a = AreaOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string v = value ?? string.Empty;
            string r = string.Empty;
            if (v.Contains('V') || v.Contains('E'))
            {
                r = "V";
            }
            if (v.Contains('E') && !a.NoEdit)
            {
                r += "E";
                if (v.Contains('X') && a.Extra != string.Empty)
                {
                    r += "X";
                }
            }
            return r;
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool VerifySha(string password, string hex)
        {
            string hash = (hex ?? string.Empty).ToLowerInvariant();
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
            {
                return false;
            }
            string given = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(hash));
        }

        public static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..");
        }
    }
}
