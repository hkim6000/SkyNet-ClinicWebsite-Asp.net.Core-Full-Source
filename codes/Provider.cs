using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Provider : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("id"), 80), Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--pr-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "pr", "Provider", false))
                .Replace("{plhd_drawer}", Nav(site, "pr", "Provider", true))
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
                return "<i class=\"pr-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"pr-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"pr-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"pr-hrow" + (d == today ? " pr-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("ProviderJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("pr-main", v.Main);
            response.SetElementContents("pr-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("pr-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("pr-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("pr-today", TodayLine(site));
            response.SetElementContents("pr-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("pr-fhours", HoursRows(site));
            response.SetElementContents("pr-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("pr-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("pr-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ProviderJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ProviderJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"pr-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"pr-card\"><a class=\"pr-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"pr-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"pr-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"pr-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"pr-btn pr-btn-sm pr-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"pr-pcard\"><a href=\"" + href + "\">" + Ui.Face("pr", p) + "</a><div class=\"pr-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"pr-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            Clinician? p = site.Providers.FirstOrDefault(x => x.Id == a && x.Active);
            if (p == null)
            {
                return new View { Title = "Not found", Main = "<section class=\"pr-phead\"><div class=\"pr-wrap\"><h1>We couldn't find that page</h1><p class=\"pr-lead\"><a class=\"pr-more\" href=\"Providers\">Meet the team</a></p></div></section>" };
            }
            Engine e = new Engine(site);
            Specialty? sp = Ui.SpecialtyOf(site, p.Specialty);
            List<CareService> svs = site.Services.Where(s => s.Active && s.Providers.Contains(p.Id)).OrderBy(s => s.Order).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"pr-wrap\"><div class=\"pr-crumb pr-pcrumb\"><a href=\"Providers\">" + Ui.Enc(site.Settings.ProvidersWord) + "</a><span>/</span><span>" + Ui.Enc(p.Name) + "</span></div><section class=\"pr-prof\"><div class=\"pr-prof-img\">" + Ui.Face("pr", p) + "</div><div class=\"pr-prof-t\">" + SpecTag(site, p.Specialty) + "<h1>" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? "<small>, " + Ui.Enc(p.Credentials) + "</small>" : string.Empty) + "</h1><p class=\"pr-ptitle\">" + Ui.Enc(p.Title) + "</p><p class=\"pr-bio\">" + Ui.Enc(p.Bio) + "</p>");
            sb.Append("<dl class=\"pr-pfacts\">");
            if (p.Languages.Count > 0)
            {
                sb.Append("<div><dt>Languages</dt><dd>" + Ui.Enc(string.Join(", ", p.Languages)) + "</dd></div>");
            }
            if (p.Education.Count > 0)
            {
                sb.Append("<div><dt>Training</dt><dd>" + string.Join("<br>", p.Education.Select(Ui.Enc)) + "</dd></div>");
            }
            ClinicRoom? room = Ui.RoomOf(site, p.Room);
            if (room != null)
            {
                sb.Append("<div><dt>Where</dt><dd>" + Ui.Enc(room.Name) + "</dd></div>");
            }
            sb.Append("</dl>");
            TimeOff? away = p.TimeOff.Where(t => t.To.Date >= e.Today && t.From.Date <= e.Today.AddDays(30)).OrderBy(t => t.From).FirstOrDefault();
            if (away != null)
            {
                sb.Append("<p class=\"pr-info-box\">Away " + Ui.Day(away.From, false) + " to " + Ui.Day(away.To, false) + ". Book another time or another " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) + " in the same team.</p>");
            }
            sb.Append(p.Accepting ? "<div class=\"pr-hero-acts\"><a class=\"pr-btn pr-btn-accent\" href=\"Book?p=" + Uri.EscapeDataString(p.Id) + (svs.Count == 1 ? "&amp;sv=" + Uri.EscapeDataString(svs[0].Key) : string.Empty) + "\">Book with " + Ui.Enc(p.Name.StartsWith("Dr", StringComparison.Ordinal) ? "Dr. " + p.Name.Split(' ')[^1] : p.Name.Split(' ')[0]) + "</a></div>" : "<p class=\"pr-warn-box\">Not taking online bookings right now. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            sb.Append("</div></section><div class=\"pr-pgrid\"><section class=\"pr-box\"><h2>Services</h2><div class=\"pr-svlist\">");
            foreach (CareService sv in svs)
            {
                (DateTime Start, Clinician P)? n = sv.Online && p.Accepting ? e.Next(sv, p, 21) : null;
                sb.Append("<div class=\"pr-svrow\"><div><a href=\"Service?k=" + Uri.EscapeDataString(sv.Key) + "\"><b>" + Ui.Enc(sv.Name) + "</b></a><small>" + Ui.Dur(sv.Minutes) + (Price(site, sv) != string.Empty ? " &middot; " + Price(site, sv) : string.Empty) + " &middot; " + (sv.Online && p.Accepting ? "next " + NextText(site, n, e) : "book by phone") + "</small></div>" + (sv.Online && p.Accepting ? "<a class=\"pr-btn pr-btn-sm pr-btn-ghost\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "&amp;p=" + Uri.EscapeDataString(p.Id) + "\">Book</a>" : string.Empty) + "</div>");
            }
            sb.Append("</div></section><section class=\"pr-box\"><h2>In clinic</h2><div class=\"pr-week\">");
            int today = (int)e.Today.DayOfWeek;
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                WorkDay? w = p.Hours.FirstOrDefault(h => h.Day == d);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                bool on = w != null && w.On && o != null && o.Open;
                sb.Append("<div class=\"pr-wrow" + (d == today ? " pr-now" : string.Empty) + (on ? string.Empty : " pr-off") + "\"><span>" + Ui.DayNames[d] + "</span><span>" + (on ? Ui.HmText(site, w!.From) + " &ndash; " + Ui.HmText(site, w.To) : "Not in clinic") + "</span></div>");
            }
            sb.Append("</div><p class=\"pr-hint\">Times may change for leave and training days. Online booking always shows the real free times.</p></section></div></div>");
            return new View { Title = p.Name, Desc = p.Title + " at " + site.Settings.Name, Main = sb.ToString() };
        }
    }
}
