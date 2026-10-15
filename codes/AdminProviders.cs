using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminProviders : WebPage
    {
        private const string AreaKey = "providers";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Clinicians");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--pd-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("pd-menu", Auth.Menu("pd", "AdminProviders", c, site));
            response.SetElementContents("pd-who", Auth.Who("pd", c));
            response.SetElementContents("pd-demo", Auth.DemoPill("pd", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminProvidersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"pd-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"pd-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminProvidersJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminProvidersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("pd-main", NoAccess("Clinicians"));
                return response;
            }
            response.SetElementContents("pd-main", await MainHtml(c, site));
            response.ExecuteScript("AdminProvidersJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"pd-tr pd-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Slug(string v)
        {
            string s = Regex.Replace(Regex.Replace((v ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-"), "^-+|-+$", string.Empty);
            return s.Length > 30 ? s.Substring(0, 30).Trim('-') : s;
        }

        private static string Hhmm(string v)
        {
            return Clock.Hm(v) != null ? Clock.Text(Clock.Hm(v)!.Value) : string.Empty;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Engine e = new Engine(site);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            string word = site.Settings.ProviderWord.ToLowerInvariant();
            StringBuilder sb = new StringBuilder("<div class=\"pd-ptop\"><div><h1>" + Ui.Enc(site.Settings.ProvidersWord) + "</h1><p>Profiles, working hours, services and time off. Online booking uses these to show free times.</p></div><div class=\"pd-acts\">" + (edit ? "<button type=\"button\" class=\"pd-btn pd-btn-accent\" onclick=\"AdminProvidersJs.edit('')\">Add " + Ui.Enc(word) + "</button>" : string.Empty) + "</div></div><div class=\"pd-pcards\">");
            List<Specialty> specs = site.Specialties.OrderBy(x => x.Order).ToList();
            foreach (Clinician p in site.Providers.OrderByDescending(p => p.Active).ThenBy(p => specs.FindIndex(s => s.Key == p.Specialty)).ThenBy(p => p.Order))
            {
                Specialty? sp = Ui.SpecialtyOf(site, p.Specialty);
                List<(TimeSpan From, TimeSpan To)> w = e.Windows(p, e.Today);
                int week = site.Appointments.Count(a => a.Provider == p.Id && a.Start >= e.Now && a.Start.Date < e.Today.AddDays(7) && (a.Status is "booked" or "confirmed"));
                TimeOff? next = p.TimeOff.Where(t => t.To.Date >= e.Today).OrderBy(t => t.From).FirstOrDefault();
                sb.Append("<article class=\"pd-pc\" style=\"--c:" + Ui.Attr(Ui.SafeColor(p.Color) ? p.Color : "#1F7A74") + "\"><div class=\"pd-pch\">" + Ui.Face("pd", p) + "<div><b>" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</b><small>" + Ui.Enc(p.Title) + " &middot; " + Ui.Enc(sp?.Name ?? string.Empty) + "</small></div></div><div class=\"pd-pcf\">" + (p.Active ? "<span class=\"pd-st pd-st-on\">Active</span>" : "<span class=\"pd-st pd-st-off\">Hidden</span>") + (p.Active ? p.Accepting ? "<span class=\"pd-st pd-st-view\">Online booking on</span>" : "<span class=\"pd-st pd-st-off\">Online booking off</span>" : string.Empty) + "</div><div class=\"pd-pcl\"><span>Today: <b>" + (w.Count > 0 ? Ui.Hm(site, w.Min(x => x.From)) + " &ndash; " + Ui.Hm(site, w.Max(x => x.To)) : e.Off(p, e.Today) ? "Away" : "Not in") + "</b></span><span>Next 7 days: <b>" + week + " booked</b></span><span>Services: <b>" + site.Services.Count(s => s.Active && s.Providers.Contains(p.Id)) + "</b> &middot; " + Ui.Enc(Ui.RoomName(site, p.Room)) + "</span>" + (next != null ? "<span>Time off: <b>" + Ui.Day(next.From, false) + (next.To.Date != next.From.Date ? " &ndash; " + Ui.Day(next.To, false) : string.Empty) + "</b></span>" : string.Empty) + "</div><div class=\"pd-pcb\"><button type=\"button\" class=\"pd-btn pd-btn-sm " + (edit ? "pd-btn-accent" : "pd-btn-ghost") + "\" onclick=\"AdminProvidersJs.edit('" + Ui.Js(p.Id) + "')\">" + (edit ? "Edit" : "View") + "</button><a class=\"pd-btn pd-btn-sm pd-btn-ghost\" href=\"AdminWeek?p=" + Uri.EscapeDataString(p.Id) + "\">Week</a><a class=\"pd-btn pd-btn-sm pd-btn-ghost\" href=\"Provider?id=" + Uri.EscapeDataString(p.Id) + "\" target=\"_blank\" rel=\"noopener\">Profile page</a></div></article>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminProvidersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            Clinician? p = site.Providers.FirstOrDefault(x => x.Id == GetDataValue("id"));
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool offEdit = Access.Can(c.U, AreaKey, 'X');
            if (p == null && !edit)
            {
                return response;
            }
            Clinician v = p ?? new Clinician { Specialty = site.Specialties.FirstOrDefault()?.Key ?? string.Empty, Hours = Enumerable.Range(0, 7).Select(d => new WorkDay { Day = d, On = d is >= 1 and <= 5 }).ToList(), Order = site.Providers.Count };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            string id = Ui.Js(p?.Id ?? string.Empty);
            StringBuilder sb = new StringBuilder("<h2>" + (p == null ? "Add " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) : Ui.Enc(p.Name)) + "</h2><div class=\"pd-tabs\"><button type=\"button\" class=\"pd-tab pd-act\" onclick=\"AdminProvidersJs.tab(this,'pd-t1')\">Profile</button><button type=\"button\" class=\"pd-tab\" onclick=\"AdminProvidersJs.tab(this,'pd-t2')\">Hours</button><button type=\"button\" class=\"pd-tab\" onclick=\"AdminProvidersJs.tab(this,'pd-t3')\">Services</button><button type=\"button\" class=\"pd-tab\" onclick=\"AdminProvidersJs.tab(this,'pd-t4')\">Time off<em>" + v.TimeOff.Count(t => t.To.Date >= e.Today) + "</em></button></div>");
            sb.Append("<div class=\"pd-pane pd-form\" id=\"pd-t1\"><div class=\"pd-f3\"><label class=\"pd-field\"><span>Full name</span><input type=\"text\" id=\"pd-vname\" maxlength=\"60\" value=\"" + Ui.Attr(v.Name) + "\"" + ro + "></label><label class=\"pd-field\"><span>Job title</span><input type=\"text\" id=\"pd-vtitle\" maxlength=\"60\" value=\"" + Ui.Attr(v.Title) + "\"" + ro + "></label><label class=\"pd-field\"><span>Letters after name</span><input type=\"text\" id=\"pd-vcred\" maxlength=\"20\" value=\"" + Ui.Attr(v.Credentials) + "\"" + ro + "></label></div>");
            sb.Append("<div class=\"pd-f4\"><label class=\"pd-field\"><span>Team</span><select id=\"pd-vspec\"" + dis + ">" + Ui.Opts(site.Specialties.OrderBy(x => x.Order).Select(x => (x.Key, x.Name)), v.Specialty) + "</select></label><label class=\"pd-field\"><span>Usual " + Ui.Enc(site.Settings.RoomWord.ToLowerInvariant()) + "</span><select id=\"pd-vroom\"" + dis + "><option value=\"\">None</option>" + Ui.Opts(site.Rooms.OrderBy(r => r.Order).Select(r => (r.Id, r.Name)), v.Room) + "</select></label><label class=\"pd-field\"><span>Colour</span><input type=\"color\" id=\"pd-vcolor\" value=\"" + Ui.Attr(Ui.SafeColor(v.Color) ? v.Color : "#1F7A74") + "\"" + dis + "></label><label class=\"pd-field\"><span>Order</span><input type=\"number\" id=\"pd-vorder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"" + ro + "></label></div>");
            sb.Append("<div class=\"pd-f2\"><label class=\"pd-field\"><span>Languages (comma between)</span><input type=\"text\" id=\"pd-vlangs\" maxlength=\"120\" value=\"" + Ui.Attr(string.Join(", ", v.Languages)) + "\"" + ro + "></label><label class=\"pd-field\"><span>Training (one per line)</span><textarea id=\"pd-vedu\" rows=\"2\" maxlength=\"400\"" + ro + ">" + Ui.Enc(Ui.Lines(v.Education)) + "</textarea></label></div><label class=\"pd-field\"><span>About (shown on the website)</span><textarea id=\"pd-vbio\" rows=\"3\" maxlength=\"600\"" + ro + ">" + Ui.Enc(v.Bio) + "</textarea></label><div class=\"pd-f2\"><label class=\"pd-chk\"><input type=\"checkbox\" id=\"pd-vaccept\"" + (v.Accepting ? " checked" : string.Empty) + dis + "> Takes online bookings</label><label class=\"pd-chk\"><input type=\"checkbox\" id=\"pd-vactive\"" + (p == null || v.Active ? " checked" : string.Empty) + dis + "> Show on the website and schedule</label></div><div><span class=\"pd-hint\">Photo</span>" + Ui.Picker("pd", "pd-vphoto", "people", v.Photo, !edit, true) + "</div></div>");
            sb.Append("<div class=\"pd-pane\" id=\"pd-t2\" hidden><div class=\"pd-hrs\" id=\"pd-hrs\"><div class=\"pd-hrow2 pd-hh\"><span>Day</span><span>Works</span><span>From</span><span>To</span><span>Break from</span><span>Break to</span></div>");
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                WorkDay w = v.Hours.FirstOrDefault(h => h.Day == d) ?? new WorkDay { Day = d };
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                sb.Append("<div class=\"pd-hrow2" + (w.On ? string.Empty : " pd-closed") + "\" data-day=\"" + d + "\"><span><b>" + Ui.DayNames[d] + "</b><small>" + (o != null && o.Open ? "Clinic " + Ui.HmText(site, o.From) + "&ndash;" + Ui.HmText(site, o.To) : "Clinic closed") + "</small></span><label><input type=\"checkbox\"" + (w.On ? " checked" : string.Empty) + dis + " onchange=\"AdminProvidersJs.dayOn(this)\" aria-label=\"Works on " + Ui.DayNames[d] + "\"></label><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(w.From) + "\"" + ro + " aria-label=\"From\"><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(w.To) + "\"" + ro + " aria-label=\"To\"><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(w.BreakFrom) + "\"" + ro + " aria-label=\"Break from\"><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(w.BreakTo) + "\"" + ro + " aria-label=\"Break to\"></div>");
            }
            sb.Append("</div><p class=\"pd-hint\" style=\"margin-top:10px\">Bookable times are these hours within the clinic's opening hours, minus the break, time off and closed days.</p></div>");
            sb.Append("<div class=\"pd-pane\" id=\"pd-t3\" hidden><div class=\"pd-cbs\" id=\"pd-vsvcs\">");
            foreach (Specialty sp in site.Specialties.OrderBy(x => x.Order))
            {
                List<CareService> list = site.Services.Where(s => s.Specialty == sp.Key).OrderBy(s => s.Order).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<h4>" + Ui.Enc(sp.Name) + "</h4>");
                foreach (CareService s in list)
                {
                    sb.Append("<label><input type=\"checkbox\" value=\"" + Ui.Attr(s.Key) + "\"" + (p != null && s.Providers.Contains(p.Id) ? " checked" : string.Empty) + dis + "> " + Ui.Enc(s.Name) + " <small>" + Ui.Dur(s.Minutes) + (s.Active ? string.Empty : " &middot; hidden") + "</small></label>");
                }
            }
            sb.Append("</div></div><div class=\"pd-pane pd-form\" id=\"pd-t4\" hidden><div class=\"pd-offl\">");
            List<TimeOff> offs = v.TimeOff.Where(t => t.To.Date >= e.Today.AddDays(-30)).OrderBy(t => t.From).ToList();
            if (offs.Count == 0)
            {
                sb.Append("<p class=\"pd-none\">No time off booked.</p>");
            }
            foreach (TimeOff t in offs)
            {
                sb.Append("<div class=\"pd-offr\" data-id=\"" + Ui.Attr(t.Id) + "\"><span><b>" + Ui.Day(t.From, false) + (t.To.Date != t.From.Date ? " &ndash; " + Ui.Day(t.To, false) : string.Empty) + "</b>" + (t.Note != string.Empty ? " &middot; " + Ui.Enc(t.Note) : string.Empty) + "</span>" + (offEdit ? "<button type=\"button\" class=\"pd-btn pd-btn-sm pd-btn-ghost\" onclick=\"AdminProvidersJs.dropOff(this)\">Remove</button>" : string.Empty) + "</div>");
            }
            sb.Append("</div>");
            if (offEdit && p != null)
            {
                sb.Append("<h3>Add time off</h3><div class=\"pd-offnew\"><label class=\"pd-field\"><span>From</span><input type=\"date\" id=\"pd-ofrom\" min=\"" + Ui.Ymd(e.Today) + "\"></label><label class=\"pd-field\"><span>To</span><input type=\"date\" id=\"pd-oto\" min=\"" + Ui.Ymd(e.Today) + "\"></label><label class=\"pd-field\"><span>Note</span><input type=\"text\" id=\"pd-onote\" maxlength=\"60\" placeholder=\"Annual leave, training&hellip;\"></label></div><p class=\"pd-hint\">Online booking stops offering these days. Appointments already booked stay; the schedule shows them so you can move them.</p>");
            }
            else if (!offEdit)
            {
                sb.Append("<p class=\"pd-hint\">Only staff with the Time off right can change this.</p>");
            }
            sb.Append("</div><em class=\"pd-err\" id=\"pd-e-prov\" role=\"alert\"></em><div class=\"pd-mfoot\"><button type=\"button\" class=\"pd-btn pd-btn-ghost\" onclick=\"AdminProvidersJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (edit ? "<button type=\"button\" class=\"pd-btn pd-btn-accent\" onclick=\"AdminProvidersJs.save('" + id + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("pd-mbody", sb.ToString());
            response.ExecuteScript("AdminProvidersJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminProvidersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save", GetDataValue("id"), "You can view but not change this.");
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("name"), 60), "\\s+", " ");
            List<WorkDay> hours = new List<WorkDay>();
            string err = name.Length < 2 ? "Enter the full name." : string.Empty;
            foreach (string row in GetDataValue("hours").Split(';'))
            {
                string[] x = row.Split('|');
                if (x.Length != 6 || !int.TryParse(x[0], out int d) || d < 0 || d > 6)
                {
                    continue;
                }
                WorkDay w = new WorkDay { Day = d, On = x[1] == "1", From = Hhmm(x[2]) != string.Empty ? Hhmm(x[2]) : "09:00", To = Hhmm(x[3]) != string.Empty ? Hhmm(x[3]) : "17:00", BreakFrom = Hhmm(x[4]), BreakTo = Hhmm(x[5]) };
                if (w.On && string.CompareOrdinal(w.From, w.To) >= 0)
                {
                    err = err == string.Empty ? Ui.DayNames[d] + ": the end time must be after the start time." : err;
                }
                if ((w.BreakFrom == string.Empty) != (w.BreakTo == string.Empty) || (w.BreakFrom != string.Empty && string.CompareOrdinal(w.BreakFrom, w.BreakTo) >= 0))
                {
                    err = err == string.Empty ? Ui.DayNames[d] + ": give the break a start and a later end, or leave both empty." : err;
                }
                hours.Add(w);
            }
            bool offx = GetDataValue("offx") == "1" && Access.Can(c.U, AreaKey, 'X');
            DateTime? of = Ui.ParseDay(GetDataValue("offfrom"));
            DateTime? ot = Ui.ParseDay(GetDataValue("offto")) ?? of;
            if (offx && of != null && ot != null && ot < of)
            {
                err = err == string.Empty ? "Time off: the last day must be on or after the first day." : err;
            }
            if (err != string.Empty)
            {
                response.SetElementContents("pd-e-prov", err);
                return response;
            }
            int outside = 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Clinician? p = site.Providers.FirstOrDefault(x => x.Id == id);
                if (p == null)
                {
                    if (id != string.Empty)
                    {
                        return response;
                    }
                    string slug = "p-" + Slug(name.Replace("Dr. ", string.Empty).Split(' ')[^1]);
                    string nid = slug;
                    for (int i = 2; site.Providers.Any(x => x.Id == nid); i++)
                    {
                        nid = slug + "-" + i;
                    }
                    p = new Clinician { Id = nid };
                    site.Providers.Add(p);
                }
                p.Name = name;
                p.Title = Ui.Clip(GetDataValue("title"), 60);
                p.Credentials = Ui.Clip(GetDataValue("cred"), 20);
                p.Specialty = site.Specialties.Any(s => s.Key == GetDataValue("spec")) ? GetDataValue("spec") : p.Specialty;
                p.Room = site.Rooms.Any(r => r.Id == GetDataValue("room")) ? GetDataValue("room") : string.Empty;
                p.Color = Ui.SafeColor(GetDataValue("color")) ? GetDataValue("color") : p.Color;
                p.Languages = GetDataValue("langs").Split(',').Select(x => Ui.Clip(x, 30)).Where(x => x != string.Empty).Distinct().Take(8).ToList();
                p.Education = Ui.SplitLines(GetDataValue("edu"), 5, 100);
                p.Bio = Ui.Clip(GetDataValue("bio"), 600);
                string photo = GetDataValue("photo");
                p.Photo = photo == string.Empty || (Access.SafeImage(photo) && Ui.Images("people").Contains(photo)) ? photo : p.Photo;
                p.Order = int.TryParse(GetDataValue("order"), out int ord) ? Math.Clamp(ord, 0, 99) : p.Order;
                p.Accepting = GetDataValue("accept") == "1";
                p.Active = GetDataValue("active") == "1";
                if (hours.Count == 7)
                {
                    p.Hours = hours.OrderBy(h => h.Day).ToList();
                }
                HashSet<string> svcs = GetDataValue("svcs").Split(',').Where(x => x != string.Empty).ToHashSet();
                foreach (CareService s in site.Services)
                {
                    bool has = s.Providers.Contains(p.Id);
                    if (svcs.Contains(s.Key) && !has)
                    {
                        s.Providers.Add(p.Id);
                    }
                    else if (!svcs.Contains(s.Key) && has)
                    {
                        s.Providers.Remove(p.Id);
                    }
                }
                if (offx)
                {
                    HashSet<string> keep = GetDataValue("offkeep").Split(',').ToHashSet();
                    p.TimeOff = p.TimeOff.Where(t => keep.Contains(t.Id) || t.To.Date < Clock.Today(site).AddDays(-30)).ToList();
                    if (of != null && ot != null)
                    {
                        p.TimeOff.Add(new TimeOff { Id = Codes.Id("to"), From = of.Value, To = ot.Value, Note = Ui.Clip(GetDataValue("offnote"), 60) });
                    }
                }
                id = p.Id;
                await Store.SaveSite(site, c.Sbx);
                Engine e = new Engine(site);
                foreach (Appointment a in site.Appointments.Where(a => a.Provider == p.Id && a.Start >= e.Now && (a.Status is "booked" or "confirmed")))
                {
                    double m = a.Start.TimeOfDay.TotalMinutes;
                    if (!e.Windows(p, a.Start.Date).Any(w => m >= w.From.TotalMinutes && m + a.Minutes <= w.To.TotalMinutes))
                    {
                        outside++;
                    }
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "clinician", name, true);
            response.ExecuteScript("AdminProvidersJs.closeModal();AdminProvidersJs.list();AdminProvidersJs.toast('" + (outside > 0 ? "Saved. " + outside + " booked appointment" + (outside == 1 ? " is" : "s are") + " now outside working hours. Check the schedule." : "Saved") + "');");
            return response;
        }
    }
}
