using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Services : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("s"), 80), Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--sv-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "sv", "Services", false))
                .Replace("{plhd_drawer}", Nav(site, "sv", "Services", true))
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
                return "<i class=\"sv-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"sv-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"sv-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"sv-hrow" + (d == today ? " sv-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("ServicesJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("sv-main", v.Main);
            response.SetElementContents("sv-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sv-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("sv-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("sv-today", TodayLine(site));
            response.SetElementContents("sv-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("sv-fhours", HoursRows(site));
            response.SetElementContents("sv-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("sv-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("sv-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ServicesJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ServicesJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"sv-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"sv-card\"><a class=\"sv-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"sv-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"sv-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"sv-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"sv-btn sv-btn-sm sv-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"sv-pcard\"><a href=\"" + href + "\">" + Ui.Face("sv", p) + "</a><div class=\"sv-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"sv-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            Engine e = new Engine(site);
            List<Specialty> specs = site.Specialties.Where(x => x.Active).OrderBy(x => x.Order).ToList();
            Specialty? only = specs.FirstOrDefault(x => x.Key == a);
            StringBuilder sb = new StringBuilder("<section class=\"sv-phead\"><div class=\"sv-wrap\"><span class=\"sv-kick\">Services</span><h1>" + (only != null ? Ui.Enc(only.Name) : "What we can help with") + "</h1><p class=\"sv-lead\">" + (only != null ? Ui.Enc(only.Blurb) : "Every service shows how long it takes, the price and the next time you can book online.") + "</p><div class=\"sv-chips sv-filter\"><a class=\"sv-chip" + (only == null ? " sv-act" : string.Empty) + "\" href=\"Services\">All</a>");
            foreach (Specialty sp in specs)
            {
                sb.Append("<a class=\"sv-chip" + (only == sp ? " sv-act" : string.Empty) + "\" href=\"Services?s=" + Uri.EscapeDataString(sp.Key) + "\"><i style=\"background:" + Ui.Attr(sp.Color) + "\"></i>" + Ui.Enc(sp.Name) + "</a>");
            }
            sb.Append("</div></div></section>");
            foreach (Specialty sp in specs.Where(x => only == null || x == only))
            {
                List<CareService> list = site.Services.Where(x => x.Active && x.Specialty == sp.Key).OrderBy(x => x.Order).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<section class=\"sv-sec sv-group\"><div class=\"sv-wrap\"><div class=\"sv-shead\"><div><h2 class=\"sv-h2\"><i class=\"sv-dot sv-bigdot\" style=\"background:" + Ui.Attr(sp.Color) + "\"></i>" + Ui.Enc(sp.Name) + "</h2>" + (only == null ? "<p class=\"sv-lead\">" + Ui.Enc(sp.Blurb) + "</p>" : string.Empty) + "</div><a class=\"sv-more\" href=\"Providers?s=" + Uri.EscapeDataString(sp.Key) + "\">Our " + Ui.Enc(site.Settings.ProvidersWord.ToLowerInvariant()) + " &rarr;</a></div><div class=\"sv-cards\">");
                foreach (CareService sv in list)
                {
                    sb.Append(ServiceCard(site, sv, e));
                }
                sb.Append("</div></div></section>");
            }
            return new View { Title = only != null ? only.Name : "Services", Desc = only != null ? only.Blurb : "Services at " + site.Settings.Name, Main = sb.ToString() };
        }
    }
}
