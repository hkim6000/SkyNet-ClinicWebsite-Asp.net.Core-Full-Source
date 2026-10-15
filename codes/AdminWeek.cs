using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminWeek : WebPage
    {
        private const string AreaKey = "schedule";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Week view");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--wk-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("wk-menu", Auth.Menu("wk", "AdminWeek", c, site));
            response.SetElementContents("wk-who", Auth.Who("wk", c));
            response.SetElementContents("wk-demo", Auth.DemoPill("wk", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminWeekJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"wk-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"wk-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminWeekJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminWeekJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("wk-main", NoAccess("Week view"));
                return response;
            }
            response.SetElementContents("wk-main", await MainHtml(c, site));
            response.ExecuteScript("AdminWeekJs.loaded();");
            return response;
        }

        private static string Per(string v)
        {
            return v is "today" or "7" or "30" or "range" ? v : "all";
        }

        private static string Who(Dictionary<string, Staff> staff, string id)
        {
            return id == "web" ? "Website" : staff.TryGetValue(id, out Staff? s) ? s.Name : id;
        }

        private static string Head((string Name, string Cls)[] cols)
        {
            StringBuilder sb = new StringBuilder("<div class=\"wk-tr wk-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Engine e = new Engine(site);
            string own = Auth.Mine(c);
            List<Clinician> ps = site.Providers.Where(p => p.Active && (own == string.Empty || p.Id == own)).OrderBy(p => p.Order).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"wk-ptop\"><div><h1>Week view</h1><p>" + (own == string.Empty ? "See the whole team at a glance, or pick one " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) + " to see their week." : "Your week at a glance. Click a day to open it.") + "</p></div><div class=\"wk-acts\"><button type=\"button\" class=\"wk-btn wk-btn-ghost\" onclick=\"window.print()\">Print</button></div></div>");
            sb.Append("<div class=\"wk-bar\"><div class=\"wk-dnav\"><button type=\"button\" class=\"wk-btn wk-btn-ghost wk-btn-sm wk-arrow\" aria-label=\"Previous week\" onclick=\"AdminWeekJs.shift(-1)\">&lsaquo;</button><button type=\"button\" class=\"wk-btn wk-btn-ghost wk-btn-sm\" onclick=\"AdminWeekJs.thisWeek()\">This week</button><button type=\"button\" class=\"wk-btn wk-btn-ghost wk-btn-sm wk-arrow\" aria-label=\"Next week\" onclick=\"AdminWeekJs.shift(1)\">&rsaquo;</button><input type=\"date\" id=\"wk-w\" value=\"" + Ui.Ymd(e.Today) + "\" onchange=\"AdminWeekJs.list()\" aria-label=\"Week of\"></div><label>" + Ui.Enc(site.Settings.ProviderWord) + "<select id=\"wk-wp\" onchange=\"AdminWeekJs.list()\">" + (own == string.Empty ? "<option value=\"\">Everyone (summary)</option>" : string.Empty) + Ui.Opts(ps.Select(p => (p.Id, p.Name)), own) + "</select></label></div><div id=\"wk-wsum\" class=\"wk-daysum\"></div><div id=\"wk-wbody\"><p class=\"wk-loading\">Loading&hellip;</p></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Week()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminWeekJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            DateTime w = Ui.ParseDay(GetDataValue("w")) ?? e.Today;
            DateTime mon = w.AddDays(-(((int)w.DayOfWeek + 6) % 7));
            string own = Auth.Mine(c);
            string pid = own != string.Empty ? own : Ui.Clip(GetDataValue("p"), 40);
            Clinician? one = site.Providers.FirstOrDefault(p => p.Id == pid);
            Dictionary<string, Patient> pm = Ui.PatientMap(site);
            List<Appointment> week = site.Appointments.Where(a => a.Start.Date >= mon && a.Start.Date < mon.AddDays(7) && a.Status != "cancelled").ToList();
            List<DateTime> days = Enumerable.Range(0, 7).Select(i => mon.AddDays(i)).Where(d => e.Clinic(d) != null || week.Any(a => a.Start.Date == d && (one == null || a.Provider == one.Id))).ToList();
            StringBuilder sum = new StringBuilder("<span class=\"wk-dayh\">" + mon.ToString("d MMM", Ui.Inv) + " &ndash; " + mon.AddDays(6).ToString("d MMM yyyy", Ui.Inv) + "</span>");
            List<Appointment> mine = week.Where(a => one == null || a.Provider == one.Id).ToList();
            sum.Append("<span class=\"wk-st wk-st-booked\">" + mine.Count(a => a.Status != "noshow") + " appointments</span>");
            foreach (Holiday h in site.Holidays.Where(h => h.Date.Date >= mon && h.Date.Date < mon.AddDays(7)))
            {
                sum.Append("<span class=\"wk-st wk-st-noshow\">Closed " + h.Date.ToString("ddd d", Ui.Inv) + " &middot; " + Ui.Enc(h.Name) + "</span>");
            }
            StringBuilder sb = new StringBuilder();
            if (one == null)
            {
                sb.Append("<div class=\"wk-mx\"><div class=\"wk-mxt\" style=\"--n:" + days.Count + "\"><div class=\"wk-mh\">" + Ui.Enc(site.Settings.ProviderWord) + "</div>");
                foreach (DateTime d in days)
                {
                    sb.Append("<div class=\"wk-mh" + (d == e.Today ? " wk-tday" : string.Empty) + "\">" + d.ToString("ddd d", Ui.Inv) + "</div>");
                }
                List<Specialty> specs = site.Specialties.OrderBy(x => x.Order).ToList();
                foreach (Clinician p in site.Providers.Where(p => p.Active).OrderBy(p => specs.FindIndex(s => s.Key == p.Specialty)).ThenBy(p => p.Order))
                {
                    sb.Append("<div class=\"wk-mp\">" + Ui.Face("wk", p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + "</small></span></div>");
                    foreach (DateTime d in days)
                    {
                        List<(TimeSpan From, TimeSpan To)> win = e.Windows(p, d);
                        List<Appointment> list = week.Where(a => a.Provider == p.Id && a.Start.Date == d && a.Status != "noshow").ToList();
                        double av = win.Sum(x => (x.To - x.From).TotalMinutes);
                        int pc = av > 0 ? (int)Math.Round(Math.Min(100, list.Sum(a => (double)a.Minutes) * 100 / av)) : 0;
                        if (win.Count == 0 && list.Count == 0)
                        {
                            sb.Append("<div class=\"wk-mc wk-moff\"><small>" + (e.Off(p, d) ? "Away" : "&mdash;") + "</small></div>");
                            continue;
                        }
                        sb.Append("<a class=\"wk-mc\" href=\"AdminSchedule?d=" + Ui.Ymd(d) + "\"><b>" + list.Count + "</b><small>" + pc + "% booked</small><span class=\"wk-mbar\"><i" + (pc >= 90 ? " class=\"wk-full\"" : string.Empty) + " style=\"width:" + pc + "%\"></i></span></a>");
                    }
                }
                sb.Append("<div class=\"wk-mp wk-mt\">All</div>");
                foreach (DateTime d in days)
                {
                    sb.Append("<a class=\"wk-mc wk-mt\" href=\"AdminSchedule?d=" + Ui.Ymd(d) + "\"><b>" + week.Count(a => a.Start.Date == d && a.Status != "noshow") + "</b><small>appointments</small></a>");
                }
                sb.Append("</div></div>");
                response.SetElementContents("wk-wbody", sb.ToString());
            }
            else
            {
                int start = 24 * 60;
                int end = 0;
                foreach (DateTime d in days)
                {
                    foreach ((TimeSpan f, TimeSpan t) in e.Windows(one, d))
                    {
                        start = Math.Min(start, (int)f.TotalMinutes);
                        end = Math.Max(end, (int)t.TotalMinutes);
                    }
                }
                foreach (Appointment a in mine)
                {
                    start = Math.Min(start, (int)a.Start.TimeOfDay.TotalMinutes);
                    end = Math.Max(end, (int)a.Start.TimeOfDay.TotalMinutes + a.Minutes);
                }
                if (end <= start)
                {
                    start = 480;
                    end = 1080;
                }
                start = start / 60 * 60;
                end = Math.Min(1440, (end + 59) / 60 * 60);
                bool nowOn = days.Contains(e.Today) && e.Now.TimeOfDay.TotalMinutes >= start && e.Now.TimeOfDay.TotalMinutes <= end;
                string nowTop = ((e.Now.TimeOfDay.TotalMinutes - start) * 1.6).ToString("0", Ui.Inv);
                sb.Append("<div class=\"wk-gridw\" id=\"wk-gridw\"><div class=\"wk-sch\" style=\"--n:" + days.Count + ";--h:" + ((end - start) * 1.6).ToString("0", Ui.Inv) + "px\"><div class=\"wk-schh\"><div></div>");
                foreach (DateTime d in days)
                {
                    int n = mine.Count(a => a.Start.Date == d && a.Status != "noshow");
                    sb.Append("<div class=\"wk-ch" + (d == e.Today ? " wk-tday" : string.Empty) + "\"><a href=\"AdminSchedule?d=" + Ui.Ymd(d) + "\"><span><b>" + d.ToString("ddd d MMM", Ui.Inv) + "</b><small>" + (e.Windows(one, d).Count > 0 ? n + " booked" : e.Off(one, d) ? "Away" : "Not working") + "</small></span></a></div>");
                }
                sb.Append("</div><div class=\"wk-tcol\">" + HourLabels(site, start, end) + (nowOn ? "<em class=\"wk-nowt\" style=\"top:" + nowTop + "px\">" + Ui.Hm(site, e.Now) + "</em>" : string.Empty) + "</div>");
                foreach (DateTime d in days)
                {
                    List<(TimeSpan From, TimeSpan To)> win = e.Windows(one, d);
                    sb.Append("<div class=\"wk-col wk-can\" data-d=\"" + Ui.Ymd(d) + "\" onclick=\"AdminWeekJs.slot(event,this)\">" + (win.Count == 0 ? "<i class=\"wk-off wk-away\" style=\"top:0;height:100%\"><span>" + (e.Off(one, d) ? "Away" : "Not working") + "</span></i>" : Shade(win, start, end)) + Blocks(site, mine.Where(a => a.Start.Date == d).ToList(), start, pm, false) + (nowOn && d == e.Today ? "<i class=\"wk-nowl\" style=\"top:" + nowTop + "px\"></i>" : string.Empty) + "</div>");
                }
                sb.Append("</div></div>" + Legend);
                response.SetElementContents("wk-wbody", sb.ToString());
            }
            response.SetElementContents("wk-wsum", sum.ToString());
            response.ExecuteScript("AdminWeekJs.drawn('" + Ui.Ymd(mon) + "');");
            return response;
        }

        private static string Blocks(SiteData site, List<Appointment> list, int start, Dictionary<string, Patient> pm, bool showProv)
        {
            StringBuilder sb = new StringBuilder();
            List<Appointment> sorted = list.OrderBy(a => a.Start).ThenByDescending(a => a.Minutes).ToList();
            int i = 0;
            while (i < sorted.Count)
            {
                List<Appointment> cluster = new List<Appointment> { sorted[i] };
                DateTime end = sorted[i].Start.AddMinutes(sorted[i].Minutes);
                int j = i + 1;
                while (j < sorted.Count && sorted[j].Start < end)
                {
                    cluster.Add(sorted[j]);
                    DateTime e2 = sorted[j].Start.AddMinutes(sorted[j].Minutes);
                    end = e2 > end ? e2 : end;
                    j++;
                }
                List<DateTime> lanes = new List<DateTime>();
                Dictionary<Appointment, int> lane = new Dictionary<Appointment, int>();
                foreach (Appointment a in cluster)
                {
                    int k = lanes.FindIndex(x => x <= a.Start);
                    if (k < 0)
                    {
                        lanes.Add(a.Start.AddMinutes(a.Minutes));
                        k = lanes.Count - 1;
                    }
                    else
                    {
                        lanes[k] = a.Start.AddMinutes(a.Minutes);
                    }
                    lane[a] = k;
                }
                double w = 100.0 / lanes.Count;
                foreach (Appointment a in cluster)
                {
                    double top = (a.Start.TimeOfDay.TotalMinutes - start) * 1.6;
                    double h = Math.Max(18, a.Minutes * 1.6 - 2);
                    Patient? pt = pm.TryGetValue(a.Patient, out Patient? x) ? x : null;
                    CareService? sv = Ui.ServiceOf(site, a.Service);
                    string who = Ui.Name(pt);
                    string title = Ui.Hm(site, a.Start) + " " + who + " · " + (sv?.Name ?? string.Empty) + " · " + Ui.StatusName(a.Status) + (showProv ? " · " + Ui.ProviderName(site, a.Provider) : string.Empty);
                    sb.Append("<button type=\"button\" class=\"wk-blk wk-b-" + a.Status + (a.Minutes <= 15 ? " wk-tiny" : string.Empty) + "\" style=\"top:" + top.ToString("0.#", Ui.Inv) + "px;height:" + h.ToString("0.#", Ui.Inv) + "px;left:" + (lane[a] * w).ToString("0.##", Ui.Inv) + "%;width:" + w.ToString("0.##", Ui.Inv) + "%;--c:" + Ui.ColorOf(site, a) + "\" title=\"" + Ui.Attr(title) + "\" onclick=\"event.stopPropagation();AdminWeekJs.openAppt('" + Ui.Js(a.Id) + "')\"><span class=\"wk-bl1\"><b>" + a.Start.ToString(site.Settings.Clock24 ? "HH:mm" : "h:mm", Ui.Inv) + "</b> " + Ui.Enc(who) + (a.NewPatient ? " <i>New</i>" : string.Empty) + "</span><small>" + Ui.Enc(sv?.Name ?? string.Empty) + (showProv ? " &middot; " + Ui.Enc(Ui.ProviderName(site, a.Provider)) : string.Empty) + "</small></button>");
                }
                i = j;
            }
            return sb.ToString();
        }

        private static string Shade(List<(TimeSpan From, TimeSpan To)> work, int start, int end)
        {
            StringBuilder sb = new StringBuilder();
            int cur = start;
            foreach ((TimeSpan f, TimeSpan t) in work.OrderBy(x => x.From))
            {
                int a = (int)f.TotalMinutes;
                int b = (int)t.TotalMinutes;
                if (a > cur)
                {
                    sb.Append("<i class=\"wk-off\" style=\"top:" + ((cur - start) * 1.6).ToString("0.#", Ui.Inv) + "px;height:" + ((a - cur) * 1.6).ToString("0.#", Ui.Inv) + "px\"></i>");
                }
                cur = Math.Max(cur, b);
            }
            if (cur < end)
            {
                sb.Append("<i class=\"wk-off\" style=\"top:" + ((cur - start) * 1.6).ToString("0.#", Ui.Inv) + "px;height:" + ((end - cur) * 1.6).ToString("0.#", Ui.Inv) + "px\"></i>");
            }
            return sb.ToString();
        }

        private static string HourLabels(SiteData site, int start, int end)
        {
            StringBuilder sb = new StringBuilder();
            for (int m = start; m < end; m += 60)
            {
                sb.Append("<span style=\"top:" + ((m - start) * 1.6).ToString("0.#", Ui.Inv) + "px\">" + Ui.Hm(site, TimeSpan.FromMinutes(m)) + "</span>");
            }
            return sb.ToString();
        }

        private const string Legend = "<div class=\"wk-legend\"><span><i style=\"background:#EAF1FA\"></i>Booked</span><span><i style=\"background:#DCF0EC\"></i>Confirmed</span><span><i style=\"background:#FCEBCB\"></i>Checked in</span><span><i style=\"background:#E6DDF5\"></i>In room</span><span><i style=\"background:#E7EFEA\"></i>Completed</span><span><i style=\"background:#F8E1DF\"></i>No-show</span><span><i style=\"background:repeating-linear-gradient(135deg,rgba(120,140,136,.25) 0 3px,transparent 3px 6px);border-left-color:transparent\"></i>Not working</span></div>";
    }
}
