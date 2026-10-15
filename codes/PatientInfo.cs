using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class PatientInfo : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--pi-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "pi", "PatientInfo", false))
                .Replace("{plhd_drawer}", Nav(site, "pi", "PatientInfo", true))
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
                return "<i class=\"pi-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"pi-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"pi-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"pi-hrow" + (d == today ? " pi-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("PatientInfoJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("pi-main", v.Main);
            response.SetElementContents("pi-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("pi-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("pi-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("pi-today", TodayLine(site));
            response.SetElementContents("pi-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("pi-fhours", HoursRows(site));
            response.SetElementContents("pi-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("pi-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("pi-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("PatientInfoJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("PatientInfoJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"pi-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"pi-card\"><a class=\"pi-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"pi-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"pi-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"pi-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"pi-btn pi-btn-sm pi-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"pi-pcard\"><a href=\"" + href + "\">" + Ui.Face("pi", p) + "</a><div class=\"pi-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"pi-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            ContentBlock first = Block(site, "firstvisit");
            ContentBlock bring = Block(site, "bring");
            ContentBlock policy = Block(site, "policy");
            List<string> langs = site.Providers.Where(p => p.Active).SelectMany(p => p.Languages).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x == "English" ? 0 : 1).ThenBy(x => x).ToList();
            StringBuilder sb = new StringBuilder("<section class=\"pi-phead\"><div class=\"pi-wrap\"><span class=\"pi-kick\">" + Ui.Enc(site.Settings.PatientsWord) + "</span><h1>Before your visit</h1><p class=\"pi-lead\">What to expect, what to bring and answers to common questions.</p><nav class=\"pi-chips pi-jump\" aria-label=\"On this page\"><a class=\"pi-chip\" href=\"#first\">First visit</a><a class=\"pi-chip\" href=\"#bring\">What to bring</a><a class=\"pi-chip\" href=\"#insurance\">Insurance</a><a class=\"pi-chip\" href=\"#policy\">Changes &amp; cancellations</a><a class=\"pi-chip\" href=\"#faq\">Questions</a></nav></div></section>");
            if (site.Settings.Urgent != string.Empty)
            {
                sb.Append("<div class=\"pi-wrap\"><p class=\"pi-urg\"><b>Urgent?</b> " + Ui.Enc(site.Settings.Urgent) + "</p></div>");
            }
            sb.Append("<section class=\"pi-sec pi-anc\" id=\"first\"><div class=\"pi-wrap pi-fv\"><div>" + (first.Image != string.Empty ? Ui.Pic(first.Image, string.Empty, "pi-fvimg") : string.Empty) + "</div><div><h2 class=\"pi-h2\">" + Ui.Enc(first.Title) + "</h2><p class=\"pi-lead\">" + Ui.Enc(first.Text) + "</p><ol class=\"pi-steps\">");
            foreach (ContentItem it in first.Items)
            {
                sb.Append("<li><b>" + Ui.Enc(it.Name) + "</b><span>" + Ui.Enc(it.Text) + "</span></li>");
            }
            sb.Append("</ol></div></div></section><section class=\"pi-sec\"><div class=\"pi-wrap pi-grid2\"><div class=\"pi-box pi-anc\" id=\"bring\"><h2>" + Ui.Enc(bring.Title != string.Empty ? bring.Title : "What to bring") + "</h2><ul class=\"pi-ticks\">");
            foreach (ContentItem it in bring.Items)
            {
                sb.Append("<li>" + Ui.Enc(it.Name) + "</li>");
            }
            sb.Append("</ul></div><div class=\"pi-box pi-anc\" id=\"insurance\"><h2>Insurance</h2><p>We work with the plans below. If yours isn't listed, you are welcome as a self-pay " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + ", and we can give you a receipt to claim back.</p><div class=\"pi-ins\">");
            foreach (string ins in site.Settings.Insurance)
            {
                sb.Append("<span>" + Ui.Enc(ins) + "</span>");
            }
            sb.Append("</div></div><div class=\"pi-box pi-anc\" id=\"policy\"><h2>" + Ui.Enc(policy.Title != string.Empty ? policy.Title : "Changes & cancellations") + "</h2><p>" + Ui.Enc(policy.Text) + "</p><p class=\"pi-hint pi-mt\">Online changes close " + site.Settings.CancelHours + " hours before your visit. After that, please call " + Ui.Enc(site.Settings.Phone) + ".</p><a class=\"pi-btn pi-btn-ghost pi-btn-sm pi-mt\" href=\"MyAppointment\">Change a booking</a></div><div class=\"pi-box\"><h2>Languages we speak</h2><p>Our team can see you in:</p><div class=\"pi-ins\">");
            foreach (string l in langs)
            {
                sb.Append("<span>" + Ui.Enc(l) + "</span>");
            }
            sb.Append("</div><p class=\"pi-hint pi-mt\">Need an interpreter for another language? Tell us when you book and we will arrange one.</p></div></div></section>");
            sb.Append("<section class=\"pi-sec pi-anc\" id=\"faq\"><div class=\"pi-wrap pi-faqw\"><div><span class=\"pi-kick\">Questions</span><h2 class=\"pi-h2\">Good to know</h2><p class=\"pi-lead\">Can't find your answer? <a class=\"pi-more\" href=\"Contact\">Send us a message</a></p></div><div class=\"pi-faq\">");
            foreach (Faq f in site.Faqs)
            {
                sb.Append("<details><summary>" + Ui.Enc(f.Q) + "</summary><p>" + Ui.Enc(f.A) + "</p></details>");
            }
            sb.Append("</div></div></section>");
            return new View { Title = "Patient information", Desc = "First visits, what to bring, insurance and common questions at " + site.Settings.Name + ".", Main = sb.ToString() };
        }
    }
}
