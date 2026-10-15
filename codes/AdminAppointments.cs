using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminAppointments : WebPage
    {
        private const string AreaKey = "appointments";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Appointments");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--ap-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ap-menu", Auth.Menu("ap", "AdminAppointments", c, site));
            response.SetElementContents("ap-who", Auth.Who("ap", c));
            response.SetElementContents("ap-demo", Auth.DemoPill("ap", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminAppointmentsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ap-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ap-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAppointmentsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ap-main", NoAccess("Appointments"));
                return response;
            }
            response.SetElementContents("ap-main", await MainHtml(c, site));
            response.ExecuteScript("AdminAppointmentsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ap-tr ap-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 25;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            string own = Auth.Mine(c);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<div class=\"ap-ptop\"><div><h1>Appointments</h1><p>Search every booking, open one to check in, move or cancel it.</p></div><div class=\"ap-acts\">" + (edit ? "<button type=\"button\" class=\"ap-btn ap-btn-accent\" onclick=\"AdminAppointmentsJs.newAppt('" + Ui.Js(own) + "','','','')\">New appointment</button>" : string.Empty) + "</div></div>");
            sb.Append("<div class=\"ap-bar\"><label>Search<input type=\"search\" id=\"ap-q\" placeholder=\"Code, name or phone\" maxlength=\"60\" oninput=\"AdminAppointmentsJs.search()\"></label><label>Status<select id=\"ap-ast\" onchange=\"AdminAppointmentsJs.list(1)\"><option value=\"\">Any status</option><option value=\"live\">Not finished</option>" + Ui.Opts(Ui.Statuses.Select(s => (s.Key, s.Name)), string.Empty) + "</select></label>");
            if (own == string.Empty)
            {
                sb.Append("<label>" + Ui.Enc(site.Settings.ProviderWord) + "<select id=\"ap-apv\" onchange=\"AdminAppointmentsJs.list(1)\"><option value=\"\">Everyone</option>" + Ui.Opts(site.Providers.OrderBy(p => p.Order).Select(p => (p.Id, p.Name)), string.Empty) + "</select></label>");
            }
            else
            {
                sb.Append("<input type=\"hidden\" id=\"ap-apv\" value=\"\">");
            }
            sb.Append("<label>Booked<select id=\"ap-asrc\" onchange=\"AdminAppointmentsJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Any way"), ("web", "Online"), ("phone", "Phone"), ("desk", "At the desk"), ("walkin", "Walk-in") }, string.Empty) + "</select></label></div><div class=\"ap-bar\" id=\"ap-perbar\">" + Ui.PeriodBar("ap", "AdminAppointmentsJs.per", "7", string.Empty, string.Empty, true) + "</div><div class=\"ap-tbl ap-aptbl\" id=\"ap-list\"></div><div id=\"ap-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            string own = Auth.Mine(c);
            string q = Ui.Clip(GetDataValue("q"), 60);
            string st = Ui.Clip(GetDataValue("st"), 12);
            string prov = own != string.Empty ? own : Ui.Clip(GetDataValue("prov"), 40);
            string src = Ui.Clip(GetDataValue("src"), 12);
            string per = GetDataValue("per") == string.Empty ? "7" : Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(e.Today, per, GetDataValue("pfrom"), GetDataValue("pto"), true);
            Dictionary<string, Patient> pm = Ui.PatientMap(site);
            string d = Ui.Digits(q);
            IEnumerable<Appointment> all = site.Appointments.Where(a => Ui.InPeriod(a.Start, from, to) && (prov == string.Empty || a.Provider == prov) && (src == string.Empty || a.Source == src) && (st == string.Empty || (st == "live" ? Live(a) : a.Status == st)));
            if (q != string.Empty)
            {
                all = all.Where(a =>
                {
                    Patient? p = pm.TryGetValue(a.Patient, out Patient? x) ? x : null;
                    return a.Code.Contains(q, StringComparison.OrdinalIgnoreCase) || (p != null && ((p.First + " " + p.Last).Contains(q, StringComparison.OrdinalIgnoreCase) || p.Email.Contains(q, StringComparison.OrdinalIgnoreCase) || (d.Length >= 3 && Ui.Digits(p.Phone).Contains(d, StringComparison.Ordinal))));
                });
            }
            List<Appointment> list = per == "all" ? all.OrderByDescending(a => a.Start).ToList() : all.OrderBy(a => a.Start).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, list.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("When", string.Empty), (site.Settings.PatientWord, string.Empty), ("Service", string.Empty), ("Booked", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (list.Count == 0)
            {
                sb.Append("<div class=\"ap-tr\"><span class=\"ap-none ap-wide\">No appointments match.</span></div>");
            }
            foreach (Appointment a in list.Skip((page - 1) * Size).Take(Size))
            {
                Patient? p = pm.TryGetValue(a.Patient, out Patient? x) ? x : null;
                sb.Append("<div class=\"ap-tr\"><span data-l=\"When\" class=\"ap-when\"><b>" + Ui.Day(a.Start, a.Start.Year != e.Today.Year) + "</b><small>" + Ui.Hm(site, a.Start) + " &middot; " + Ui.Dur(a.Minutes) + "</small></span><span data-l=\"" + Ui.Attr(site.Settings.PatientWord) + "\"><b>" + Ui.Enc(Ui.Name(p)) + "</b><small>" + Ui.Enc(p?.Phone ?? string.Empty) + "</small></span><span data-l=\"Service\" class=\"ap-wide\"><b>" + Ui.Enc(Ui.ServiceName(site, a.Service)) + "</b><small>" + Ui.Enc(Ui.ProviderName(site, a.Provider)) + " &middot; <span class=\"ap-code\">" + Ui.Enc(a.Code) + "</span></small></span><span data-l=\"Booked\">" + Source(a.Source) + "</span><span data-l=\"Status\">" + Ui.StatusPill("ap", a) + "</span><span><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminAppointmentsJs.openAppt('" + Ui.Js(a.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("ap-list", sb.ToString());
            response.SetElementContents("ap-pager", Ui.Pager("ap", page, Size, list.Count, "appointment", "appointments", "AdminAppointmentsJs.list"));
            response.SetElementContents("ap-perbar", Ui.PeriodBar("ap", "AdminAppointmentsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto"), true));
            return response;
        }

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
            return "<button type=\"button\" class=\"ap-btn ap-btn-sm " + kind + "\" onclick=\"AdminAppointmentsJs.act('" + id + "','" + act + "')\">" + label + "</button>";
        }

        private static string Detail(SiteData site, Ctx c, Appointment a, Dictionary<string, Staff> staff)
        {
            Engine e = new Engine(site);
            Patient? pt = site.Patients.FirstOrDefault(x => x.Id == a.Patient);
            CareService? sv = Ui.ServiceOf(site, a.Service);
            Clinician? p = Ui.ProviderOf(site, a.Provider);
            bool edit = Access.Can(c.U, AreaKey, 'E') && Mine(c, a);
            string id = Ui.Js(a.Id);
            StringBuilder sb = new StringBuilder("<div class=\"ap-ad\" style=\"--c:" + Ui.ColorOf(site, a) + "\"><div class=\"ap-adh\"><div><h2>" + Ui.Enc(Ui.Name(pt)) + (a.NewPatient ? "<span class=\"ap-newp\">New " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span>" : string.Empty) + "</h2><p>" + Ui.Enc(sv?.Name ?? a.Service) + " &middot; " + Ui.Dur(a.Minutes) + " &middot; " + Ui.Enc(p?.Name ?? "Unassigned") + (a.Room != string.Empty ? " &middot; " + Ui.Enc(Ui.RoomName(site, a.Room)) : string.Empty) + "</p></div>" + Ui.StatusPill("ap", a) + "</div>");
            sb.Append("<div class=\"ap-adtime\"><b>" + Ui.LongDay(a.Start) + "</b><span>" + Ui.Hm(site, a.Start) + " &ndash; " + Ui.Hm(site, a.Start.AddMinutes(a.Minutes)) + "</span><span class=\"ap-code\">" + Ui.Enc(a.Code) + "</span></div>");
            if (edit)
            {
                sb.Append("<div class=\"ap-adacts\">");
                if (a.Status == "booked")
                {
                    sb.Append(Btn(id, "confirm", "Confirm", "ap-btn-ghost") + Btn(id, "checkin", "Check in", "ap-btn-accent"));
                }
                else if (a.Status == "confirmed")
                {
                    sb.Append(Btn(id, "checkin", "Check in", "ap-btn-accent"));
                }
                else if (a.Status == "arrived")
                {
                    sb.Append(Btn(id, "start", "Start visit", "ap-btn-accent"));
                }
                else if (a.Status == "inroom")
                {
                    sb.Append(Btn(id, "done", "Complete", "ap-btn-accent"));
                }
                if ((a.Status is "booked" or "confirmed") && a.Start <= e.Now)
                {
                    sb.Append(Btn(id, "noshow", "No-show", "ap-btn-ghost"));
                }
                if (a.Status is "booked" or "confirmed" or "arrived")
                {
                    sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminAppointmentsJs.moveForm('" + id + "')\">Change time</button>");
                }
                if (Live(a) && Access.Can(c.U, "appointments", 'X'))
                {
                    sb.Append(Btn(id, "cancel", "Cancel", "ap-btn-danger"));
                }
                if (!Live(a))
                {
                    sb.Append(Btn(id, "reopen", "Reopen", "ap-btn-ghost"));
                }
                sb.Append("</div><div id=\"ap-mv\"></div>");
            }
            sb.Append("<div class=\"ap-grid2 ap-adg\"><section class=\"ap-adbox\"><h3>" + Ui.Enc(site.Settings.PatientWord) + "</h3><dl class=\"ap-dl\">");
            if (pt != null)
            {
                sb.Append("<dt>Phone</dt><dd>" + (pt.Phone != string.Empty ? "<a href=\"tel:" + Ui.Attr(Regex.Replace(pt.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(pt.Phone) + "</a>" : "&mdash;") + "</dd><dt>Email</dt><dd>" + (pt.Email != string.Empty ? "<a href=\"mailto:" + Ui.Attr(pt.Email) + "\">" + Ui.Enc(pt.Email) + "</a>" : "&mdash;") + "</dd><dt>Prefers</dt><dd>" + (pt.Contact == "email" ? "Email" : pt.Contact == "phone" ? "Phone call" : "Text message") + "</dd><dt>Insurance</dt><dd>" + Ui.Enc(pt.Insurance != string.Empty ? pt.Insurance : "Not given") + "</dd>" + (pt.Note != string.Empty ? "<dt>Desk note</dt><dd>" + Ui.Enc(pt.Note) + "</dd>" : string.Empty));
            }
            sb.Append("</dl>" + (pt != null && Access.Can(c.U, "patients", 'V') ? "<a href=\"AdminPatients?id=" + Uri.EscapeDataString(pt.Id) + "\">Open " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + " record &rarr;</a>" : string.Empty) + "</section><section class=\"ap-adbox\"><h3>Booking</h3><dl class=\"ap-dl\"><dt>How</dt><dd>" + Source(a.Source) + "</dd><dt>Booked</dt><dd>" + Ui.Time(a.Created) + "</dd><dt>By</dt><dd>" + Ui.Enc(Who(staff, a.By)) + "</dd>");
            if (a.Note != string.Empty)
            {
                sb.Append("<dt>Their note</dt><dd>" + Ui.Enc(a.Note) + "</dd>");
            }
            foreach (Answer an in a.Answers)
            {
                sb.Append("<dt>" + Ui.Enc(an.Q) + "</dt><dd>" + Ui.Enc(an.A) + "</dd>");
            }
            sb.Append("</dl></section></div><section class=\"ap-desk\"><h3>Front-desk note</h3><textarea id=\"ap-desk\" rows=\"2\" maxlength=\"300\"" + (edit ? string.Empty : " readonly") + " placeholder=\"Admin notes only, for example: parking pass given, paid at desk\">" + Ui.Enc(a.Desk) + "</textarea><div><span class=\"ap-hint\">Keep clinical notes in your clinical record system, not here.</span>" + (edit ? "<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminAppointmentsJs.desk('" + id + "')\">Save note</button>" : string.Empty) + "</div></section><section><h3>History</h3><ul class=\"ap-hist\">");
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
                response.ExecuteScript("AdminAppointmentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Appointment? a = site.Appointments.FirstOrDefault(x => x.Id == GetDataValue("id"));
            if (a == null || !Mine(c, a) || !Access.Can(c.U, AreaKey, 'V'))
            {
                response.ExecuteScript("AdminAppointmentsJs.toast('That appointment is not available.');");
                return response;
            }
            response.SetElementContents("ap-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminAppointmentsJs.openModal();if(AdminAppointmentsJs.at){AdminAppointmentsJs.at('" + Ui.Ymd(a.Start) + "');}");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
                    response.SetElementContents("ap-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
                }
                response.ExecuteScript("AdminAppointmentsJs.refresh();AdminAppointmentsJs.toast('" + Ui.Js(err) + "');");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, act, a!.Code + " · " + Ui.Name(site.Patients.FirstOrDefault(x => x.Id == a.Patient)), true);
            response.SetElementContents("ap-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            Chrome(response, c, site);
            response.ExecuteScript("AdminAppointmentsJs.refresh();AdminAppointmentsJs.toast('" + Ui.Js(text) + "');");
            return response;
        }

        public async Task<ApiResponse> Desk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
            response.ExecuteScript("AdminAppointmentsJs.toast('Note saved');");
            return response;
        }

        public async Task<ApiResponse> MoveForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ap-mvbox\"><h3>Change time</h3><div class=\"ap-f2\"><label class=\"ap-field\"><span>Date</span><input type=\"date\" id=\"ap-mvd\" min=\"" + Ui.Ymd(e.Today) + "\" value=\"" + Ui.Ymd(day) + "\" onchange=\"AdminAppointmentsJs.moveForm('" + id + "')\"></label><label class=\"ap-field\"><span>" + Ui.Enc(site.Settings.ProviderWord) + "</span><select id=\"ap-mvp\" onchange=\"AdminAppointmentsJs.moveForm('" + id + "')\">" + Ui.Opts(ps.Select(p => (p.Id, p.Name)), who?.Id ?? string.Empty) + "</select></label></div>");
            List<DateTime> free = who != null ? e.Free(who, a.Minutes, day, false, a.Id) : new List<DateTime>();
            if (free.Count == 0)
            {
                sb.Append("<p class=\"ap-hint\">" + (e.Clinic(day) == null ? "The clinic is closed on this day." : "No free " + Ui.Dur(a.Minutes) + " times on this day.") + "</p>");
            }
            else
            {
                sb.Append("<div class=\"ap-mvfree\">");
                foreach (DateTime t in free)
                {
                    sb.Append("<button type=\"button\" onclick=\"AdminAppointmentsJs.moveTo('" + id + "','" + t.ToString("HH:mm", Ui.Inv) + "','" + Ui.Hm(site, t) + "')\">" + Ui.Hm(site, t) + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("<details><summary>Another time</summary><div class=\"ap-mvman\"><input type=\"time\" id=\"ap-mvt\" step=\"300\" value=\"" + a.Start.ToString("HH:mm", Ui.Inv) + "\" aria-label=\"Time\"><label class=\"ap-chk\"><input type=\"checkbox\" id=\"ap-mvf\"> Allow overlap</label><button type=\"button\" class=\"ap-btn ap-btn-sm\" onclick=\"AdminAppointmentsJs.moveManual('" + id + "')\">Move</button></div></details><em class=\"ap-err\" id=\"ap-e-mv\"></em></div>");
            response.SetElementContents("ap-mv", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
                response.SetElementContents("ap-e-mv", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "move", a!.Code + " · " + Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start), true);
            response.SetElementContents("ap-mbody", Detail(site, c, a, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminAppointmentsJs.refresh();AdminAppointmentsJs.toast('Moved to " + Ui.Js(Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start)) + "');");
            return response;
        }

        public async Task<ApiResponse> NewForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
            StringBuilder sb = new StringBuilder("<h2>New appointment</h2><div class=\"ap-form\"><div class=\"ap-npwho\"><div class=\"ap-seg2\" role=\"radiogroup\"><label><input type=\"radio\" name=\"ap-npm\" value=\"old\" checked onchange=\"AdminAppointmentsJs.npMode()\"><span>Existing " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span></label><label><input type=\"radio\" name=\"ap-npm\" value=\"new\" onchange=\"AdminAppointmentsJs.npMode()\"><span>New " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span></label></div>");
            sb.Append("<div id=\"ap-npx\" class=\"ap-form\"><input type=\"search\" class=\"ap-npq\" id=\"ap-npq\" placeholder=\"Search by name, phone or email\" maxlength=\"60\" oninput=\"AdminAppointmentsJs.pfind()\" autocomplete=\"off\"" + (pt != null ? " hidden" : string.Empty) + "><div class=\"ap-nplist\" id=\"ap-nplist\"></div><input type=\"hidden\" id=\"ap-npid\" value=\"" + Ui.Attr(pt?.Id ?? string.Empty) + "\"><div class=\"ap-npsel\" id=\"ap-npsel\">" + (pt != null ? "<span><b>" + Ui.Enc(Ui.Name(pt)) + "</b><small>" + Ui.Enc(pt.Phone) + " &middot; " + Ui.Enc(pt.Email) + "</small></span><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminAppointmentsJs.pclear()\">Change</button>" : string.Empty) + "</div></div>");
            sb.Append("<div id=\"ap-npn\" class=\"ap-form\" hidden><div class=\"ap-f2\"><label class=\"ap-field\"><span>First name</span><input type=\"text\" id=\"ap-nfirst\" maxlength=\"40\"></label><label class=\"ap-field\"><span>Last name</span><input type=\"text\" id=\"ap-nlast\" maxlength=\"40\"></label></div><div class=\"ap-f2\"><label class=\"ap-field\"><span>Phone</span><input type=\"tel\" id=\"ap-nphone\" maxlength=\"30\"></label><label class=\"ap-field\"><span>Email</span><input type=\"email\" id=\"ap-nemail\" maxlength=\"120\"></label></div></div></div>");
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
            sb.Append("<div class=\"ap-f2\"><label class=\"ap-field\"><span>" + Ui.Enc(site.Settings.ProviderWord) + "</span><select id=\"ap-nprov\" onchange=\"AdminAppointmentsJs.svFilter()\">" + (own == string.Empty ? "<option value=\"\">Choose</option>" : string.Empty) + Ui.Opts(ps.Select(p => (p.Id, p.Name)), prov) + "</select></label><label class=\"ap-field\"><span>Service</span><select id=\"ap-nsv\">" + opts + "</select></label></div>");
            sb.Append("<div class=\"ap-f3\"><label class=\"ap-field\"><span>Date</span><input type=\"date\" id=\"ap-nd\" min=\"" + Ui.Ymd(e.Today) + "\" value=\"" + Ui.Ymd(day < e.Today ? e.Today : day) + "\"></label><label class=\"ap-field\"><span>Time</span><input type=\"time\" id=\"ap-nt\" step=\"300\" value=\"" + Ui.Attr(time) + "\"></label><label class=\"ap-field\"><span>Booked by</span><select id=\"ap-nsrc\">" + Ui.Opts(new[] { ("phone", "Phone"), ("desk", "At the desk"), ("walkin", "Walk-in (checks in now)") }, "phone") + "</select></label></div>");
            sb.Append("<label class=\"ap-field\"><span>Front-desk note (optional)</span><input type=\"text\" id=\"ap-nnote\" maxlength=\"300\" placeholder=\"Admin notes only\"></label><label class=\"ap-chk\"><input type=\"checkbox\" id=\"ap-nforce\"> Allow overlap or a time outside working hours</label><em class=\"ap-err\" id=\"ap-e-new\" role=\"alert\"></em></div><div class=\"ap-mfoot\"><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminAppointmentsJs.closeModal()\">Cancel</button><button type=\"button\" class=\"ap-btn ap-btn-accent\" onclick=\"AdminAppointmentsJs.create()\">Book appointment</button></div>");
            response.SetElementContents("ap-mbody", sb.ToString());
            response.ExecuteScript("AdminAppointmentsJs.openModal();" + (pt == null ? "setTimeout(function(){var q=document.getElementById('ap-npq');if(q){q.focus();}},50);" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> PFind()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
                return response;
            }
            string q = Ui.Clip(GetDataValue("q"), 60);
            if (q.Length < 2)
            {
                response.SetElementContents("ap-nplist", string.Empty);
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string d = Ui.Digits(q);
            List<Patient> hits = site.Patients.Where(p => (p.First + " " + p.Last).Contains(q, StringComparison.OrdinalIgnoreCase) || p.Email.Contains(q, StringComparison.OrdinalIgnoreCase) || (d.Length >= 3 && Ui.Digits(p.Phone).Contains(d, StringComparison.Ordinal))).OrderBy(p => p.Last).ThenBy(p => p.First).Take(8).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (Patient p in hits)
            {
                int n = site.Appointments.Count(a => a.Patient == p.Id && a.Status == "done");
                sb.Append("<button type=\"button\" class=\"ap-npi\" onclick=\"AdminAppointmentsJs.pchoose(this,'" + Ui.Js(p.Id) + "')\"><b>" + Ui.Enc(Ui.Name(p)) + "</b><small>" + Ui.Enc(p.Phone) + " &middot; " + Ui.Enc(p.Email) + " &middot; " + n + (n == 1 ? " visit" : " visits") + "</small></button>");
            }
            if (hits.Count == 0)
            {
                sb.Append("<p class=\"ap-hint\">No match. Switch to New to add this person.</p>");
            }
            response.SetElementContents("ap-nplist", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Create()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAppointmentsJs.expired();");
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
                response.SetElementContents("ap-e-new", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "book", a.Code + " · " + Ui.Name(site.Patients.FirstOrDefault(x => x.Id == a.Patient)), true);
            Chrome(response, c, site);
            response.ExecuteScript("AdminAppointmentsJs.closeModal();AdminAppointmentsJs.refresh();AdminAppointmentsJs.toast('Booked " + Ui.Js(a.Code) + " for " + Ui.Js(Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start)) + "');");
            return response;
        }
    }
}
