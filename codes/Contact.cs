using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Contact : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            View v = Render(site, Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_town}", Ui.Enc(site.Settings.Town))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_addr}", Ui.Enc(site.Settings.Address))
                .Replace("{plhd_phone}", Ui.Enc(site.Settings.Phone))
                .Replace("{plhd_tel}", Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)))
                .Replace("{plhd_email}", Ui.Enc(site.Settings.Email))
                .Replace("{plhd_urgent}", Ui.Enc(site.Settings.Urgent))
                .Replace("{plhd_today}", TodayLine(site))
                .Replace("{plhd_hours}", HoursRows(site))
                .Replace("{plhd_providers}", Ui.Enc(site.Settings.ProvidersWord))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ct-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "ct", "Contact", false))
                .Replace("{plhd_drawer}", Nav(site, "ct", "Contact", true))
                .Replace("{plhd_main}", v.Main);
        }

        private static string Nav(SiteData site, string p, string page, bool drawer)
        {
            (string Page, string Name)[] links = { ("Services", "Services"), ("Providers", site.Settings.ProvidersWord), ("PatientInfo", "Patient info"), ("Contact", "Contact") };
            StringBuilder sb = new StringBuilder();
            if (drawer)
            {
                sb.Append("<a href=\"Home\">Home</a>");
            }
            foreach ((string k, string n) in links)
            {
                bool on = k == page || (k == "Services" && page == "Service") || (k == "Providers" && page == "Provider");
                sb.Append("<a href=\"" + k + "\"" + (on ? " class=\"" + p + "-on\"" : string.Empty) + ">" + Ui.Enc(n) + "</a>");
            }
            if (drawer)
            {
                sb.Append("<a href=\"MyAppointment\">My appointment</a>");
            }
            return sb.ToString();
        }

        private static string TodayLine(SiteData site)
        {
            Engine e = new Engine(site);
            DateTime now = e.Now;
            (TimeSpan From, TimeSpan To)? w = e.Clinic(now.Date);
            Holiday? h = e.HolidayOn(now.Date);
            if (w != null && now.TimeOfDay >= w.Value.From && now.TimeOfDay < w.Value.To)
            {
                return "<i></i>Open now &middot; until " + Ui.Hm(site, w.Value.To);
            }
            if (w != null && now.TimeOfDay < w.Value.From)
            {
                return "<i class=\"ct-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"ct-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"ct-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"ct-hrow" + (d == today ? " ct-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("ContactJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("ct-main", v.Main);
            response.SetElementContents("ct-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ct-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("ct-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ct-today", TodayLine(site));
            response.SetElementContents("ct-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("ct-fhours", HoursRows(site));
            response.SetElementContents("ct-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("ct-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("ct-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ContactJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
            return response;
        }

        private static async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Store.Demo)
            {
                return string.Empty;
            }
            string sbx = Store.Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = await Store.NewSandbox();
                response.ExecuteScript("ContactJs.sandboxed('" + sbx + "');");
            }
            return sbx;
        }

        private static string Readable(string requested)
        {
            return Store.Demo ? Store.Sandbox(requested) : string.Empty;
        }

        private static ContentBlock Block(SiteData site, string key)
        {
            return site.Content.TryGetValue(key, out ContentBlock? c) && c != null ? c : new ContentBlock();
        }

        private static string Price(SiteData site, CareService sv)
        {
            return site.Settings.ShowPrices && sv.Price > 0 ? "From " + Ui.Money(sv.Price) : string.Empty;
        }

        private static string SpecTag(SiteData site, string key)
        {
            Specialty? sp = Ui.SpecialtyOf(site, key);
            return sp == null ? string.Empty : "<span class=\"ct-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
        }

        private static string NextText(SiteData site, (DateTime Start, Clinician P)? n, Engine e)
        {
            if (n == null)
            {
                return "No online times in the next 4 weeks";
            }
            DateTime d = n.Value.Start.Date;
            string day = d == e.Today ? "Today" : d == e.Today.AddDays(1) ? "Tomorrow" : Ui.Day(d, false);
            return day + " at " + Ui.Hm(site, n.Value.Start);
        }

        private static string ServiceCard(SiteData site, CareService sv, Engine e)
        {
            string href = "Service?k=" + Uri.EscapeDataString(sv.Key);
            (DateTime Start, Clinician P)? n = sv.Online ? e.Next(sv, null, 14) : null;
            string price = Price(site, sv);
            return "<article class=\"ct-card\"><a class=\"ct-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"ct-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"ct-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"ct-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"ct-btn ct-btn-sm ct-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"ct-pcard\"><a href=\"" + href + "\">" + Ui.Face("ct", p) + "</a><div class=\"ct-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"ct-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            ContentBlock dir = Block(site, "directions");
            StringBuilder sb = new StringBuilder("<section class=\"ct-phead\"><div class=\"ct-wrap\"><span class=\"ct-kick\">Contact</span><h1>Visit or get in touch</h1><p class=\"ct-lead\">Call the front desk, send us a message or come and see us.</p></div></section>");
            sb.Append("<div class=\"ct-wrap ct-cgrid\"><div class=\"ct-cleft\"><div class=\"ct-map\">" + (dir.Image != string.Empty ? Ui.Pic(dir.Image, "The clinic building", string.Empty) : string.Empty) + "<div class=\"ct-pin\"><b>" + Ui.Enc(site.Settings.Name) + "</b><span>" + Ui.Enc(site.Settings.Address) + "</span></div></div>");
            sb.Append("<div class=\"ct-cboxes\"><div class=\"ct-box\"><h2>Call or email</h2><a class=\"ct-big\" href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a><a class=\"ct-more\" href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a><p class=\"ct-hint ct-mt\">The front desk answers during opening hours.</p></div><div class=\"ct-box\"><h2>Opening hours</h2><div class=\"ct-hours\">" + HoursRows(site) + "</div></div></div>");
            if (dir.Text != string.Empty || dir.Items.Count > 0)
            {
                sb.Append("<div class=\"ct-box\"><h2>" + Ui.Enc(dir.Title != string.Empty ? dir.Title : "Getting here") + "</h2>" + (dir.Text != string.Empty ? "<p>" + Ui.Enc(dir.Text) + "</p>" : string.Empty) + "<dl class=\"ct-ways\">");
                foreach (ContentItem it in dir.Items)
                {
                    sb.Append("<div><dt>" + Ui.Enc(it.Name) + "</dt><dd>" + Ui.Enc(it.Text) + "</dd></div>");
                }
                sb.Append("</dl></div>");
            }
            sb.Append("</div><form class=\"ct-box ct-msg\" id=\"ct-msg\" onsubmit=\"return ContactJs.send()\" novalidate><h2>Send a message</h2><p class=\"ct-hint\">For booking questions, records, insurance or feedback. We reply within one working day.</p><label class=\"ct-field\"><span>Your name</span><input type=\"text\" id=\"ct-name\" maxlength=\"80\" autocomplete=\"name\"><em class=\"ct-err\" id=\"ct-e-name\"></em></label><div class=\"ct-row2\"><label class=\"ct-field\"><span>Email</span><input type=\"email\" id=\"ct-email\" maxlength=\"120\" autocomplete=\"email\"><em class=\"ct-err\" id=\"ct-e-email\"></em></label><label class=\"ct-field\"><span>Phone <i>(optional)</i></span><input type=\"tel\" id=\"ct-phone\" maxlength=\"30\" autocomplete=\"tel\"></label></div><label class=\"ct-field\"><span>About</span><select id=\"ct-topic\">" + Ui.Opts(new[] { ("Booking", "A booking"), ("Records", "Records and letters"), ("Insurance", "Insurance and payments"), ("Feedback", "Feedback"), ("Other", "Something else") }, "Booking") + "</select></label><label class=\"ct-field\"><span>Message</span><textarea id=\"ct-text\" rows=\"6\" maxlength=\"1000\"></textarea><em class=\"ct-err\" id=\"ct-e-text\"></em></label><p class=\"ct-note\">Please don't send medical details by message. If you feel unwell, call us or book a visit.</p><em class=\"ct-err\" id=\"ct-e-form\" role=\"alert\"></em><button type=\"submit\" class=\"ct-btn ct-btn-accent\">Send message</button></form></div>");
            return new View { Title = "Contact", Desc = "Opening hours, address and contact details for " + site.Settings.Name + ".", Main = sb.ToString() };
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string name = Ui.Clip(GetDataValue("name"), 80);
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string topic = GetDataValue("topic") is "Booking" or "Records" or "Insurance" or "Feedback" ? GetDataValue("topic") : "Other";
            string text = Ui.Clip(GetDataValue("text"), 1000);
            bool bad = false;
            if (name == string.Empty)
            {
                response.SetElementContents("ct-e-name", "Enter your name.");
                bad = true;
            }
            if (!Ui.Email(email))
            {
                response.SetElementContents("ct-e-email", "Enter a valid email address so we can reply.");
                bad = true;
            }
            if (text.Length < 5)
            {
                response.SetElementContents("ct-e-text", "Write a short message.");
                bad = true;
            }
            if (bad)
            {
                return response;
            }
            if (Auth.Locked("msg:" + email))
            {
                response.SetElementContents("ct-e-form", "You have sent several messages already. Please wait a few minutes or call us.");
                return response;
            }
            Auth.Fail("msg:" + email);
            string sbx = await Writable(response, GetDataValue("sbx"));
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                site.Messages.Insert(0, new Message { Id = Codes.Id("m"), Name = name, Email = email, Phone = phone, Topic = topic, Text = text, When = DateTime.UtcNow });
                if (site.Messages.Count > 500)
                {
                    site.Messages.RemoveRange(500, site.Messages.Count - 500);
                }
                await Store.SaveSite(site, sbx);
            }
            await Store.Log(sbx, "web", "message", topic + " from " + name, true);
            response.SetElementContents("ct-msg", "<div class=\"ct-sent\"><div class=\"ct-tick\" aria-hidden=\"true\"></div><h2>Thanks, " + Ui.Enc(name.Split(' ')[0]) + "</h2><p>Your message is with the front desk. We reply within one working day.</p><a class=\"ct-btn ct-btn-ghost ct-btn-sm\" href=\"Contact\">Send another message</a></div>");
            return response;
        }
    }
}
