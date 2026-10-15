using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clinic.Models;

namespace Clinic.Core
{
    public static class Store
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        public static bool Demo => Cfg("app.app.runmode") != "1";

        public static string CleanId(string? value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(v, "^[a-f0-9]{32}$") ? v : string.Empty;
        }

        public static string SandboxDir(string sbx)
        {
            return Path.Combine(Root, "temp", "sandbox", sbx);
        }

        public static string Sandbox(string? value)
        {
            string id = CleanId(value);
            return id != string.Empty && Demo && File.Exists(Path.Combine(SandboxDir(id), "site.json")) && Made(id) > DateTime.UtcNow.AddMinutes(-60) ? id : string.Empty;
        }

        public static DateTime Made(string sbx)
        {
            string file = Path.Combine(SandboxDir(sbx), "made");
            return File.Exists(file) && long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? new DateTime(t, DateTimeKind.Utc) : Directory.GetCreationTimeUtc(SandboxDir(sbx));
        }

        public static int MinutesLeft(string sbx)
        {
            return Math.Max(1, (int)Math.Ceiling((Made(sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes));
        }

        private static string FileOf(string name, string sbx)
        {
            return Path.Combine(sbx == string.Empty ? DataDir : SandboxDir(sbx), name + ".json");
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release(Gate);
        }

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? gate;

            public Release(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                gate?.Release();
                gate = null;
            }
        }

        private static async Task<T> Read<T>(string name, string sbx) where T : new()
        {
            string file = FileOf(name, sbx);
            if (!File.Exists(file))
            {
                file = FileOf(name, string.Empty);
            }
            if (!File.Exists(file))
            {
                return new T();
            }
            try
            {
                return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file), ReadOpts) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        private static async Task Write<T>(string name, string sbx, T data)
        {
            string file = FileOf(name, sbx);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            string tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(data, WriteOpts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite(string sbx = "")
        {
            SiteData site = await Read<SiteData>("site", sbx);
            Fill(site);
            if (Demo)
            {
                if (sbx == string.Empty)
                {
                    Shift(site, Days(site));
                }
                Simulate(site);
            }
            return site;
        }

        private static void Fill(SiteData site)
        {
            for (int d = 0; d < 7; d++)
            {
                if (!site.Settings.Hours.Any(h => h.Day == d))
                {
                    site.Settings.Hours.Add(new OpenDay { Day = d, Open = d is >= 1 and <= 5 });
                }
            }
            site.Settings.Hours = site.Settings.Hours.OrderBy(h => h.Day).ToList();
            foreach (Clinician p in site.Providers)
            {
                for (int d = 0; d < 7; d++)
                {
                    if (!p.Hours.Any(h => h.Day == d))
                    {
                        p.Hours.Add(new WorkDay { Day = d });
                    }
                }
                p.Hours = p.Hours.OrderBy(h => h.Day).ToList();
            }
        }

        private static TimeSpan Days(SiteData site)
        {
            if (site.Anchor == default)
            {
                return TimeSpan.Zero;
            }
            double days = Math.Floor((Clock.Today(site) - site.Anchor.Date).TotalDays);
            return TimeSpan.FromDays(Math.Floor(days / 7) * 7);
        }

        private static void Shift(SiteData site, TimeSpan d)
        {
            if (site.Anchor == default || d == TimeSpan.Zero)
            {
                return;
            }
            foreach (Appointment a in site.Appointments)
            {
                a.Start = a.Start.Add(d);
                a.Created = a.Created.Add(d);
                foreach (Hist h in a.History)
                {
                    h.When = h.When.Add(d);
                }
            }
            foreach (Patient p in site.Patients)
            {
                p.Created = p.Created.Add(d);
            }
            foreach (Clinician p in site.Providers)
            {
                foreach (TimeOff t in p.TimeOff)
                {
                    t.From = t.From.Add(d);
                    t.To = t.To.Add(d);
                }
            }
            foreach (Holiday h in site.Holidays)
            {
                h.Date = h.Date.Add(d);
            }
            foreach (Message m in site.Messages)
            {
                m.When = m.When.Add(d);
            }
            site.Anchor = site.Anchor.Add(d);
        }

        private static int Hash(string s)
        {
            int h = 17;
            foreach (char c in s)
            {
                h = unchecked(h * 31 + c);
            }
            return Math.Abs(h % 1000);
        }

        private static void Simulate(SiteData site)
        {
            DateTime now = Clock.Now(site);
            DateTime today = now.Date;
            foreach (Appointment a in site.Appointments.Where(x => x.Seed))
            {
                if (a.Status is not ("booked" or "confirmed" or "arrived" or "inroom"))
                {
                    continue;
                }
                DateTime end = a.Start.AddMinutes(a.Minutes);
                int h = Hash(a.Id);
                if (end <= now)
                {
                    a.Status = h % 23 == 0 ? "noshow" : "done";
                }
                else if (a.Start <= now)
                {
                    a.Status = "inroom";
                }
                else if (a.Start <= now.AddMinutes(20))
                {
                    a.Status = h % 3 == 0 ? "confirmed" : "arrived";
                }
                else if (a.Start.Date == today || (a.Start.Date == today.AddDays(1) && h % 2 == 0))
                {
                    a.Status = "confirmed";
                }
            }
        }

        public static Task SaveSite(SiteData site, string sbx)
        {
            return Write("site", sbx, site);
        }

        public static Task<List<Staff>> LoadStaff(string sbx = "")
        {
            return Read<List<Staff>>("staff", sbx);
        }

        public static Task SaveStaff(List<Staff> staff, string sbx)
        {
            return Write("staff", sbx, staff);
        }

        public static Task<List<Activity>> LoadActivity(string sbx = "")
        {
            return Read<List<Activity>>("activity", sbx);
        }

        public static async Task Log(string sbx, string user, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                List<Activity> list = await Read<List<Activity>>("activity", sbx);
                list.Insert(0, new Activity { When = DateTime.UtcNow, User = user, Action = action, Target = target.Length > 120 ? target.Substring(0, 120) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write("activity", sbx, list);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static async Task<string> NewSandbox()
        {
            Sweep();
            string id = Guid.NewGuid().ToString("N");
            string dir = SandboxDir(id);
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "made"), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SiteData site = await Read<SiteData>("site", string.Empty);
            Fill(site);
            TimeSpan d = Days(site);
            Shift(site, d);
            await Write("site", id, site);
            List<Activity> acts = await Read<List<Activity>>("activity", string.Empty);
            foreach (Activity a in acts)
            {
                a.When = a.When.Add(d);
            }
            await Write("activity", id, acts);
            File.Copy(FileOf("staff", string.Empty), FileOf("staff", id), true);
            return id;
        }

        public static void Sweep()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-60);
            string boxes = Path.Combine(Root, "temp", "sandbox");
            try
            {
                if (Directory.Exists(boxes))
                {
                    foreach (string dir in Directory.GetDirectories(boxes))
                    {
                        if (Made(Path.GetFileName(dir)) < limit)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
