using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Admin : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--db-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "Admin", c, site));
            response.SetElementContents("db-who", Auth.Who("db", c));
            response.SetElementContents("db-demo", Auth.DemoPill("db", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"db-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"db-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("db-main", NoAccess("Dashboard"));
                return response;
            }
            response.SetElementContents("db-main", await MainHtml(c, site));
            response.ExecuteScript("AdminJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"db-tr db-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Greeting(int hour)
        {
            return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Staff u = c.U;
            Engine e = new Engine(site);
            DateTime now = e.Now;
            DateTime today = e.Today;
            string own = Auth.Mine(c);
            Dictionary<string, Patient> pm = Ui.PatientMap(site);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            bool sched = Access.Can(u, "schedule", 'V');
            bool msgs = Access.Can(u, "appointments", 'V') && own == string.Empty;
            List<Appointment> day = site.Appointments.Where(a => a.Start.Date == today && (own == string.Empty || a.Provider == own)).OrderBy(a => a.Start).ToList();
            List<Appointment> live = day.Where(a => a.Status != "cancelled").ToList();
            Holiday? hol = e.HolidayOn(today);
            StringBuilder sb = new StringBuilder("<div class=\"db-ptop\"><div><h1>" + Greeting(now.Hour) + ", " + Ui.Enc(u.Name.Replace("Dr. ", string.Empty).Split(' ')[0]) + "</h1><p>" + Ui.Enc(u.Title) + " &middot; " + Ui.LongDay(today) + (hol != null ? " &middot; closed for " + Ui.Enc(hol.Name) : string.Empty) + "</p></div>");
            if (sched)
            {
                sb.Append("<div class=\"db-acts\"><a class=\"db-btn db-btn-accent\" href=\"AdminSchedule\">" + (own != string.Empty ? "My day" : "Day schedule") + "</a><a class=\"db-btn db-btn-ghost\" href=\"Book\" target=\"_blank\" rel=\"noopener\">Booking page</a></div>");
            }
            sb.Append("</div>");
            if (sched)
            {
                int waiting = day.Count(a => a.Status == "arrived");
                int inroom = day.Count(a => a.Status == "inroom");
                int done = day.Count(a => a.Status == "done");
                int ns = day.Count(a => a.Status == "noshow");
                sb.Append("<div class=\"db-stats\"><a class=\"db-stat\" href=\"AdminSchedule\"><b>" + live.Count + "</b><span>" + (own != string.Empty ? "Your appointments today" : "Appointments today") + "</span></a><a class=\"db-stat" + (waiting > 0 ? " db-hot" : string.Empty) + "\" href=\"AdminSchedule\"><b>" + waiting + "</b><span>In the waiting room</span></a><a class=\"db-stat\" href=\"AdminSchedule\"><b>" + inroom + "</b><span>In a room now</span></a><a class=\"db-stat\" href=\"AdminAppointments\"><b>" + done + "</b><span>Completed" + (ns > 0 ? " &middot; " + ns + " no-show" + (ns == 1 ? string.Empty : "s") : string.Empty) + "</span></a></div>");
                List<Clinician> working = site.Providers.Where(p => p.Active && (own == string.Empty || p.Id == own) && e.Windows(p, today).Count > 0).ToList();
                double avail = working.Sum(p => e.Windows(p, today).Sum(w => (w.To - w.From).TotalMinutes));
                double booked = live.Where(a => a.Status != "noshow" && working.Any(p => p.Id == a.Provider)).Sum(a => (double)a.Minutes);
                int util = avail > 0 ? (int)Math.Round(Math.Min(100, booked * 100 / avail)) : 0;
                int free = working.Sum(p => e.Free(p, e.Step, today, false).Count);
                sb.Append("<div class=\"db-stats\"><div class=\"db-stat\"><b>" + util + "%</b><span>Time booked today</span><div class=\"db-bar2\"><i style=\"width:" + util + "%\"></i></div></div><a class=\"db-stat\" href=\"AdminSchedule\"><b>" + free + "</b><span>Free " + e.Step + "-min slots left today</span></a>");
                if (Access.Can(u, "appointments", 'V'))
                {
                    int web = site.Appointments.Count(a => a.Source == "web" && a.Created >= DateTime.UtcNow.AddDays(-7) && (own == string.Empty || a.Provider == own));
                    int week = site.Appointments.Count(a => a.Start.Date > today && a.Start.Date <= today.AddDays(7) && Live(a) && (own == string.Empty || a.Provider == own));
                    sb.Append("<a class=\"db-stat\" href=\"AdminAppointments\"><b>" + web + "</b><span>Booked online, last 7 days</span></a><a class=\"db-stat\" href=\"AdminWeek\"><b>" + week + "</b><span>Booked for the next 7 days</span></a>");
                }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"db-grid2\"><div>");
            if (sched)
            {
                List<Appointment> next = day.Where(a => (a.Status is "booked" or "confirmed") && a.Start >= now.AddMinutes(-20)).Take(8).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Next up</h2><a href=\"AdminSchedule\">Day schedule</a></div>");
                if (next.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">No more appointments waiting to start today.</p>");
                }
                foreach (Appointment a in next)
                {
                    Patient? pt = pm.TryGetValue(a.Patient, out Patient? x) ? x : null;
                    sb.Append("<a class=\"db-row\" href=\"AdminSchedule?a=" + Uri.EscapeDataString(a.Id) + "\"><span class=\"db-tm\">" + Ui.Hm(site, a.Start) + "</span><div><b>" + Ui.Enc(Ui.Name(pt)) + "</b><small>" + Ui.Enc(Ui.ServiceName(site, a.Service)) + " &middot; " + Ui.Enc(Ui.ProviderName(site, a.Provider)) + "</small></div>" + Ui.StatusPill("db", a) + "</a>");
                }
                sb.Append("</section>");
                List<Appointment> wait = day.Where(a => a.Status == "arrived").ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Waiting room</h2></div>");
                if (wait.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">Nobody is waiting.</p>");
                }
                foreach (Appointment a in wait)
                {
                    Patient? pt = pm.TryGetValue(a.Patient, out Patient? x) ? x : null;
                    Hist? h = a.History.LastOrDefault(y => y.Text.StartsWith("Checked in", StringComparison.Ordinal));
                    int mins = h != null && !a.Seed ? (int)Math.Max(0, (DateTime.UtcNow - h.When).TotalMinutes) : (int)Math.Max(0, (now - a.Start.AddMinutes(-10)).TotalMinutes);
                    sb.Append("<a class=\"db-row\" href=\"AdminSchedule?a=" + Uri.EscapeDataString(a.Id) + "\"><span class=\"db-tm\">" + Ui.Hm(site, a.Start) + "</span><div><b>" + Ui.Enc(Ui.Name(pt)) + "</b><small>" + Ui.Enc(Ui.ProviderName(site, a.Provider)) + "</small></div><span class=\"db-wait" + (mins > 15 ? " db-long" : string.Empty) + "\">" + mins + " min</span></a>");
                }
                sb.Append("</section>");
            }
            if (msgs)
            {
                List<Message> ms = site.Messages.OrderBy(m => m.Read).ThenByDescending(m => m.When).Take(6).ToList();
                sb.Append("<section class=\"db-panel\" id=\"msgs\"><div class=\"db-ph\"><h2>Messages</h2><span class=\"db-hint\">" + site.Messages.Count(m => !m.Read) + " unread</span></div>");
                if (ms.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">No messages from the website.</p>");
                }
                foreach (Message m in ms)
                {
                    sb.Append("<button type=\"button\" class=\"db-row db-msg" + (m.Read ? string.Empty : " db-unread") + "\" onclick=\"AdminJs.msg('" + Ui.Js(m.Id) + "')\"><div><b>" + Ui.Enc(m.Name) + " &middot; " + Ui.Enc(m.Topic) + "</b><small>" + Ui.Enc(Ui.Clip(m.Text, 90)) + " &middot; " + Ui.Time(m.When) + "</small></div></button>");
                }
                sb.Append("</section>");
            }
            sb.Append("</div><div>");
            if (sched)
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>" + Ui.Enc(site.Settings.ProvidersWord) + " today</h2><a href=\"AdminWeek\">Week view</a></div>");
                foreach (Clinician p in site.Providers.Where(p => p.Active && (own == string.Empty || p.Id == own)).OrderBy(p => p.Order))
                {
                    List<(TimeSpan From, TimeSpan To)> w = e.Windows(p, today);
                    int n = live.Count(a => a.Provider == p.Id && a.Status != "noshow");
                    double av = w.Sum(x => (x.To - x.From).TotalMinutes);
                    double bk = live.Where(a => a.Provider == p.Id && a.Status != "noshow").Sum(a => (double)a.Minutes);
                    int pc = av > 0 ? (int)Math.Round(Math.Min(100, bk * 100 / av)) : 0;
                    string hours = w.Count > 0 ? Ui.Hm(site, w.Min(x => x.From)) + " &ndash; " + Ui.Hm(site, w.Max(x => x.To)) : e.Off(p, today) ? "Away today" : "Not in today";
                    sb.Append("<div class=\"db-prow\">" + Ui.Face("db", p) + "<div><b>" + Ui.Enc(p.Name) + "</b><small>" + hours + "</small></div><div>" + (w.Count > 0 ? "<em>" + n + " booked &middot; " + pc + "%</em><div class=\"db-bar2\"><i style=\"width:" + pc + "%\"></i></div>" : string.Empty) + "</div></div>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Recent activity</h2><a href=\"AdminActivity\">All</a></div>");
            List<Activity> acts = (await Store.LoadActivity(c.Sbx)).Where(a => Access.Can(u, "activity", 'V') || a.User == u.Id).Take(7).ToList();
            if (acts.Count == 0)
            {
                sb.Append("<p class=\"db-none\">No activity yet.</p>");
            }
            foreach (Activity a in acts)
            {
                sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(Who(staff, a.User)) + " &middot; " + Ui.Enc(a.Action) + (a.Ok ? string.Empty : " (refused)") + "</b><small>" + Ui.Enc(a.Target) + " &middot; " + Ui.Time(a.When) + "</small></div></div>");
            }
            sb.Append("</section><section class=\"db-panel\"><h2>Quick links</h2><div class=\"db-quick\">");
            foreach (Area a in Access.Areas.Where(a => Access.Can(u, a.Key, 'V')).Take(8))
            {
                string name = a.Key switch { "providers" => site.Settings.ProvidersWord, "rooms" => site.Settings.RoomsWord, "patients" => site.Settings.PatientsWord, _ => a.Name };
                sb.Append("<a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"" + a.Page + "\">" + Ui.Enc(name) + "</a>");
            }
            sb.Append("</div></section></div></div>");
            return sb.ToString();
        }

        private static bool Live(Appointment a)
        {
            return a.Status is "booked" or "confirmed" or "arrived" or "inroom";
        }

        public async Task<ApiResponse> Msg()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, "appointments", 'V'))
            {
                return await Refuse(response, c, "message", "open", "You can't open website messages.");
            }
            Message? m;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                m = site.Messages.FirstOrDefault(x => x.Id == GetDataValue("id"));
                if (m != null && !m.Read && Access.Can(c.U, "appointments", 'E'))
                {
                    m.Read = true;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (m == null)
            {
                return response;
            }
            response.SetElementContents("db-mbody", "<h2>" + Ui.Enc(m.Topic) + "</h2><dl class=\"db-dl\"><dt>From</dt><dd>" + Ui.Enc(m.Name) + "</dd><dt>Email</dt><dd><a href=\"mailto:" + Ui.Attr(m.Email) + "\">" + Ui.Enc(m.Email) + "</a></dd>" + (m.Phone != string.Empty ? "<dt>Phone</dt><dd>" + Ui.Enc(m.Phone) + "</dd>" : string.Empty) + "<dt>Sent</dt><dd>" + Ui.Time(m.When, "full") + "</dd></dl><p class=\"db-mtext\">" + Ui.Enc(m.Text) + "</p><div class=\"db-mfoot\"><button type=\"button\" class=\"db-btn db-btn-ghost\" onclick=\"AdminJs.closeModal()\">Close</button><a class=\"db-btn db-btn-accent\" href=\"mailto:" + Ui.Attr(m.Email) + "?subject=" + Uri.EscapeDataString("Re: " + m.Topic) + "\">Reply by email</a></div>");
            Chrome(response, c, site);
            response.ExecuteScript("AdminJs.openModal();times(document.getElementById('db-mbody'));");
            return response;
        }
    }
}
