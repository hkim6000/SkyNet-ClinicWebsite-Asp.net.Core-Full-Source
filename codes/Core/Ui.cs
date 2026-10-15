using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Models;

namespace Clinic.Core
{
    public static class Ui
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Enc(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "&quot;").Replace("<", "\\x3c").Replace("\n", " ").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string Iso(DateTime d)
        {
            return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.UtcNow - d.ToUniversalTime();
            if (t.TotalMinutes < 1)
            {
                return "Just now";
            }
            if (t.TotalMinutes < 60)
            {
                int m = (int)t.TotalMinutes;
                return m + (m == 1 ? " min ago" : " mins ago");
            }
            if (t.TotalHours < 24)
            {
                int h = (int)t.TotalHours;
                return h + (h == 1 ? " hour ago" : " hours ago");
            }
            return d.ToUniversalTime().ToString("d MMM yyyy", Inv);
        }

        public static string Time(DateTime d, string mode = "ago")
        {
            DateTime u = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return "<time datetime=\"" + Iso(u) + "\" data-t=\"" + mode + "\">" + (mode == "ago" ? Ago(u) : u.ToString("d MMMM yyyy, HH:mm 'UTC'", Inv)) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = Regex.Replace(name ?? string.Empty, "^(Dr\\.?|Mr\\.?|Ms\\.?|Mrs\\.?)\\s+", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(Inv);
        }

        public static string Avatar(string p, Staff? u, string size = "")
        {
            string name = u?.Name ?? "Staff";
            return "<span class=\"" + p + "-av" + (size != string.Empty ? " " + p + "-av-" + size : string.Empty) + "\" style=\"--h:" + Hue(u?.Id ?? name) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }

        public static string Face(string p, Clinician? v, string cls = "")
        {
            string c = p + "-face" + (cls != string.Empty ? " " + cls : string.Empty);
            if (v != null && v.Photo != string.Empty)
            {
                return "<img class=\"" + c + "\" src=\"" + Attr(v.Photo) + "\" alt=\"\" loading=\"lazy\" width=\"480\" height=\"600\">";
            }
            return "<span class=\"" + c + " " + p + "-noface\" style=\"--h:" + Hue(v?.Id ?? "x") + "\" aria-hidden=\"true\">" + Enc(Initials(v?.Name ?? "?")) + "</span>";
        }

        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            string count = total + " " + (total == 1 ? one : many);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + count + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static Dictionary<string, Staff> StaffMap(List<Staff> list)
        {
            Dictionary<string, Staff> d = new Dictionary<string, Staff>();
            foreach (Staff s in list)
            {
                d[s.Id] = s;
            }
            return d;
        }

        public static string Money(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Inv);
        }

        public static string Num(int v)
        {
            return v.ToString("#,##0", Inv);
        }

        public static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd d MMM yyyy" : "ddd d MMM", Inv);
        }

        public static string LongDay(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", Inv);
        }

        public static string Ymd(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", Inv);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified) : null;
        }

        public static DateTime? ParseStart(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd'T'HH:mm", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified) : null;
        }

        public static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Inv);
        }

        public static string Hm(SiteData s, DateTime d)
        {
            return d.ToString(s.Settings.Clock24 ? "HH:mm" : "h:mm tt", Inv);
        }

        public static string Hm(SiteData s, TimeSpan t)
        {
            return Hm(s, DateTime.Today.Add(t));
        }

        public static string HmText(SiteData s, string hm)
        {
            TimeSpan? t = Clock.Hm(hm);
            return t == null ? string.Empty : Hm(s, t.Value);
        }

        public static string When(SiteData s, DateTime d, bool year = false)
        {
            return Day(d, year) + " &middot; " + Hm(s, d);
        }

        public static string Dur(int minutes)
        {
            if (minutes < 60)
            {
                return minutes + " min";
            }
            int h = minutes / 60;
            int m = minutes % 60;
            return h + " h" + (m > 0 ? " " + m + " min" : string.Empty);
        }

        public static string Thumb(string full)
        {
            return full.Contains("/full/", StringComparison.Ordinal) ? full.Replace("/full/", "/thumbs/") : full;
        }

        public static string Pic(string src, string alt, string cls, bool thumb = false, bool eager = false)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            return "<img class=\"" + cls + "\" src=\"" + Attr(thumb ? Thumb(src) : src) + "\" alt=\"" + Attr(alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " width=\"" + (thumb ? "640" : "1600") + "\" height=\"" + (thumb ? "427" : "1067") + "\">";
        }

        public static string CleanPhone(string? v)
        {
            return Regex.Replace(Clip(v, 30), "[^0-9+()\\-. ]", string.Empty).Trim();
        }

        public static string Digits(string? v)
        {
            return Regex.Replace(v ?? string.Empty, "[^0-9]", string.Empty);
        }

        public static bool Email(string v)
        {
            return Regex.IsMatch(v, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
        }

        public static string Opts(IEnumerable<(string Value, string Text)> items, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string v, string t) in items)
            {
                sb.Append("<option value=\"" + Attr(v) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        public static string DataList(string id, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder("<datalist id=\"" + id + "\">");
            foreach (string v in values.Where(x => x != string.Empty).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("<option value=\"" + Attr(v) + "\"></option>");
            }
            sb.Append("</datalist>");
            return sb.ToString();
        }

        public static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static readonly (string Key, string Name)[] Statuses =
        {
            ("booked", "Booked"),
            ("confirmed", "Confirmed"),
            ("arrived", "Checked in"),
            ("inroom", "In room"),
            ("done", "Completed"),
            ("noshow", "No-show"),
            ("cancelled", "Cancelled")
        };

        public static string StatusName(string st)
        {
            foreach ((string k, string n) in Statuses)
            {
                if (k == st)
                {
                    return n;
                }
            }
            return st;
        }

        public static string StatusPill(string p, Appointment a)
        {
            return "<span class=\"" + p + "-st " + p + "-st-" + a.Status + "\">" + StatusName(a.Status) + "</span>";
        }

        public static Dictionary<string, Patient> PatientMap(SiteData s)
        {
            Dictionary<string, Patient> d = new Dictionary<string, Patient>();
            foreach (Patient x in s.Patients)
            {
                d[x.Id] = x;
            }
            return d;
        }

        public static string Name(Patient? x)
        {
            return x == null ? "Unknown" : (x.First + " " + x.Last).Trim();
        }

        public static CareService? ServiceOf(SiteData s, string key)
        {
            return s.Services.FirstOrDefault(x => x.Key == key);
        }

        public static Clinician? ProviderOf(SiteData s, string id)
        {
            return s.Providers.FirstOrDefault(x => x.Id == id);
        }

        public static Specialty? SpecialtyOf(SiteData s, string key)
        {
            return s.Specialties.FirstOrDefault(x => x.Key == key);
        }

        public static ClinicRoom? RoomOf(SiteData s, string id)
        {
            return s.Rooms.FirstOrDefault(x => x.Id == id);
        }

        public static string ServiceName(SiteData s, string key)
        {
            return ServiceOf(s, key)?.Name ?? key;
        }

        public static string ProviderName(SiteData s, string id)
        {
            return ProviderOf(s, id)?.Name ?? "Unassigned";
        }

        public static string RoomName(SiteData s, string id)
        {
            return RoomOf(s, id)?.Name ?? string.Empty;
        }

        public static string ColorOf(SiteData s, Appointment a)
        {
            CareService? sv = ServiceOf(s, a.Service);
            Specialty? sp = sv != null ? SpecialtyOf(s, sv.Specialty) : null;
            return SafeColor(sp?.Color ?? string.Empty) ? sp!.Color : "#1F7A74";
        }

        public static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public static (DateTime? From, DateTime? To) Period(DateTime today, string period, string from, string to, bool future = false)
        {
            return period switch
            {
                "today" => (today, today),
                "7" => future ? (today, today.AddDays(6)) : (today.AddDays(-6), today),
                "30" => future ? (today, today.AddDays(29)) : (today.AddDays(-29), today),
                "range" => (ParseDay(from), ParseDay(to)),
                _ => (null, null)
            };
        }

        public static bool InPeriod(DateTime d, DateTime? from, DateTime? to)
        {
            return (from == null || d.Date >= from.Value) && (to == null || d.Date <= to.Value);
        }

        public static string PeriodBar(string p, string js, string cur, string from, string to, bool future = false)
        {
            (string k, string n)[] chips = { ("today", "Today"), ("7", future ? "Next 7 days" : "7 days"), ("30", future ? "Next 30 days" : "30 days"), ("all", "All") };
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-period\"><div class=\"" + p + "-chips\">");
            foreach ((string k, string n) in chips)
            {
                sb.Append("<button type=\"button\" class=\"" + p + "-chip" + (k == cur ? " " + p + "-act" : string.Empty) + "\" onclick=\"" + js + "('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"" + p + "-pf\"><span>From</span><input type=\"date\" id=\"" + p + "-from\" value=\"" + Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><label class=\"" + p + "-pf\"><span>To</span><input type=\"date\" id=\"" + p + "-to\" value=\"" + Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><input type=\"hidden\" id=\"" + p + "-per\" value=\"" + Attr(cur) + "\"></div>");
            return sb.ToString();
        }

        public static List<string> Images(string folder)
        {
            string dir = Path.Combine(Store.Root, "images", "clinic", folder);
            if (!Directory.Exists(dir))
            {
                return new List<string>();
            }
            return Directory.GetFiles(dir, "*.webp").Select(f => "images/clinic/" + folder + "/" + Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        public static string Picker(string p, string name, string folder, string cur, bool ro, bool none)
        {
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-imgs" + (folder == "people" ? " " + p + "-imgs-tall" : string.Empty) + "\">");
            string off = ro ? " disabled" : string.Empty;
            if (none)
            {
                sb.Append("<label class=\"" + p + "-pnone\"><input type=\"radio\" name=\"" + name + "\" value=\"\"" + (cur == string.Empty ? " checked" : string.Empty) + off + "><span>No photo</span></label>");
            }
            foreach (string f in Images(folder))
            {
                string t = folder == "full" ? Thumb(f) : f;
                string label = Regex.Replace(Path.GetFileNameWithoutExtension(f), "[-_]+", " ");
                sb.Append("<label title=\"" + Attr(label) + "\"><input type=\"radio\" name=\"" + name + "\" value=\"" + Attr(f) + "\"" + (f == cur ? " checked" : string.Empty) + off + "><img src=\"" + Attr(t) + "\" alt=\"\" loading=\"lazy\"><small>" + Enc(label) + "</small></label>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string Lines(IEnumerable<string> items)
        {
            return string.Join("\n", items);
        }

        public static List<string> SplitLines(string? v, int max, int each)
        {
            return (v ?? string.Empty).Split('\n').Select(x => Clip(x, each)).Where(x => x != string.Empty).Take(max).ToList();
        }
    }
}
