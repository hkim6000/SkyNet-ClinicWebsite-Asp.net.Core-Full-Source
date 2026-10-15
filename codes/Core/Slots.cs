using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Clinic.Models;

namespace Clinic.Core
{
    public static class Clock
    {
        private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new ConcurrentDictionary<string, TimeZoneInfo>();

        public static TimeZoneInfo Zone(string id)
        {
            return Zones.GetOrAdd(id ?? string.Empty, k =>
            {
                try
                {
                    return k == string.Empty ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(k);
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Utc;
                }
                catch (InvalidTimeZoneException)
                {
                    return TimeZoneInfo.Utc;
                }
            });
        }

        public static bool Known(string id)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        public static DateTime Now(SiteData s)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone(s.Settings.TimeZone)), DateTimeKind.Unspecified);
        }

        public static DateTime Local(SiteData s, DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(s.Settings.TimeZone));
        }

        public static DateTime Today(SiteData s)
        {
            return Now(s).Date;
        }

        public static TimeSpan? Hm(string? v)
        {
            return TimeSpan.TryParseExact((v ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan t) && t >= TimeSpan.Zero && t <= TimeSpan.FromHours(24) ? t : null;
        }

        public static string Text(TimeSpan t)
        {
            return t.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        }
    }

    public sealed class Engine
    {
        private readonly SiteData s;
        private readonly ILookup<DateTime, Appointment> byDay;
        private readonly DateTime now;

        public Engine(SiteData site)
        {
            s = site;
            byDay = site.Appointments.Where(Blocks).ToLookup(a => a.Start.Date);
            now = Clock.Now(site);
        }

        public DateTime Now => now;

        public DateTime Today => now.Date;

        public static bool Blocks(Appointment a)
        {
            return a.Status is not ("cancelled" or "noshow");
        }

        public int Step => Math.Clamp(s.Settings.SlotMinutes, 5, 60);

        public Holiday? HolidayOn(DateTime day)
        {
            return s.Holidays.FirstOrDefault(h => h.Date.Date == day.Date);
        }

        public (TimeSpan From, TimeSpan To)? Clinic(DateTime day)
        {
            if (HolidayOn(day) != null)
            {
                return null;
            }
            OpenDay? o = s.Settings.Hours.FirstOrDefault(h => h.Day == (int)day.DayOfWeek);
            TimeSpan? f = Clock.Hm(o?.From);
            TimeSpan? t = Clock.Hm(o?.To);
            if (o == null || !o.Open || f == null || t == null || t <= f)
            {
                return null;
            }
            return (f.Value, t.Value);
        }

        public bool Off(Clinician p, DateTime day)
        {
            return p.TimeOff.Any(t => day.Date >= t.From.Date && day.Date <= t.To.Date);
        }

        public List<(TimeSpan From, TimeSpan To)> Windows(Clinician p, DateTime day)
        {
            List<(TimeSpan, TimeSpan)> list = new List<(TimeSpan, TimeSpan)>();
            (TimeSpan From, TimeSpan To)? c = Clinic(day);
            WorkDay? w = p.Hours.FirstOrDefault(h => h.Day == (int)day.DayOfWeek);
            if (c == null || w == null || !w.On || !p.Active || Off(p, day))
            {
                return list;
            }
            TimeSpan? wf = Clock.Hm(w.From);
            TimeSpan? wt = Clock.Hm(w.To);
            if (wf == null || wt == null)
            {
                return list;
            }
            TimeSpan from = wf.Value > c.Value.From ? wf.Value : c.Value.From;
            TimeSpan to = wt.Value < c.Value.To ? wt.Value : c.Value.To;
            if (to <= from)
            {
                return list;
            }
            TimeSpan? bf = Clock.Hm(w.BreakFrom);
            TimeSpan? bt = Clock.Hm(w.BreakTo);
            if (bf != null && bt != null && bt > bf && bf < to && bt > from)
            {
                if (bf.Value > from)
                {
                    list.Add((from, bf.Value));
                }
                if (bt.Value < to)
                {
                    list.Add((bt.Value, to));
                }
                return list;
            }
            list.Add((from, to));
            return list;
        }

        public bool Busy(string provider, string room, DateTime start, int minutes, string ignore)
        {
            DateTime end = start.AddMinutes(minutes);
            foreach (Appointment a in byDay[start.Date])
            {
                if (a.Id == ignore)
                {
                    continue;
                }
                if (a.Provider != provider && (room == string.Empty || a.Room != room))
                {
                    continue;
                }
                if (a.Start < end && a.Start.AddMinutes(a.Minutes) > start)
                {
                    return true;
                }
            }
            return false;
        }

        public bool Online(Clinician p, CareService sv)
        {
            return p.Active && p.Accepting && sv.Active && sv.Online && sv.Providers.Contains(p.Id);
        }

        public List<Clinician> For(CareService sv, bool web)
        {
            return s.Providers.Where(p => web ? Online(p, sv) : p.Active && sv.Providers.Contains(p.Id)).OrderBy(p => p.Order).ToList();
        }

        public DateTime Earliest(bool web)
        {
            return web ? now.AddHours(Math.Max(0, s.Settings.MinNoticeHours)) : now.Date;
        }

        public DateTime Last(bool web)
        {
            return web ? Today.AddDays(Math.Clamp(s.Settings.BookAheadDays, 1, 365)) : Today.AddDays(400);
        }

        public List<DateTime> Free(Clinician p, int minutes, DateTime day, bool web, string ignore = "")
        {
            List<DateTime> list = new List<DateTime>();
            if (day.Date > Last(web) || day.Date < Today)
            {
                return list;
            }
            DateTime earliest = web ? Earliest(true) : now;
            int step = Step;
            foreach ((TimeSpan from, TimeSpan to) in Windows(p, day))
            {
                int m0 = (int)Math.Ceiling(from.TotalMinutes / step) * step;
                for (int m = m0; m + minutes <= to.TotalMinutes; m += step)
                {
                    DateTime start = day.Date.AddMinutes(m);
                    if (start < earliest || Busy(p.Id, p.Room, start, minutes, ignore))
                    {
                        continue;
                    }
                    list.Add(start);
                }
            }
            return list;
        }

        public int Load(Clinician p, DateTime day)
        {
            return byDay[day.Date].Count(a => a.Provider == p.Id);
        }

        public List<(DateTime Start, Clinician P)> Slots(CareService sv, Clinician? only, DateTime day, bool web, string ignore = "")
        {
            Dictionary<DateTime, Clinician> pick = new Dictionary<DateTime, Clinician>();
            List<Clinician> ps = only != null ? new List<Clinician> { only } : For(sv, web);
            foreach (Clinician p in ps.OrderBy(x => Load(x, day)).ThenBy(x => x.Order))
            {
                foreach (DateTime t in Free(p, sv.Minutes, day, web, ignore))
                {
                    if (!pick.ContainsKey(t))
                    {
                        pick[t] = p;
                    }
                }
            }
            return pick.OrderBy(k => k.Key).Select(k => (k.Key, k.Value)).ToList();
        }

        public (DateTime Start, Clinician P)? Next(CareService sv, Clinician? only, int days = 28)
        {
            for (int i = 0; i < days; i++)
            {
                DateTime d = Today.AddDays(i);
                if (d > Last(true))
                {
                    break;
                }
                List<(DateTime, Clinician)> l = Slots(sv, only, d, true);
                if (l.Count > 0)
                {
                    return l[0];
                }
            }
            return null;
        }

        public bool Fits(Clinician p, CareService sv, DateTime start, int minutes, bool web, string ignore)
        {
            if (start.Date > Last(web) || start < Earliest(web))
            {
                return false;
            }
            if (web && !Online(p, sv))
            {
                return false;
            }
            double m = start.TimeOfDay.TotalMinutes;
            bool inside = Windows(p, start.Date).Any(w => m >= w.From.TotalMinutes && m + minutes <= w.To.TotalMinutes);
            return inside && !Busy(p.Id, p.Room, start, minutes, ignore);
        }
    }

    public static class Codes
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Prefix(SiteData s)
        {
            string p = string.Concat(s.Settings.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).Select(w => char.ToUpperInvariant(w[0])));
            return p.Length >= 2 ? p.Substring(0, Math.Min(3, p.Length)) : "APT";
        }

        public static string New(SiteData s)
        {
            string pre = Prefix(s);
            while (true)
            {
                char[] c = new char[6];
                for (int i = 0; i < 6; i++)
                {
                    c[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
                }
                string code = pre + "-" + new string(c);
                if (!s.Appointments.Any(a => a.Code == code))
                {
                    return code;
                }
            }
        }

        public static string Id(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 10);
        }
    }
}
