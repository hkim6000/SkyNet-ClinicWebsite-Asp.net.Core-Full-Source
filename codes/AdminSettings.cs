using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--st-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-menu", Auth.Menu("st", "AdminSettings", c, site));
            response.SetElementContents("st-who", Auth.Who("st", c));
            response.SetElementContents("st-demo", Auth.DemoPill("st", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"st-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"st-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("st-main", NoAccess("Settings"));
                return response;
            }
            response.SetElementContents("st-main", await MainHtml(c, site));
            response.ExecuteScript("AdminSettingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"st-tr st-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static readonly string[] Zones = { "America/New_York", "America/Chicago", "America/Denver", "America/Phoenix", "America/Los_Angeles", "America/Anchorage", "Pacific/Honolulu", "America/Toronto", "America/Vancouver", "America/Mexico_City", "America/Sao_Paulo", "Europe/London", "Europe/Dublin", "Europe/Lisbon", "Europe/Paris", "Europe/Berlin", "Europe/Madrid", "Europe/Rome", "Europe/Amsterdam", "Europe/Stockholm", "Europe/Athens", "Africa/Lagos", "Africa/Johannesburg", "Africa/Nairobi", "Asia/Dubai", "Asia/Kolkata", "Asia/Singapore", "Asia/Hong_Kong", "Asia/Shanghai", "Asia/Tokyo", "Asia/Seoul", "Australia/Perth", "Australia/Sydney", "Pacific/Auckland", "UTC" };

        private static string Field(string id, string label, string value, int max, string type = "text")
        {
            return "<label class=\"st-field\"><span>" + label + "</span><input type=\"" + type + "\" id=\"st-s" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\"></label>";
        }

        private static ContentBlock Block(SiteData site, string key)
        {
            return site.Content.TryGetValue(key, out ContentBlock? b) && b != null ? b : new ContentBlock();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            ClinicSettings s = site.Settings;
            Engine e = new Engine(site);
            StringBuilder sb = new StringBuilder("<div class=\"st-ptop\"><div><h1>Settings</h1><p>Clinic details, opening hours, booking rules and website text.</p></div></div><nav class=\"st-setnav\"><a href=\"#s-clinic\">Clinic</a><a href=\"#s-words\">Words</a><a href=\"#s-hours\">Opening hours</a><a href=\"#s-rules\">Booking rules</a><a href=\"#s-closed\">Closed days</a><a href=\"#s-ins\">Insurance</a><a href=\"#s-text\">Website text</a><a href=\"#s-faq\">Questions</a></nav><form class=\"st-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel\" id=\"s-clinic\"><h2>Clinic</h2><div class=\"st-form\"><div class=\"st-f3\">" + Field("name", "Name", s.Name, 40) + Field("town", "Town or area", s.Town, 40) + Field("tag", "Tagline", s.Tagline, 120) + "</div><div class=\"st-f3\">" + Field("addr", "Address", s.Address, 100) + Field("phone", "Phone", s.Phone, 30) + Field("email", "Email", s.Email, 80, "email") + "</div>" + Field("urgent", "Urgent care notice (shown in the footer and on Patient info)", s.Urgent, 200) + "<div class=\"st-f3\"><label class=\"st-field\"><span>Accent colour</span><input type=\"color\" id=\"st-saccent\" value=\"" + Ui.Attr(Ui.SafeColor(s.Accent) ? s.Accent : "#1F7A74") + "\"></label><label class=\"st-field\"><span>Website theme</span><select id=\"st-stheme\">" + Ui.Opts(new[] { ("light", "Light"), ("dark", "Dark") }, s.Theme) + "</select></label></div></div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-words\"><h2>Words</h2><p class=\"st-hint\">Make the site fit your practice: Doctor, Dentist, Therapist or Vet; Room, Chair or Studio; Patient or Client.</p><div class=\"st-f3\">" + Field("pw", "One provider", s.ProviderWord, 24) + Field("pws", "Many providers", s.ProvidersWord, 24) + Field("aw", "One patient", s.PatientWord, 24) + "</div><div class=\"st-f3\" style=\"margin-top:12px\">" + Field("aws", "Many patients", s.PatientsWord, 24) + Field("rw", "One room", s.RoomWord, 24) + Field("rws", "Many rooms", s.RoomsWord, 24) + "</div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-hours\"><h2>Opening hours</h2><div id=\"st-hours\">");
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay o = s.Hours.FirstOrDefault(h => h.Day == d) ?? new OpenDay { Day = d };
                sb.Append("<div class=\"st-orow" + (o.Open ? string.Empty : " st-closed") + "\" data-day=\"" + d + "\"><b>" + Ui.DayNames[d] + "</b><label><input type=\"checkbox\"" + (o.Open ? " checked" : string.Empty) + " onchange=\"AdminSettingsJs.dayOn(this)\" aria-label=\"Open on " + Ui.DayNames[d] + "\"></label><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(o.From) + "\" aria-label=\"Opens\"><input type=\"time\" step=\"300\" value=\"" + Ui.Attr(o.To) + "\" aria-label=\"Closes\"></div>");
            }
            sb.Append("</div><p class=\"st-hint\" style=\"margin-top:8px\">Each " + Ui.Enc(s.ProviderWord.ToLowerInvariant()) + "'s own hours sit inside these.</p></section>");
            List<(string, string)> zones = Zones.Select(z => (z, z.Replace('_', ' '))).ToList();
            if (!Zones.Contains(s.TimeZone))
            {
                zones.Insert(0, (s.TimeZone, s.TimeZone));
            }
            sb.Append("<section class=\"st-panel\" id=\"s-rules\"><h2>Booking rules</h2><div class=\"st-f4\"><label class=\"st-field\"><span>Time slots</span><select id=\"st-sslot\">" + Ui.Opts(new[] { ("5", "Every 5 min"), ("10", "Every 10 min"), ("15", "Every 15 min"), ("20", "Every 20 min"), ("30", "Every 30 min") }, s.SlotMinutes.ToString(Ui.Inv)) + "</select></label><label class=\"st-field\"><span>Earliest online booking (hours ahead)</span><input type=\"number\" id=\"st-snotice\" min=\"0\" max=\"72\" value=\"" + s.MinNoticeHours + "\"></label><label class=\"st-field\"><span>Book up to (days ahead)</span><input type=\"number\" id=\"st-sahead\" min=\"1\" max=\"365\" value=\"" + s.BookAheadDays + "\"></label><label class=\"st-field\"><span>Online changes close (hours before)</span><input type=\"number\" id=\"st-scancel\" min=\"0\" max=\"168\" value=\"" + s.CancelHours + "\"></label></div><div class=\"st-f3\" style=\"margin-top:12px\"><label class=\"st-field\"><span>Time zone (now " + Ui.Hm(site, e.Now) + ")</span><select id=\"st-stz\">" + Ui.Opts(zones, s.TimeZone) + "</select></label><label class=\"st-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"st-sprices\"" + (s.ShowPrices ? " checked" : string.Empty) + "> Show prices on the website</label><label class=\"st-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"st-sh24\"" + (s.Clock24 ? " checked" : string.Empty) + "> 24-hour clock</label></div></section>");
            string holRow = "<div class=\"st-hol\"><input type=\"date\" value=\"{d}\" aria-label=\"Date\"><input type=\"text\" maxlength=\"60\" value=\"{n}\" placeholder=\"Reason, for example Staff training day\" aria-label=\"Reason\"><button type=\"button\" class=\"st-ibtn\" aria-label=\"Remove\" onclick=\"AdminSettingsJs.dropRow(this)\">&times;</button></div>";
            sb.Append("<section class=\"st-panel\" id=\"s-closed\"><h2>Closed days</h2><p class=\"st-hint\">Online booking skips these days. Appointments already booked stay on the schedule.</p><div id=\"st-hols\" style=\"margin-top:10px\">");
            foreach (Holiday h in site.Holidays.Where(h => h.Date.Date >= e.Today).OrderBy(h => h.Date))
            {
                sb.Append(holRow.Replace("{d}", Ui.Ymd(h.Date)).Replace("{n}", Ui.Attr(h.Name)));
            }
            sb.Append("</div><template id=\"st-holtpl\">" + holRow.Replace("{d}", string.Empty).Replace("{n}", string.Empty) + "</template><button type=\"button\" class=\"st-btn st-btn-sm st-btn-ghost\" onclick=\"AdminSettingsJs.addRow('st-hols','st-holtpl')\">Add a closed day</button></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-ins\"><h2>Insurance plans</h2><label class=\"st-field\"><span>One plan per line</span><textarea id=\"st-sins\" rows=\"5\" maxlength=\"1200\">" + Ui.Enc(Ui.Lines(s.Insurance)) + "</textarea></label></section>");
            ContentBlock home = Block(site, "home");
            ContentBlock about = Block(site, "about");
            ContentBlock policy = Block(site, "policy");
            sb.Append("<section class=\"st-panel\" id=\"s-text\"><h2>Website text</h2><div class=\"st-form\"><div class=\"st-f2\">" + Field("htitle", "Home headline", home.Title, 80) + Field("atitle", "About headline", about.Title, 80) + "</div><div class=\"st-f2\"><label class=\"st-field\"><span>Home intro</span><textarea id=\"st-shtext\" rows=\"3\" maxlength=\"300\">" + Ui.Enc(home.Text) + "</textarea></label><label class=\"st-field\"><span>About text</span><textarea id=\"st-satext\" rows=\"3\" maxlength=\"600\">" + Ui.Enc(about.Text) + "</textarea></label></div><label class=\"st-field\"><span>Cancellation policy</span><textarea id=\"st-spolicy\" rows=\"2\" maxlength=\"400\">" + Ui.Enc(policy.Text) + "</textarea></label></div></section>");
            string faqRow = "<div class=\"st-faqr\"><input type=\"text\" maxlength=\"120\" value=\"{q}\" placeholder=\"Question\" aria-label=\"Question\"><textarea maxlength=\"500\" placeholder=\"Answer\" aria-label=\"Answer\">{a}</textarea><button type=\"button\" class=\"st-ibtn\" aria-label=\"Remove\" onclick=\"AdminSettingsJs.dropRow(this)\">&times;</button></div>";
            sb.Append("<section class=\"st-panel\" id=\"s-faq\"><h2>Questions and answers</h2><div id=\"st-faqs\">");
            foreach (Faq f in site.Faqs)
            {
                sb.Append(faqRow.Replace("{q}", Ui.Attr(f.Q)).Replace("{a}", Ui.Enc(f.A)));
            }
            sb.Append("</div><template id=\"st-faqtpl\">" + faqRow.Replace("{q}", string.Empty).Replace("{a}", string.Empty) + "</template><button type=\"button\" class=\"st-btn st-btn-sm st-btn-ghost\" onclick=\"AdminSettingsJs.addRow('st-faqs','st-faqtpl')\">Add a question</button></section>");
            sb.Append("<div class=\"st-savebar\"><em class=\"st-err\" id=\"st-e-set\" role=\"alert\"></em><button type=\"submit\" class=\"st-btn st-btn-accent\">Save settings</button></div></form>");
            return sb.ToString();
        }

        private static string Word(string v, string fallback)
        {
            string w = Regex.Replace(Ui.Clip(v, 24), "[<>\"]", string.Empty).Trim();
            return w.Length >= 2 ? w : fallback;
        }

        private static int Num(string v, int min, int max, int cur)
        {
            return int.TryParse(v, out int n) ? Math.Clamp(n, min, max) : cur;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "settings", "settings", "Only the clinic manager changes settings.");
            }
            string name = Ui.Clip(GetDataValue("name"), 40);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string tz = Ui.Clip(GetDataValue("tz"), 60);
            string err = name.Length < 2 ? "Enter the clinic name." : email != string.Empty && !Ui.Email(email) ? "Check the email address." : !Clock.Known(tz) ? "Choose a time zone." : string.Empty;
            List<OpenDay> hours = new List<OpenDay>();
            foreach (string row in GetDataValue("hours").Split('~'))
            {
                string[] x = row.Split('|');
                if (x.Length != 4 || !int.TryParse(x[0], out int d) || d < 0 || d > 6)
                {
                    continue;
                }
                OpenDay o = new OpenDay { Day = d, Open = x[1] == "1", From = Clock.Hm(x[2]) != null ? Clock.Text(Clock.Hm(x[2])!.Value) : "08:00", To = Clock.Hm(x[3]) != null ? Clock.Text(Clock.Hm(x[3])!.Value) : "18:00" };
                if (o.Open && string.CompareOrdinal(o.From, o.To) >= 0 && err == string.Empty)
                {
                    err = Ui.DayNames[d] + ": closing time must be after opening time.";
                }
                hours.Add(o);
            }
            if (err != string.Empty)
            {
                response.SetElementContents("st-e-set", err);
                return response;
            }
            string accent = Ui.SafeColor(GetDataValue("accent")) ? GetDataValue("accent") : "#1F7A74";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                ClinicSettings s = site.Settings;
                s.Name = name;
                s.Tagline = Ui.Clip(GetDataValue("tag"), 120);
                s.Town = Ui.Clip(GetDataValue("town"), 40);
                s.Address = Ui.Clip(GetDataValue("addr"), 100);
                s.Phone = Ui.CleanPhone(GetDataValue("phone"));
                s.Email = email;
                s.Urgent = Ui.Clip(GetDataValue("urgent"), 200);
                s.Accent = accent;
                s.Theme = GetDataValue("theme") == "dark" ? "dark" : "light";
                s.ProviderWord = Word(GetDataValue("pw"), s.ProviderWord);
                s.ProvidersWord = Word(GetDataValue("pws"), s.ProvidersWord);
                s.RoomWord = Word(GetDataValue("rw"), s.RoomWord);
                s.RoomsWord = Word(GetDataValue("rws"), s.RoomsWord);
                s.PatientWord = Word(GetDataValue("aw"), s.PatientWord);
                s.PatientsWord = Word(GetDataValue("aws"), s.PatientsWord);
                if (hours.Count == 7)
                {
                    s.Hours = hours.OrderBy(h => h.Day).ToList();
                }
                int slot = Num(GetDataValue("slot"), 5, 30, s.SlotMinutes);
                s.SlotMinutes = slot is 5 or 10 or 15 or 20 or 30 ? slot : s.SlotMinutes;
                s.MinNoticeHours = Num(GetDataValue("notice"), 0, 72, s.MinNoticeHours);
                s.BookAheadDays = Num(GetDataValue("ahead"), 1, 365, s.BookAheadDays);
                s.CancelHours = Num(GetDataValue("cancel"), 0, 168, s.CancelHours);
                s.TimeZone = tz;
                s.ShowPrices = GetDataValue("prices") == "1";
                s.Clock24 = GetDataValue("h24") == "1";
                s.Insurance = Ui.SplitLines(GetDataValue("ins"), 20, 60).Distinct().ToList();
                DateTime today = Clock.Today(site);
                List<Holiday> past = site.Holidays.Where(h => h.Date.Date < today).ToList();
                foreach (string row in GetDataValue("hols").Split('~'))
                {
                    string[] x = row.Split('|');
                    DateTime? d = x.Length == 2 ? Ui.ParseDay(x[0]) : null;
                    if (d != null && d >= today && !past.Any(h => h.Date.Date == d))
                    {
                        past.Add(new Holiday { Date = d.Value, Name = Ui.Clip(x[1], 60) != string.Empty ? Ui.Clip(x[1], 60) : "Closed" });
                    }
                }
                site.Holidays = past.GroupBy(h => h.Date.Date).Select(g => g.First()).OrderBy(h => h.Date).Take(100).ToList();
                site.Faqs = GetDataValue("faqs").Split('~').Select(r => r.Split('|')).Where(x => x.Length == 2 && x[0].Trim() != string.Empty).Take(20).Select(x => new Faq { Q = Ui.Clip(x[0], 120), A = Ui.Clip(x[1], 500) }).ToList();
                ContentBlock home = Block(site, "home");
                ContentBlock about = Block(site, "about");
                ContentBlock policy = Block(site, "policy");
                home.Title = Ui.Clip(GetDataValue("htitle"), 80);
                home.Text = Ui.Clip(GetDataValue("htext"), 300);
                about.Title = Ui.Clip(GetDataValue("atitle"), 80);
                about.Text = Ui.Clip(GetDataValue("atext"), 600);
                policy.Text = Ui.Clip(GetDataValue("policy"), 400);
                site.Content["home"] = home;
                site.Content["about"] = about;
                site.Content["policy"] = policy;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "settings", name, true);
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');document.getElementById('st-site').style.setProperty('--st-accent','" + accent + "');");
            return response;
        }
    }
}
