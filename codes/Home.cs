using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Home : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "hm", "Home", false))
                .Replace("{plhd_drawer}", Nav(site, "hm", "Home", true))
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
                return "<i class=\"hm-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"hm-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"hm-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"hm-hrow" + (d == today ? " hm-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("HomeJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("hm-main", v.Main);
            response.SetElementContents("hm-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("hm-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("hm-today", TodayLine(site));
            response.SetElementContents("hm-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("hm-fhours", HoursRows(site));
            response.SetElementContents("hm-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("hm-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("hm-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("HomeJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("HomeJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"hm-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"hm-card\"><a class=\"hm-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"hm-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"hm-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"hm-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"hm-btn hm-btn-sm hm-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"hm-pcard\"><a href=\"" + href + "\">" + Ui.Face("hm", p) + "</a><div class=\"hm-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"hm-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            ContentBlock home = Block(site, "home");
            ContentBlock about = Block(site, "about");
            ContentBlock first = Block(site, "firstvisit");
            Engine e = new Engine(site);
            List<Specialty> specs = site.Specialties.Where(x => x.Active).OrderBy(x => x.Order).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"hm-hero\">" + Ui.Pic(home.Image, string.Empty, "hm-hero-img", false, true) + "<div class=\"hm-wrap hm-hero-in\"><div class=\"hm-hero-t\"><span class=\"hm-kick\">" + Ui.Enc(site.Settings.Name) + "</span><h1>" + Ui.Enc(home.Title) + "</h1><p>" + Ui.Enc(home.Text) + "</p><div class=\"hm-hero-acts\"><a class=\"hm-btn hm-btn-accent\" href=\"Book\">Book an appointment</a><a class=\"hm-btn hm-btn-ghost hm-btn-light\" href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">Call " + Ui.Enc(site.Settings.Phone) + "</a></div></div>");
            sb.Append("<form class=\"hm-quick\" action=\"Book\" method=\"get\" aria-label=\"Find a time\"><h2>Find a time</h2><label class=\"hm-field\"><span>What do you need?</span><select name=\"sv\" id=\"hm-qsv\" required><option value=\"\">Choose a service</option>");
            foreach (Specialty sp in specs)
            {
                sb.Append("<optgroup label=\"" + Ui.Attr(sp.Name) + "\">");
                foreach (CareService sv in site.Services.Where(x => x.Active && x.Online && x.Specialty == sp.Key).OrderBy(x => x.Order))
                {
                    sb.Append("<option value=\"" + Ui.Attr(sv.Key) + "\">" + Ui.Enc(sv.Name) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            sb.Append("</select></label><button type=\"submit\" class=\"hm-btn hm-btn-accent hm-btn-wide\">See available times</button><div class=\"hm-soon\"><b>Next available</b>");
            foreach (Specialty sp in specs)
            {
                (DateTime Start, Clinician P)? best = null;
                foreach (CareService sv in site.Services.Where(x => x.Active && x.Online && x.Specialty == sp.Key))
                {
                    (DateTime Start, Clinician P)? n = e.Next(sv, null, 7);
                    if (n != null && (best == null || n.Value.Start < best.Value.Start))
                    {
                        best = n;
                    }
                }
                sb.Append("<a href=\"Services?s=" + Uri.EscapeDataString(sp.Key) + "\"><span><i class=\"hm-dot\" style=\"background:" + Ui.Attr(sp.Color) + "\"></i>" + Ui.Enc(sp.Name) + "</span><em>" + NextText(site, best, e) + "</em></a>");
            }
            sb.Append("</div></form></div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Our care</span><h2 class=\"hm-h2\">Four teams, one front desk</h2></div><a class=\"hm-more\" href=\"Services\">All services &rarr;</a></div><div class=\"hm-specs\">");
            foreach (Specialty sp in specs)
            {
                int n = site.Services.Count(x => x.Active && x.Specialty == sp.Key);
                int pn = site.Providers.Count(x => x.Active && x.Specialty == sp.Key);
                sb.Append("<a class=\"hm-spec\" href=\"Services?s=" + Uri.EscapeDataString(sp.Key) + "\" style=\"--c:" + Ui.Attr(sp.Color) + "\">" + Ui.Pic(sp.Image, string.Empty, string.Empty, true) + "<span><b>" + Ui.Enc(sp.Name) + "</b><small>" + Ui.Enc(sp.Blurb) + "</small><em>" + n + (n == 1 ? " service" : " services") + " &middot; " + pn + " " + Ui.Enc(pn == 1 ? site.Settings.ProviderWord.ToLowerInvariant() : site.Settings.ProvidersWord.ToLowerInvariant()) + "</em></span></a>");
            }
            sb.Append("</div></div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-about\">" + (about.Image != string.Empty ? "<div class=\"hm-about-img\">" + Ui.Pic(about.Image, string.Empty, string.Empty) + "</div>" : string.Empty) + "<div><span class=\"hm-kick\">About us</span><h2 class=\"hm-h2\">" + Ui.Enc(about.Title) + "</h2><p class=\"hm-lead\">" + Ui.Enc(about.Text) + "</p><div class=\"hm-stats\">");
            foreach (ContentItem it in about.Items)
            {
                sb.Append("<div><b>" + Ui.Enc(it.Name) + "</b><span>" + Ui.Enc(it.Text) + "</span></div>");
            }
            sb.Append("</div></div></div></section>");
            List<Clinician> ps = site.Providers.Where(x => x.Active).OrderBy(x => x.Order).ToList();
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">" + Ui.Enc(site.Settings.ProvidersWord) + "</span><h2 class=\"hm-h2\">Meet the team</h2></div><a class=\"hm-more\" href=\"Providers\">All " + Ui.Enc(site.Settings.ProvidersWord.ToLowerInvariant()) + " &rarr;</a></div><div class=\"hm-team\">");
            foreach (Clinician p in ps.Take(4))
            {
                sb.Append(ProviderCard(site, p, e));
            }
            sb.Append("</div></div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-steps-box\"><div><span class=\"hm-kick\">New here?</span><h2 class=\"hm-h2\">" + Ui.Enc(first.Title) + "</h2><p class=\"hm-lead\">" + Ui.Enc(first.Text) + "</p><a class=\"hm-more\" href=\"PatientInfo\">Patient information &rarr;</a></div><ol class=\"hm-steps\">");
            foreach (ContentItem it in first.Items)
            {
                sb.Append("<li><b>" + Ui.Enc(it.Name) + "</b><span>" + Ui.Enc(it.Text) + "</span></li>");
            }
            sb.Append("</ol></div></div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-visit\"><div class=\"hm-box\"><h2>Opening hours</h2><div class=\"hm-hours\">" + HoursRows(site) + "</div>");
            Holiday? hol = site.Holidays.Where(h => h.Date.Date >= e.Today).OrderBy(h => h.Date).FirstOrDefault();
            if (hol != null && hol.Date.Date <= e.Today.AddDays(30))
            {
                sb.Append("<p class=\"hm-info-box\">Closed " + Ui.LongDay(hol.Date) + " &middot; " + Ui.Enc(hol.Name) + "</p>");
            }
            sb.Append("</div><div class=\"hm-box\"><h2>Find us</h2><p class=\"hm-addr\">" + Ui.Enc(site.Settings.Address) + "</p><p><a class=\"hm-more\" href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a></p><p><a class=\"hm-more\" href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a></p><a class=\"hm-btn hm-btn-ghost hm-btn-sm\" href=\"Contact\">Directions &amp; parking</a></div><div class=\"hm-box\"><h2>Insurance</h2><p class=\"hm-hint\">We work with these plans. Self-pay patients are always welcome.</p><div class=\"hm-ins\">");
            foreach (string ins in site.Settings.Insurance)
            {
                sb.Append("<span>" + Ui.Enc(ins) + "</span>");
            }
            sb.Append("</div></div></div></section>");
            return new View { Desc = site.Settings.Tagline, Main = sb.ToString() };
        }
    }
}
