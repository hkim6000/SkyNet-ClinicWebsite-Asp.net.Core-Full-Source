using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Service : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("k"), 80), Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--se-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "se", "Service", false))
                .Replace("{plhd_drawer}", Nav(site, "se", "Service", true))
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
                return "<i class=\"se-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"se-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"se-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"se-hrow" + (d == today ? " se-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("ServiceJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("se-main", v.Main);
            response.SetElementContents("se-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("se-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("se-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("se-today", TodayLine(site));
            response.SetElementContents("se-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("se-fhours", HoursRows(site));
            response.SetElementContents("se-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("se-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("se-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ServiceJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ServiceJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"se-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"se-card\"><a class=\"se-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"se-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"se-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"se-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"se-btn se-btn-sm se-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"se-pcard\"><a href=\"" + href + "\">" + Ui.Face("se", p) + "</a><div class=\"se-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"se-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            CareService? sv = site.Services.FirstOrDefault(x => x.Key == a && x.Active);
            if (sv == null)
            {
                return new View { Title = "Service not found", Main = "<section class=\"se-phead\"><div class=\"se-wrap\"><h1>Service not found</h1><p class=\"se-lead\">This service may have moved. <a class=\"se-more\" href=\"Services\">See all services</a></p></div></section>" };
            }
            Engine e = new Engine(site);
            Specialty? sp = Ui.SpecialtyOf(site, sv.Specialty);
            StringBuilder sb = new StringBuilder("<section class=\"se-phero\">" + Ui.Pic(sv.Image, string.Empty, string.Empty, false, true) + "<div class=\"se-wrap\"><div class=\"se-crumb\"><a href=\"Services\">Services</a><span>/</span>" + (sp != null ? "<a href=\"Services?s=" + Uri.EscapeDataString(sp.Key) + "\">" + Ui.Enc(sp.Name) + "</a>" : string.Empty) + "</div><h1>" + Ui.Enc(sv.Name) + "</h1><p>" + Ui.Enc(sv.Short) + "</p></div></section>");
            sb.Append("<div class=\"se-wrap se-svgrid\"><div class=\"se-svmain\"><p class=\"se-svtext\">" + Ui.Enc(sv.Text) + "</p>");
            if (sv.Expect.Count > 0)
            {
                sb.Append("<h2 class=\"se-h3\">What to expect</h2><ul class=\"se-ticks\">");
                foreach (string x in sv.Expect)
                {
                    sb.Append("<li>" + Ui.Enc(x) + "</li>");
                }
                sb.Append("</ul>");
            }
            List<Clinician> ps = site.Providers.Where(p => p.Active && sv.Providers.Contains(p.Id)).OrderBy(p => p.Order).ToList();
            if (ps.Count > 0)
            {
                sb.Append("<h2 class=\"se-h3\">Who you can see</h2><div class=\"se-who\">");
                foreach (Clinician p in ps)
                {
                    sb.Append("<a class=\"se-whoi\" href=\"Provider?id=" + Uri.EscapeDataString(p.Id) + "\">" + Ui.Face("se", p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + "</small></span></a>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div><aside class=\"se-box se-svside\"><dl class=\"se-svfacts\"><div><dt>Length</dt><dd>" + Ui.Dur(sv.Minutes) + "</dd></div>" + (Price(site, sv) != string.Empty ? "<div><dt>Price</dt><dd>" + Price(site, sv) + "</dd></div>" : string.Empty) + "<div><dt>New patients</dt><dd>" + (sv.NewOk ? "Welcome" : "Existing patients only") + "</dd></div></dl>");
            if (sv.Online)
            {
                sb.Append("<h3>Next available</h3>");
                int shown = 0;
                for (int i = 0; i < 21 && shown < 2; i++)
                {
                    DateTime d = e.Today.AddDays(i);
                    List<(DateTime Start, Clinician P)> slots = e.Slots(sv, null, d, true);
                    if (slots.Count == 0)
                    {
                        continue;
                    }
                    shown++;
                    sb.Append("<p class=\"se-sday\">" + (d == e.Today ? "Today" : d == e.Today.AddDays(1) ? "Tomorrow" : Ui.Day(d, false)) + "</p><div class=\"se-slots\">");
                    foreach ((DateTime t, Clinician p) in slots.Take(6))
                    {
                        sb.Append("<a class=\"se-slot\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "&amp;p=" + Uri.EscapeDataString(p.Id) + "&amp;t=" + Ui.Stamp(t) + "\" title=\"" + Ui.Attr(p.Name) + "\">" + Ui.Hm(site, t) + "</a>");
                    }
                    sb.Append("</div>");
                }
                if (shown == 0)
                {
                    sb.Append("<p class=\"se-hint\">No online times in the next three weeks. Please call us.</p>");
                }
                sb.Append("<a class=\"se-btn se-btn-accent se-btn-wide\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">See all times</a>");
            }
            else
            {
                sb.Append("<p class=\"se-info-box\">Please call " + Ui.Enc(site.Settings.Phone) + " to book this service.</p>");
            }
            sb.Append("</aside></div>");
            List<CareService> more = site.Services.Where(x => x.Active && x.Specialty == sv.Specialty && x.Key != sv.Key).OrderBy(x => x.Order).Take(3).ToList();
            if (more.Count > 0)
            {
                sb.Append("<section class=\"se-sec\"><div class=\"se-wrap\"><div class=\"se-shead\"><h2 class=\"se-h2\">More in " + Ui.Enc(sp?.Name ?? "this team") + "</h2></div><div class=\"se-cards\">");
                foreach (CareService m in more)
                {
                    sb.Append(ServiceCard(site, m, e));
                }
                sb.Append("</div></div></section>");
            }
            return new View { Title = sv.Name, Desc = sv.Short, Main = sb.ToString() };
        }
    }
}
