using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminSchedule : WebPage
    {
        private const string AreaKey = "schedule";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Day schedule");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--sc-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("sc-menu", Auth.Menu("sc", "AdminSchedule", c, site));
            response.SetElementContents("sc-who", Auth.Who("sc", c));
            response.SetElementContents("sc-demo", Auth.DemoPill("sc", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminScheduleJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"sc-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"sc-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminScheduleJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("sc-main", NoAccess("Day schedule"));
                return response;
            }
            response.SetElementContents("sc-main", await MainHtml(c, site));
            response.ExecuteScript("AdminScheduleJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"sc-tr sc-th\">");
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
            bool edit = Access.Can(c.U, AreaKey, 'E');
            string own = Auth.Mine(c);
            StringBuilder sb = new StringBuilder("<div class=\"sc-ptop\"><div><h1>" + (own != string.Empty ? "My day" : "Day schedule") + "</h1><p>" + (edit ? "Click an appointment to check in, move or cancel it. Click an empty time to book." : "Click an appointment to see the details.") + "</p></div><div class=\"sc-acts\">" + (edit ? "<button type=\"button\" class=\"sc-btn sc-btn-accent\" onclick=\"AdminScheduleJs.newAppt('" + Ui.Js(own) + "','','','')\">New appointment</button>" : string.Empty) + "<button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"window.print()\">Print</button></div></div>");
            sb.Append("<div class=\"sc-bar\"><div class=\"sc-dnav\"><button type=\"button\" class=\"sc-btn sc-btn-ghost sc-btn-sm sc-arrow\" aria-label=\"Previous day\" onclick=\"AdminScheduleJs.shift(-1)\">&lsaquo;</button><button type=\"button\" class=\"sc-btn sc-btn-ghost sc-btn-sm\" onclick=\"AdminScheduleJs.today()\">Today</button><button type=\"button\" class=\"sc-btn sc-btn-ghost sc-btn-sm sc-arrow\" aria-label=\"Next day\" onclick=\"AdminScheduleJs.shift(1)\">&rsaquo;</button><input type=\"date\" id=\"sc-d\" value=\"" + Ui.Ymd(e.Today) + "\" onchange=\"AdminScheduleJs.list()\" aria-label=\"Date\"></div>");
            sb.Append("<label>Show<select id=\"sc-view\" onchange=\"AdminScheduleJs.list()\">" + Ui.Opts(new[] { ("prov", "By " + site.Settings.ProviderWord.ToLowerInvariant()), ("room", "By " + site.Settings.RoomWord.ToLowerInvariant()) }, "prov") + "</select></label>");
            if (own == string.Empty)
            {
                sb.Append("<label>Team<select id=\"sc-sp\" onchange=\"AdminScheduleJs.list()\"><option value=\"\">All teams</option>" + Ui.Opts(site.Specialties.OrderBy(x => x.Order).Select(x => (x.Key, x.Name)), string.Empty) + "</select></label>");
            }
            else
            {
                sb.Append("<input type=\"hidden\" id=\"sc-sp\" value=\"\">");
            }
            sb.Append("<label class=\"sc-chk\" style=\"align-self:center\"><input type=\"checkbox\" id=\"sc-cx\" onchange=\"AdminScheduleJs.list()\"> Show cancelled</label></div><div id=\"sc-daysum\" class=\"sc-daysum\"></div><div class=\"sc-gridw\" id=\"sc-gridw\"><p class=\"sc-loading\" style=\"padding:20px\">Loading&hellip;</p></div>" + Legend);
            return sb.ToString();
        }

        public async Task<ApiResponse> Grid()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            DateTime day = Ui.ParseDay(GetDataValue("d")) ?? e.Today;
            bool rooms = GetDataValue("view") == "room";
            string sp = Ui.Clip(GetDataValue("sp"), 40);
            bool cx = GetDataValue("cx") == "1";
            string own = Auth.Mine(c);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            Dictionary<string, Patient> pm = Ui.PatientMap(site);
            List<Appointment> appts = site.Appointments.Where(a => a.Start.Date == day && (own == string.Empty || a.Provider == own) && (sp == string.Empty || Ui.ServiceOf(site, a.Service)?.Specialty == sp)).ToList();
            List<Appointment> shown = appts.Where(a => cx || a.Status != "cancelled").ToList();
            (TimeSpan From, TimeSpan To)? open = e.Clinic(day);
            int start = (int)(open?.From.TotalMinutes ?? 480);
            int end = (int)(open?.To.TotalMinutes ?? 1080);
            List<(string Id, string Head, List<(TimeSpan From, TimeSpan To)> Work, string Away, List<Appointment> Items)> cols = new List<(string, string, List<(TimeSpan, TimeSpan)>, string, List<Appointment>)>();
            List<string> notIn = new List<string>();
            List<Specialty> specs = site.Specialties.OrderBy(x => x.Order).ToList();
            if (!rooms)
            {
                foreach (Clinician p in site.Providers.Where(p => (own == string.Empty || p.Id == own) && (sp == string.Empty || p.Specialty == sp)).OrderBy(p => specs.FindIndex(s => s.Key == p.Specialty)).ThenBy(p => p.Order))
                {
                    List<(TimeSpan From, TimeSpan To)> w = e.Windows(p, day);
                    List<Appointment> items = shown.Where(a => a.Provider == p.Id).ToList();
                    if (w.Count == 0 && items.Count == 0 && own == string.Empty)
                    {
                        if (p.Active)
                        {
                            notIn.Add(p.Name + (e.Off(p, day) ? " (away)" : string.Empty));
                        }
                        continue;
                    }
                    double av = w.Sum(x => (x.To - x.From).TotalMinutes);
                    double bk = items.Where(a => a.Status is not ("cancelled" or "noshow")).Sum(a => (double)a.Minutes);
                    int n = items.Count(a => a.Status is not ("cancelled" or "noshow"));
                    string head = "<div class=\"sc-ch\" title=\"" + Ui.Attr(p.Name) + "\">" + Ui.Face("sc", p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + n + " booked" + (av > 0 ? " &middot; " + (int)Math.Round(Math.Min(100, bk * 100 / av)) + "%" : string.Empty) + "</small></span></div>";
                    TimeOff? off = p.TimeOff.FirstOrDefault(t => day >= t.From.Date && day <= t.To.Date);
                    cols.Add((p.Id, head, w, off != null ? "Away" + (off.Note != string.Empty ? " &middot; " + Ui.Enc(off.Note) : string.Empty) : w.Count == 0 ? "Not working" : string.Empty, items));
                    foreach ((TimeSpan f, TimeSpan t) in w)
                    {
                        start = Math.Min(start, (int)f.TotalMinutes);
                        end = Math.Max(end, (int)t.TotalMinutes);
                    }
                }
            }
            else
            {
                foreach (ClinicRoom r in site.Rooms.Where(r => r.Active || shown.Any(a => a.Room == r.Id)).OrderBy(r => r.Order))
                {
                    List<Appointment> items = shown.Where(a => a.Room == r.Id).ToList();
                    if (own != string.Empty && items.Count == 0)
                    {
                        continue;
                    }
                    List<(TimeSpan From, TimeSpan To)> w = open != null ? new List<(TimeSpan, TimeSpan)> { open.Value } : new List<(TimeSpan, TimeSpan)>();
                    double av = w.Sum(x => (x.To - x.From).TotalMinutes);
                    double bk = items.Where(a => a.Status is not ("cancelled" or "noshow")).Sum(a => (double)a.Minutes);
                    cols.Add((string.Empty, "<div class=\"sc-ch\"><span><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(r.Kind) + (av > 0 ? " &middot; " + (int)Math.Round(Math.Min(100, bk * 100 / av)) + "% in use" : string.Empty) + "</small></span></div>", w, string.Empty, items));
                }
            }
            foreach (Appointment a in shown)
            {
                start = Math.Min(start, (int)a.Start.TimeOfDay.TotalMinutes);
                end = Math.Max(end, (int)a.Start.TimeOfDay.TotalMinutes + a.Minutes);
            }
            start = start / 60 * 60;
            end = Math.Min(1440, (end + 59) / 60 * 60);
            StringBuilder sum = new StringBuilder("<span class=\"sc-dayh\">" + Ui.LongDay(day) + "</span>");
            foreach ((string k, string n) in Ui.Statuses)
            {
                int cnt = appts.Count(a => a.Status == k);
                if (cnt > 0)
                {
                    sum.Append("<span class=\"sc-st sc-st-" + k + "\">" + n + " " + cnt + "</span>");
                }
            }
            Holiday? hol = e.HolidayOn(day);
            if (hol != null)
            {
                sum.Append("<span class=\"sc-st sc-st-noshow\">Closed &middot; " + Ui.Enc(hol.Name) + "</span>");
            }
            if (notIn.Count > 0)
            {
                sum.Append("<span class=\"sc-hint\">Not in: " + Ui.Enc(string.Join(", ", notIn)) + "</span>");
            }
            StringBuilder sb = new StringBuilder();
            if (cols.Count == 0)
            {
                sb.Append("<div class=\"sc-closed\"><b>" + (open == null ? "The clinic is closed on this day." : "Nobody is working on this day.") + "</b><p>Use the arrows to go to another day.</p></div>");
            }
            else
            {
                double hpx = (end - start) * 1.6;
                sb.Append("<div class=\"sc-sch\" id=\"sc-sch\" data-start=\"" + start + "\" data-step=\"" + e.Step + "\" data-day=\"" + Ui.Ymd(day) + "\" data-edit=\"" + (edit ? "1" : "0") + "\" style=\"--n:" + cols.Count + ";--h:" + hpx.ToString("0", Ui.Inv) + "px\"><div class=\"sc-schh\"><div></div>");
                foreach (var col in cols)
                {
                    sb.Append(col.Head);
                }
                bool nowOn = day == e.Today && e.Now.TimeOfDay.TotalMinutes >= start && e.Now.TimeOfDay.TotalMinutes <= end;
                string nowTop = ((e.Now.TimeOfDay.TotalMinutes - start) * 1.6).ToString("0", Ui.Inv);
                sb.Append("</div><div class=\"sc-tcol\">" + HourLabels(site, start, end) + (nowOn ? "<em class=\"sc-nowt\" style=\"top:" + nowTop + "px\">" + Ui.Hm(site, e.Now) + "</em>" : string.Empty) + "</div>");
                foreach (var col in cols)
                {
                    sb.Append("<div class=\"sc-col" + (edit ? " sc-can" : string.Empty) + "\" data-p=\"" + Ui.Attr(col.Id) + "\" onclick=\"AdminScheduleJs.slot(event,this)\">" + (col.Away != string.Empty && col.Work.Count == 0 ? "<i class=\"sc-off sc-away\" style=\"top:0;height:100%\"><span>" + col.Away + "</span></i>" : Shade(col.Work, start, end)) + Blocks(site, col.Items, start, pm, rooms) + (nowOn ? "<i class=\"sc-nowl\" style=\"top:" + nowTop + "px\"></i>" : string.Empty) + "</div>");
                }
                sb.Append("</div>");
            }
            response.SetElementContents("sc-daysum", sum.ToString());
            response.SetElementContents("sc-gridw", sb.ToString());
            response.ExecuteScript("AdminScheduleJs.drawn('" + Ui.Ymd(day) + "'," + (day == e.Today ? "true" : "false") + ");");
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
                    sb.Append("<button type=\"button\" class=\"sc-blk sc-b-" + a.Status + (a.Minutes <= 15 ? " sc-tiny" : string.Empty) + "\" style=\"top:" + top.ToString("0.#", Ui.Inv) + "px;height:" + h.ToString("0.#", Ui.Inv) + "px;left:" + (lane[a] * w).ToString("0.##", Ui.Inv) + "%;width:" + w.ToString("0.##", Ui.Inv) + "%;--c:" + Ui.ColorOf(site, a) + "\" title=\"" + Ui.Attr(title) + "\" onclick=\"event.stopPropagation();AdminScheduleJs.openAppt('" + Ui.Js(a.Id) + "')\"><span class=\"sc-bl1\"><b>" + a.Start.ToString(site.Settings.Clock24 ? "HH:mm" : "h:mm", Ui.Inv) + "</b> " + Ui.Enc(who) + (a.NewPatient ? " <i>New</i>" : string.Empty) + "</span><small>" + Ui.Enc(sv?.Name ?? string.Empty) + (showProv ? " &middot; " + Ui.Enc(Ui.ProviderName(site, a.Provider)) : string.Empty) + "</small></button>");
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
                    sb.Append("<i class=\"sc-off\" style=\"top:" + ((cur - start) * 1.6).ToString("0.#", Ui.Inv) + "px;height:" + ((a - cur) * 1.6).ToString("0.#", Ui.Inv) + "px\"></i>");
                }
                cur = Math.Max(cur, b);
            }
            if (cur < end)
            {
                sb.Append("<i class=\"sc-off\" style=\"top:" + ((cur - start) * 1.6).ToString("0.#", Ui.Inv) + "px;height:" + ((end - cur) * 1.6).ToString("0.#", Ui.Inv) + "px\"></i>");
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

        private const string Legend = "<div class=\"sc-legend\"><span><i style=\"background:#EAF1FA\"></i>Booked</span><span><i style=\"background:#DCF0EC\"></i>Confirmed</span><span><i style=\"background:#FCEBCB\"></i>Checked in</span><span><i style=\"background:#E6DDF5\"></i>In room</span><span><i style=\"background:#E7EFEA\"></i>Completed</span><span><i style=\"background:#F8E1DF\"></i>No-show</span><span><i style=\"background:repeating-linear-gradient(135deg,rgba(120,140,136,.25) 0 3px,transparent 3px 6px);border-left-color:transparent\"></i>Not working</span></div>";

        private static bool Mine(Ctx c, Appointment a)
        {
            string own = Auth.Mine(c);
            return own == string.Empty || a.Provider == own;
        }

        private static bool Live(Appointment a)
        {
            return a.Status is "booked" or "confirmed" or "arrived" or "inroom";
        }

        private static string Source(string s)
        {
            return s switch { "web" => "Online", "phone" => "Phone", "walkin" => "Walk-in", "desk" => "At the desk", _ => s };
        }

        private static string Btn(string id, string act, string label, string kind)
        {
            return "<button type=\"button\" class=\"sc-btn sc-btn-sm " + kind + "\" onclick=\"AdminScheduleJs.act('" + id + "','" + act + "')\">" + label + "</button>";
        }

        private static string Detail(SiteData site, Ctx c, Appointment a, Dictionary<string, Staff> staff)
        {
            Engine e = new Engine(site);
            Patient? pt = site.Patients.FirstOrDefault(x => x.Id == a.Patient);
            CareService? sv = Ui.ServiceOf(site, a.Service);
            Clinician? p = Ui.ProviderOf(site, a.Provider);
            bool edit = Access.Can(c.U, AreaKey, 'E') && Mine(c, a);
            string id = Ui.Js(a.Id);
            StringBuilder sb = new StringBuilder("<div class=\"sc-ad\" style=\"--c:" + Ui.ColorOf(site, a) + "\"><div class=\"sc-adh\"><div><h2>" + Ui.Enc(Ui.Name(pt)) + (a.NewPatient ? "<span class=\"sc-newp\">New " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span>" : string.Empty) + "</h2><p>" + Ui.Enc(sv?.Name ?? a.Service) + " &middot; " + Ui.Dur(a.Minutes) + " &middot; " + Ui.Enc(p?.Name ?? "Unassigned") + (a.Room != string.Empty ? " &middot; " + Ui.Enc(Ui.RoomName(site, a.Room)) : string.Empty) + "</p></div>" + Ui.StatusPill("sc", a) + "</div>");
            sb.Append("<div class=\"sc-adtime\"><b>" + Ui.LongDay(a.Start) + "</b><span>" + Ui.Hm(site, a.Start) + " &ndash; " + Ui.Hm(site, a.Start.AddMinutes(a.Minutes)) + "</span><span class=\"sc-code\">" + Ui.Enc(a.Code) + "</span></div>");
            if (edit)
            {
                sb.Append("<div class=\"sc-adacts\">");
                if (a.Status == "booked")
                {
                    sb.Append(Btn(id, "confirm", "Confirm", "sc-btn-ghost") + Btn(id, "checkin", "Check in", "sc-btn-accent"));
                }
                else if (a.Status == "confirmed")
                {
                    sb.Append(Btn(id, "checkin", "Check in", "sc-btn-accent"));
                }
                else if (a.Status == "arrived")
                {
                    sb.Append(Btn(id, "start", "Start visit", "sc-btn-accent"));
                }
                else if (a.Status == "inroom")
                {
                    sb.Append(Btn(id, "done", "Complete", "sc-btn-accent"));
                }
                if ((a.Status is "booked" or "confirmed") && a.Start <= e.Now)
                {
                    sb.Append(Btn(id, "noshow", "No-show", "sc-btn-ghost"));
                }
                if (a.Status is "booked" or "confirmed" or "arrived")
                {
                    sb.Append("<button type=\"button\" class=\"sc-btn sc-btn-sm sc-btn-ghost\" onclick=\"AdminScheduleJs.moveForm('" + id + "')\">Change time</button>");
                }
                if (Live(a) && Access.Can(c.U, "appointments", 'X'))
                {
                    sb.Append(Btn(id, "cancel", "Cancel", "sc-btn-danger"));
                }
                if (!Live(a))
                {
                    sb.Append(Btn(id, "reopen", "Reopen", "sc-btn-ghost"));
                }
                sb.Append("</div><div id=\"sc-mv\"></div>");
            }
            sb.Append("<div class=\"sc-grid2 sc-adg\"><section class=\"sc-adbox\"><h3>" + Ui.Enc(site.Settings.PatientWord) + "</h3><dl class=\"sc-dl\">");
            if (pt != null)
            {
                sb.Append("<dt>Phone</dt><dd>" + (pt.Phone != string.Empty ? "<a href=\"tel:" + Ui.Attr(Regex.Replace(pt.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(pt.Phone) + "</a>" : "&mdash;") + "</dd><dt>Email</dt><dd>" + (pt.Email != string.Empty ? "<a href=\"mailto:" + Ui.Attr(pt.Email) + "\">" + Ui.Enc(pt.Email) + "</a>" : "&mdash;") + "</dd><dt>Prefers</dt><dd>" + (pt.Contact == "email" ? "Email" : pt.Contact == "phone" ? "Phone call" : "Text message") + "</dd><dt>Insurance</dt><dd>" + Ui.Enc(pt.Insurance != string.Empty ? pt.Insurance : "Not given") + "</dd>" + (pt.Note != string.Empty ? "<dt>Desk note</dt><dd>" + Ui.Enc(pt.Note) + "</dd>" : string.Empty));
            }
            sb.Append("</dl>" + (pt != null && Access.Can(c.U, "patients", 'V') ? "<a href=\"AdminPatients?id=" + Uri.EscapeDataString(pt.Id) + "\">Open " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + " record &rarr;</a>" : string.Empty) + "</section><section class=\"sc-adbox\"><h3>Booking</h3><dl class=\"sc-dl\"><dt>How</dt><dd>" + Source(a.Source) + "</dd><dt>Booked</dt><dd>" + Ui.Time(a.Created) + "</dd><dt>By</dt><dd>" + Ui.Enc(Who(staff, a.By)) + "</dd>");
            if (a.Note != string.Empty)
            {
                sb.Append("<dt>Their note</dt><dd>" + Ui.Enc(a.Note) + "</dd>");
            }
            foreach (Answer an in a.Answers)
            {
                sb.Append("<dt>" + Ui.Enc(an.Q) + "</dt><dd>" + Ui.Enc(an.A) + "</dd>");
            }
            sb.Append("</dl></section></div><section class=\"sc-desk\"><h3>Front-desk note</h3><textarea id=\"sc-desk\" rows=\"2\" maxlength=\"300\"" + (edit ? string.Empty : " readonly") + " placeholder=\"Admin notes only, for example: parking pass given, paid at desk\">" + Ui.Enc(a.Desk) + "</textarea><div><span class=\"sc-hint\">Keep clinical notes in your clinical record system, not here.</span>" + (edit ? "<button type=\"button\" class=\"sc-btn sc-btn-sm sc-btn-ghost\" onclick=\"AdminScheduleJs.desk('" + id + "')\">Save note</button>" : string.Empty) + "</div></section><section><h3>History</h3><ul class=\"sc-hist\">");
            foreach (Hist h in a.History.AsEnumerable().Reverse())
            {
                sb.Append("<li><span>" + Ui.Time(h.When) + "</span><span><b>" + Ui.Enc(Who(staff, h.By)) + "</b> &middot; " + Ui.Enc(h.Text) + "</span></li>");
            }
            sb.Append("</ul></section></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Appointment? a = site.Appointments.FirstOrDefault(x => x.Id == GetDataValue("id"));
            if (a == null || !Mine(c, a) || !Access.Can(c.U, AreaKey, 'V'))
            {
                response.ExecuteScript("AdminScheduleJs.toast('That appointment is not available.');");
                return response;
            }
            response.SetElementContents("sc-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminScheduleJs.openModal();if(AdminScheduleJs.at){AdminScheduleJs.at('" + Ui.Ymd(a.Start) + "');}");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string act = Ui.Clip(GetDataValue("act"), 12);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, act, id, "You can look but not change appointments.");
            }
            if (act == "cancel" && !Access.Can(c.U, "appointments", 'X'))
            {
                return await Refuse(response, c, act, id, "Only staff with the Cancel right can cancel appointments.");
            }
            string why = Ui.Clip(GetDataValue("why"), 120);
            string err = string.Empty;
            string text = string.Empty;
            SiteData site;
            Appointment? a;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                a = site.Appointments.FirstOrDefault(x => x.Id == id);
                if (a == null || !Mine(c, a))
                {
                    err = "That appointment is not available.";
                }
                else
                {
                    Engine e = new Engine(site);
                    string from = a.Status;
                    (string To, string Text)? step = act switch
                    {
                        "confirm" when from == "booked" => ("confirmed", "Confirmed"),
                        "checkin" when from is "booked" or "confirmed" => ("arrived", "Checked in"),
                        "start" when from == "arrived" => ("inroom", "Visit started"),
                        "done" when from is "inroom" or "arrived" => ("done", "Completed"),
                        "noshow" when (from is "booked" or "confirmed") && a.Start <= e.Now => ("noshow", "Marked as no-show"),
                        "cancel" when Live(a) => ("cancelled", "Cancelled"),
                        "reopen" when !Live(a) => (from == "done" ? "inroom" : "booked", "Reopened"),
                        _ => null
                    };
                    if (step == null)
                    {
                        err = "That step no longer applies. The view has been refreshed.";
                    }
                    else if (act == "reopen" && from != "done" && e.Busy(a.Provider, a.Room, a.Start, a.Minutes, a.Id))
                    {
                        err = "That time is now taken by someone else. Book a new time instead.";
                    }
                    else
                    {
                        a.Status = step.Value.To;
                        a.Seed = false;
                        text = step.Value.Text;
                        a.History.Add(new Hist { When = DateTime.UtcNow, By = c.U.Id, Text = text + (act == "cancel" && why != string.Empty ? " · " + why : string.Empty) });
                        await Store.SaveSite(site, c.Sbx);
                    }
                }
            }
            if (err != string.Empty)
            {
                if (a != null && Mine(c, a))
                {
                    response.SetElementContents("sc-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
                }
                response.ExecuteScript("AdminScheduleJs.refresh();AdminScheduleJs.toast('" + Ui.Js(err) + "');");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, act, a!.Code + " · " + Ui.Name(site.Patients.FirstOrDefault(x => x.Id == a.Patient)), true);
            response.SetElementContents("sc-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            Chrome(response, c, site);
            response.ExecuteScript("AdminScheduleJs.refresh();AdminScheduleJs.toast('" + Ui.Js(text) + "');");
            return response;
        }

        public async Task<ApiResponse> Desk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "note", id, "You can look but not change appointments.");
            }
            string code = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Appointment? a = site.Appointments.FirstOrDefault(x => x.Id == id);
                if (a == null || !Mine(c, a))
                {
                    return response;
                }
                a.Desk = Ui.Clip(GetDataValue("text"), 300);
                code = a.Code;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "note", code, true);
            response.ExecuteScript("AdminScheduleJs.toast('Note saved');");
            return response;
        }

        public async Task<ApiResponse> MoveForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Appointment? a = site.Appointments.FirstOrDefault(x => x.Id == GetDataValue("id"));
            if (a == null || !Mine(c, a) || !Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            Engine e = new Engine(site);
            CareService? sv = Ui.ServiceOf(site, a.Service);
            DateTime day = Ui.ParseDay(GetDataValue("date")) ?? a.Start.Date;
            string own = Auth.Mine(c);
            List<Clinician> ps = site.Providers.Where(p => p.Active && (sv == null || sv.Providers.Contains(p.Id) || p.Id == a.Provider) && (own == string.Empty || p.Id == own)).OrderBy(p => p.Order).ToList();
            Clinician? who = ps.FirstOrDefault(p => p.Id == GetDataValue("prov")) ?? ps.FirstOrDefault(p => p.Id == a.Provider) ?? ps.FirstOrDefault();
            string id = Ui.Js(a.Id);
            StringBuilder sb = new StringBuilder("<div class=\"sc-mvbox\"><h3>Change time</h3><div class=\"sc-f2\"><label class=\"sc-field\"><span>Date</span><input type=\"date\" id=\"sc-mvd\" min=\"" + Ui.Ymd(e.Today) + "\" value=\"" + Ui.Ymd(day) + "\" onchange=\"AdminScheduleJs.moveForm('" + id + "')\"></label><label class=\"sc-field\"><span>" + Ui.Enc(site.Settings.ProviderWord) + "</span><select id=\"sc-mvp\" onchange=\"AdminScheduleJs.moveForm('" + id + "')\">" + Ui.Opts(ps.Select(p => (p.Id, p.Name)), who?.Id ?? string.Empty) + "</select></label></div>");
            List<DateTime> free = who != null ? e.Free(who, a.Minutes, day, false, a.Id) : new List<DateTime>();
            if (free.Count == 0)
            {
                sb.Append("<p class=\"sc-hint\">" + (e.Clinic(day) == null ? "The clinic is closed on this day." : "No free " + Ui.Dur(a.Minutes) + " times on this day.") + "</p>");
            }
            else
            {
                sb.Append("<div class=\"sc-mvfree\">");
                foreach (DateTime t in free)
                {
                    sb.Append("<button type=\"button\" onclick=\"AdminScheduleJs.moveTo('" + id + "','" + t.ToString("HH:mm", Ui.Inv) + "','" + Ui.Hm(site, t) + "')\">" + Ui.Hm(site, t) + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("<details><summary>Another time</summary><div class=\"sc-mvman\"><input type=\"time\" id=\"sc-mvt\" step=\"300\" value=\"" + a.Start.ToString("HH:mm", Ui.Inv) + "\" aria-label=\"Time\"><label class=\"sc-chk\"><input type=\"checkbox\" id=\"sc-mvf\"> Allow overlap</label><button type=\"button\" class=\"sc-btn sc-btn-sm\" onclick=\"AdminScheduleJs.moveManual('" + id + "')\">Move</button></div></details><em class=\"sc-err\" id=\"sc-e-mv\"></em></div>");
            response.SetElementContents("sc-mv", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "move", id, "You can look but not change appointments.");
            }
            DateTime? day = Ui.ParseDay(GetDataValue("date"));
            TimeSpan? tm = Clock.Hm(GetDataValue("time"));
            bool force = GetDataValue("force") == "1";
            string err = string.Empty;
            string text = string.Empty;
            SiteData site;
            Appointment? a;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                Engine e = new Engine(site);
                a = site.Appointments.FirstOrDefault(x => x.Id == id);
                Clinician? p = site.Providers.FirstOrDefault(x => x.Id == GetDataValue("prov") && x.Active);
                CareService? sv = a != null ? Ui.ServiceOf(site, a.Service) : null;
                string own = Auth.Mine(c);
                if (a == null || !Mine(c, a) || !(a.Status is "booked" or "confirmed" or "arrived"))
                {
                    err = "This appointment can't be moved now.";
                }
                else if (day == null || tm == null)
                {
                    err = "Choose a date and time.";
                }
                else if (p == null || (own != string.Empty && p.Id != own))
                {
                    err = "Choose who the appointment is with.";
                }
                else if (day.Value < e.Today)
                {
                    err = "Choose today or a later day.";
                }
                else
                {
                    DateTime start = day.Value.Add(tm.Value);
                    if (sv != null && !sv.Providers.Contains(p.Id) && p.Id != a.Provider)
                    {
                        err = p.Name + " doesn't offer " + sv.Name + ".";
                    }
                    else if (!force && (sv == null || !e.Fits(p, sv, start, a.Minutes, false, a.Id)))
                    {
                        err = "That time overlaps another appointment or is outside working hours. Tick Allow overlap to book it anyway.";
                    }
                    else
                    {
                        text = "Moved from " + Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start) + (p.Id != a.Provider ? " (" + Ui.ProviderName(site, a.Provider) + ")" : string.Empty);
                        a.Start = start;
                        a.Provider = p.Id;
                        a.Room = p.Room;
                        a.Seed = false;
                        a.History.Add(new Hist { When = DateTime.UtcNow, By = c.U.Id, Text = text + (force ? " · overlap allowed" : string.Empty) });
                        await Store.SaveSite(site, c.Sbx);
                    }
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("sc-e-mv", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "move", a!.Code + " · " + Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start), true);
            response.SetElementContents("sc-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminScheduleJs.refresh();AdminScheduleJs.toast('Moved to " + Ui.Js(Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start)) + "');");
            return response;
        }

        public async Task<ApiResponse> NewForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "new", "appointment", "You can look but not book appointments.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            string own = Auth.Mine(c);
            List<Clinician> ps = site.Providers.Where(p => p.Active && (own == string.Empty || p.Id == own)).OrderBy(p => p.Order).ToList();
            string prov = ps.Any(p => p.Id == GetDataValue("prov")) ? GetDataValue("prov") : own;
            DateTime day = Ui.ParseDay(GetDataValue("date")) ?? e.Today;
            string time = Clock.Hm(GetDataValue("time")) != null ? GetDataValue("time") : string.Empty;
            Patient? pt = site.Patients.FirstOrDefault(x => x.Id == GetDataValue("pid"));
            StringBuilder sb = new StringBuilder("<h2>New appointment</h2><div class=\"sc-form\"><div class=\"sc-npwho\"><div class=\"sc-seg2\" role=\"radiogroup\"><label><input type=\"radio\" name=\"sc-npm\" value=\"old\" checked onchange=\"AdminScheduleJs.npMode()\"><span>Existing " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span></label><label><input type=\"radio\" name=\"sc-npm\" value=\"new\" onchange=\"AdminScheduleJs.npMode()\"><span>New " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span></label></div>");
            sb.Append("<div id=\"sc-npx\" class=\"sc-form\"><input type=\"search\" class=\"sc-npq\" id=\"sc-npq\" placeholder=\"Search by name, phone or email\" maxlength=\"60\" oninput=\"AdminScheduleJs.pfind()\" autocomplete=\"off\"" + (pt != null ? " hidden" : string.Empty) + "><div class=\"sc-nplist\" id=\"sc-nplist\"></div><input type=\"hidden\" id=\"sc-npid\" value=\"" + Ui.Attr(pt?.Id ?? string.Empty) + "\"><div class=\"sc-npsel\" id=\"sc-npsel\">" + (pt != null ? "<span><b>" + Ui.Enc(Ui.Name(pt)) + "</b><small>" + Ui.Enc(pt.Phone) + " &middot; " + Ui.Enc(pt.Email) + "</small></span><button type=\"button\" class=\"sc-btn sc-btn-sm sc-btn-ghost\" onclick=\"AdminScheduleJs.pclear()\">Change</button>" : string.Empty) + "</div></div>");
            sb.Append("<div id=\"sc-npn\" class=\"sc-form\" hidden><div class=\"sc-f2\"><label class=\"sc-field\"><span>First name</span><input type=\"text\" id=\"sc-nfirst\" maxlength=\"40\"></label><label class=\"sc-field\"><span>Last name</span><input type=\"text\" id=\"sc-nlast\" maxlength=\"40\"></label></div><div class=\"sc-f2\"><label class=\"sc-field\"><span>Phone</span><input type=\"tel\" id=\"sc-nphone\" maxlength=\"30\"></label><label class=\"sc-field\"><span>Email</span><input type=\"email\" id=\"sc-nemail\" maxlength=\"120\"></label></div></div></div>");
            StringBuilder opts = new StringBuilder("<option value=\"\">Choose a service</option>");
            foreach (Specialty sp in site.Specialties.OrderBy(x => x.Order))
            {
                List<CareService> list = site.Services.Where(x => x.Active && x.Specialty == sp.Key).OrderBy(x => x.Order).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                opts.Append("<optgroup label=\"" + Ui.Attr(sp.Name) + "\">");
                foreach (CareService sv in list)
                {
                    bool ok = prov == string.Empty || sv.Providers.Contains(prov);
                    opts.Append("<option value=\"" + Ui.Attr(sv.Key) + "\" data-p=\"" + Ui.Attr(string.Join(" ", sv.Providers)) + "\"" + (ok ? string.Empty : " disabled hidden") + ">" + Ui.Enc(sv.Name) + " (" + Ui.Dur(sv.Minutes) + ")</option>");
                }
                opts.Append("</optgroup>");
            }
            sb.Append("<div class=\"sc-f2\"><label class=\"sc-field\"><span>" + Ui.Enc(site.Settings.ProviderWord) + "</span><select id=\"sc-nprov\" onchange=\"AdminScheduleJs.svFilter()\">" + (own == string.Empty ? "<option value=\"\">Choose</option>" : string.Empty) + Ui.Opts(ps.Select(p => (p.Id, p.Name)), prov) + "</select></label><label class=\"sc-field\"><span>Service</span><select id=\"sc-nsv\">" + opts + "</select></label></div>");
            sb.Append("<div class=\"sc-f3\"><label class=\"sc-field\"><span>Date</span><input type=\"date\" id=\"sc-nd\" min=\"" + Ui.Ymd(e.Today) + "\" value=\"" + Ui.Ymd(day < e.Today ? e.Today : day) + "\"></label><label class=\"sc-field\"><span>Time</span><input type=\"time\" id=\"sc-nt\" step=\"300\" value=\"" + Ui.Attr(time) + "\"></label><label class=\"sc-field\"><span>Booked by</span><select id=\"sc-nsrc\">" + Ui.Opts(new[] { ("phone", "Phone"), ("desk", "At the desk"), ("walkin", "Walk-in (checks in now)") }, "phone") + "</select></label></div>");
            sb.Append("<label class=\"sc-field\"><span>Front-desk note (optional)</span><input type=\"text\" id=\"sc-nnote\" maxlength=\"300\" placeholder=\"Admin notes only\"></label><label class=\"sc-chk\"><input type=\"checkbox\" id=\"sc-nforce\"> Allow overlap or a time outside working hours</label><em class=\"sc-err\" id=\"sc-e-new\" role=\"alert\"></em></div><div class=\"sc-mfoot\"><button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"AdminScheduleJs.closeModal()\">Cancel</button><button type=\"button\" class=\"sc-btn sc-btn-accent\" onclick=\"AdminScheduleJs.create()\">Book appointment</button></div>");
            response.SetElementContents("sc-mbody", sb.ToString());
            response.ExecuteScript("AdminScheduleJs.openModal();" + (pt == null ? "setTimeout(function(){var q=document.getElementById('sc-npq');if(q){q.focus();}},50);" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> PFind()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            string q = Ui.Clip(GetDataValue("q"), 60);
            if (q.Length < 2)
            {
                response.SetElementContents("sc-nplist", string.Empty);
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string d = Ui.Digits(q);
            List<Patient> hits = site.Patients.Where(p => (p.First + " " + p.Last).Contains(q, StringComparison.OrdinalIgnoreCase) || p.Email.Contains(q, StringComparison.OrdinalIgnoreCase) || (d.Length >= 3 && Ui.Digits(p.Phone).Contains(d, StringComparison.Ordinal))).OrderBy(p => p.Last).ThenBy(p => p.First).Take(8).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (Patient p in hits)
            {
                int n = site.Appointments.Count(a => a.Patient == p.Id && a.Status == "done");
                sb.Append("<button type=\"button\" class=\"sc-npi\" onclick=\"AdminScheduleJs.pchoose(this,'" + Ui.Js(p.Id) + "')\"><b>" + Ui.Enc(Ui.Name(p)) + "</b><small>" + Ui.Enc(p.Phone) + " &middot; " + Ui.Enc(p.Email) + " &middot; " + n + (n == 1 ? " visit" : " visits") + "</small></button>");
            }
            if (hits.Count == 0)
            {
                sb.Append("<p class=\"sc-hint\">No match. Switch to New to add this person.</p>");
            }
            response.SetElementContents("sc-nplist", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Create()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "book", "appointment", "You can look but not book appointments.");
            }
            bool isNew = GetDataValue("mode") == "new";
            string first = Ui.Clip(GetDataValue("first"), 40);
            string last = Ui.Clip(GetDataValue("last"), 40);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string src = GetDataValue("src") is "walkin" or "desk" ? GetDataValue("src") : "phone";
            DateTime? day = Ui.ParseDay(GetDataValue("date"));
            TimeSpan? tm = Clock.Hm(GetDataValue("time"));
            bool force = GetDataValue("force") == "1";
            string err = string.Empty;
            Appointment? a = null;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                Engine e = new Engine(site);
                CareService? sv = site.Services.FirstOrDefault(x => x.Key == GetDataValue("sv") && x.Active);
                Clinician? p = site.Providers.FirstOrDefault(x => x.Id == GetDataValue("prov") && x.Active);
                Patient? pt = isNew ? null : site.Patients.FirstOrDefault(x => x.Id == GetDataValue("pid"));
                string own = Auth.Mine(c);
                if (isNew && (first == string.Empty || last == string.Empty))
                {
                    err = "Enter the first and last name.";
                }
                else if (isNew && Ui.Digits(phone).Length < 7 && !Ui.Email(email))
                {
                    err = "Enter a phone number or an email address.";
                }
                else if (!isNew && pt == null)
                {
                    err = "Search for the " + site.Settings.PatientWord.ToLowerInvariant() + " and pick them from the list.";
                }
                else if (p == null || (own != string.Empty && p.Id != own))
                {
                    err = "Choose who the appointment is with.";
                }
                else if (sv == null)
                {
                    err = "Choose a service.";
                }
                else if (!sv.Providers.Contains(p.Id))
                {
                    err = p.Name + " doesn't offer " + sv.Name + ".";
                }
                else if (day == null || tm == null)
                {
                    err = "Choose a date and time.";
                }
                else if (day.Value < e.Today)
                {
                    err = "Choose today or a later day.";
                }
                else
                {
                    DateTime start = day.Value.Add(tm.Value);
                    if (!force && !e.Fits(p, sv, start, sv.Minutes, false, string.Empty))
                    {
                        err = "That time overlaps another appointment or is outside working hours. Tick Allow overlap to book it anyway.";
                    }
                    else if (!force && pt != null && site.Appointments.Any(x => x.Patient == pt.Id && Live(x) && x.Start < start.AddMinutes(sv.Minutes) && x.Start.AddMinutes(x.Minutes) > start))
                    {
                        err = "This " + site.Settings.PatientWord.ToLowerInvariant() + " already has an appointment at that time.";
                    }
                    else
                    {
                        if (pt == null)
                        {
                            pt = new Patient { Id = Codes.Id("pt"), First = first, Last = last, Phone = phone, Email = Ui.Email(email) ? email : string.Empty, Contact = "phone", Created = DateTime.UtcNow };
                            site.Patients.Add(pt);
                        }
                        bool walk = src == "walkin" && start <= e.Now.AddMinutes(30);
                        a = new Appointment
                        {
                            Id = Codes.Id("ap"),
                            Code = Codes.New(site),
                            Patient = pt.Id,
                            Service = sv.Key,
                            Provider = p.Id,
                            Room = p.Room,
                            Start = start,
                            Minutes = sv.Minutes,
                            Status = walk ? "arrived" : "booked",
                            Source = src,
                            NewPatient = isNew,
                            Desk = Ui.Clip(GetDataValue("note"), 300),
                            Created = DateTime.UtcNow,
                            By = c.U.Id,
                            History = new List<Hist> { new Hist { When = DateTime.UtcNow, By = c.U.Id, Text = (src == "walkin" ? "Walk-in" : src == "desk" ? "Booked at the desk" : "Booked by phone") + (force ? " · overlap allowed" : string.Empty) } }
                        };
                        if (walk)
                        {
                            a.History.Add(new Hist { When = DateTime.UtcNow, By = c.U.Id, Text = "Checked in" });
                        }
                        site.Appointments.Add(a);
                        await Store.SaveSite(site, c.Sbx);
                    }
                }
            }
            if (err != string.Empty || a == null)
            {
                response.SetElementContents("sc-e-new", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "book", a.Code + " · " + Ui.Name(site.Patients.FirstOrDefault(x => x.Id == a.Patient)), true);
            Chrome(response, c, site);
            response.ExecuteScript("AdminScheduleJs.closeModal();AdminScheduleJs.refresh();AdminScheduleJs.toast('Booked " + Ui.Js(a.Code) + " for " + Ui.Js(Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start)) + "');");
            return response;
        }
    }
}
