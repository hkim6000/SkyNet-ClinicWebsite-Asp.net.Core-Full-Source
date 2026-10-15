using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class Book : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("sv"), 80), Ui.Clip(QueryValue("p"), 80), Ui.Clip(QueryValue("t"), 80));
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--bk-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "bk", "Book", false))
                .Replace("{plhd_drawer}", Nav(site, "bk", "Book", true))
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
                return "<i class=\"bk-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"bk-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"bk-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"bk-hrow" + (d == today ? " bk-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("BookJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("bk-main", v.Main);
            response.SetElementContents("bk-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("bk-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("bk-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("bk-today", TodayLine(site));
            response.SetElementContents("bk-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("bk-fhours", HoursRows(site));
            response.SetElementContents("bk-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("bk-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("bk-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("BookJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("BookJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"bk-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"bk-card\"><a class=\"bk-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"bk-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"bk-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"bk-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"bk-btn bk-btn-sm bk-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"bk-pcard\"><a href=\"" + href + "\">" + Ui.Face("bk", p) + "</a><div class=\"bk-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"bk-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private sealed class St
        {
            public CareService? Sv { get; set; }
            public string P { get; set; } = string.Empty;
            public Clinician? Pa { get; set; }
            public Clinician? Pref { get; set; }
            public DateTime? T { get; set; }
            public DateTime Wk { get; set; }
            public DateTime Day { get; set; }
            public bool Single { get; set; }
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            Engine e = new Engine(site);
            St st = Norm(site, e, a, b, string.Empty, c, string.Empty, string.Empty, b);
            return new View { Title = "Book an appointment", Desc = "Book a visit at " + site.Settings.Name + " online in under a minute.", Main = "<section class=\"bk-phead\"><div class=\"bk-wrap\"><span class=\"bk-kick\">Online booking</span><h1>Book an appointment</h1><p class=\"bk-lead\">Pick a service, a " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) + " and a time. No account needed.</p></div></section><div class=\"bk-wrap bk-bgrid\"><div id=\"bk-flow\" class=\"bk-flow\">" + Flow(site, e, st) + "</div><aside class=\"bk-box bk-sum\" id=\"bk-sum\">" + Summary(site, e, st) + "</aside></div>" };
        }

        private static St Norm(SiteData site, Engine e, string sv, string p, string pa, string t, string day, string wk, string pref)
        {
            St st = new St();
            st.Sv = site.Services.FirstOrDefault(x => x.Key == sv && x.Active && x.Online);
            st.Pref = site.Providers.FirstOrDefault(x => x.Id == pref && x.Active && x.Accepting);
            Clinician? pp = site.Providers.FirstOrDefault(x => x.Id == p && x.Active && x.Accepting);
            if (st.Sv != null)
            {
                List<Clinician> ps = e.For(st.Sv, true);
                if (ps.Count == 1)
                {
                    st.P = ps[0].Id;
                    st.Single = true;
                }
                else if (p == "any" && ps.Count > 0)
                {
                    st.P = "any";
                }
                else if (pp != null && ps.Contains(pp))
                {
                    st.P = pp.Id;
                }
            }
            DateTime? when = Ui.ParseStart(t);
            if (st.Sv != null && st.P != string.Empty && when != null)
            {
                Clinician? who = st.P == "any" ? site.Providers.FirstOrDefault(x => x.Id == pa) : Ui.ProviderOf(site, st.P);
                if (who != null && e.Fits(who, st.Sv, when.Value, st.Sv.Minutes, true, string.Empty))
                {
                    st.T = when;
                    st.Pa = who;
                }
            }
            DateTime w = Ui.ParseDay(wk) ?? (when?.Date ?? e.Today);
            DateTime last = e.Last(true);
            st.Wk = w < e.Today ? e.Today : w > last ? last : w;
            DateTime? d = Ui.ParseDay(day);
            if (d == null || d < st.Wk || d > st.Wk.AddDays(6))
            {
                d = null;
            }
            if (d == null && st.Sv != null && st.P != string.Empty)
            {
                Clinician? only = st.P == "any" ? null : Ui.ProviderOf(site, st.P);
                for (int i = 0; i < 7 && d == null; i++)
                {
                    if (e.Slots(st.Sv, only, st.Wk.AddDays(i), true).Count > 0)
                    {
                        d = st.Wk.AddDays(i);
                    }
                }
            }
            st.Day = d ?? st.Wk;
            return st;
        }

        private static string Done(int n, string label, string body, string change)
        {
            return "<section class=\"bk-stp bk-done\"><span class=\"bk-n\" aria-hidden=\"true\"></span><div class=\"bk-stb\"><small>" + Ui.Enc(label) + "</small><div class=\"bk-dv\">" + body + "</div></div>" + (change != string.Empty ? "<button type=\"button\" class=\"bk-chg\" onclick=\"BookJs.go('" + change + "')\">Change</button>" : string.Empty) + "</section>";
        }

        private static string Open(int n, string title, string body)
        {
            return "<section class=\"bk-stp bk-open\" id=\"bk-cur\"><span class=\"bk-n\">" + n + "</span><div class=\"bk-stb\"><h2>" + Ui.Enc(title) + "</h2>" + body + "</div></section>";
        }

        private static string Locked(int n, string title)
        {
            return "<section class=\"bk-stp bk-lock\"><span class=\"bk-n\">" + n + "</span><div class=\"bk-stb\"><h2>" + Ui.Enc(title) + "</h2></div></section>";
        }

        private static string Flow(SiteData site, Engine e, St st)
        {
            StringBuilder sb = new StringBuilder("<i id=\"bk-st\" hidden data-sv=\"" + Ui.Attr(st.Sv?.Key ?? string.Empty) + "\" data-p=\"" + Ui.Attr(st.P) + "\" data-pa=\"" + Ui.Attr(st.Pa?.Id ?? string.Empty) + "\" data-t=\"" + (st.T != null ? Ui.Stamp(st.T.Value) : string.Empty) + "\" data-wk=\"" + Ui.Ymd(st.Wk) + "\" data-day=\"" + Ui.Ymd(st.Day) + "\" data-pref=\"" + Ui.Attr(st.Pref?.Id ?? string.Empty) + "\"></i>");
            string word = site.Settings.ProviderWord;
            if (st.Sv != null)
            {
                sb.Append(Done(1, "Service", "<b>" + Ui.Enc(st.Sv.Name) + "</b><span>" + Ui.Dur(st.Sv.Minutes) + " &middot; " + Ui.Enc(Ui.SpecialtyOf(site, st.Sv.Specialty)?.Name ?? string.Empty) + "</span>", "sv"));
            }
            else
            {
                sb.Append(Open(1, "What do you need?", ServicePick(site, st)));
            }
            if (st.Sv == null)
            {
                sb.Append(Locked(2, "Who you will see"));
            }
            else if (st.P != string.Empty)
            {
                Clinician? p = Ui.ProviderOf(site, st.P);
                sb.Append(Done(2, word, st.P == "any" ? "<b>First available</b><span>We pick whoever is free at the time you choose</span>" : "<span class=\"bk-dp\">" + Ui.Face("bk", p) + "<b>" + Ui.Enc(p?.Name ?? string.Empty) + "</b></span>", st.Single ? string.Empty : "p"));
            }
            else
            {
                sb.Append(Open(2, "Who would you like to see?", ProviderPick(site, e, st)));
            }
            if (st.Sv == null || st.P == string.Empty)
            {
                sb.Append(Locked(3, "Date and time"));
            }
            else if (st.T != null)
            {
                sb.Append(Done(3, "Date and time", "<b>" + Ui.LongDay(st.T.Value) + "</b><span>" + Ui.Hm(site, st.T.Value) + " &ndash; " + Ui.Hm(site, st.T.Value.AddMinutes(st.Sv.Minutes)) + (st.P == "any" && st.Pa != null ? " &middot; with " + Ui.Enc(st.Pa.Name) : string.Empty) + "</span>", "t"));
            }
            else
            {
                sb.Append(Open(3, "Choose a time", Strip(site, e, st.Sv, st.P == "any" ? null : Ui.ProviderOf(site, st.P), st.Wk, st.Day, string.Empty)));
            }
            if (st.T == null)
            {
                sb.Append(Locked(4, "Your details"));
            }
            else
            {
                sb.Append(Open(4, "Your details", Form(site, st)));
            }
            return sb.ToString();
        }

        private static string ServicePick(SiteData site, St st)
        {
            List<CareService> list = site.Services.Where(x => x.Active && x.Online && (st.Pref == null || x.Providers.Contains(st.Pref.Id)) && site.Providers.Any(p => p.Active && p.Accepting && x.Providers.Contains(p.Id))).OrderBy(x => x.Order).ToList();
            List<Specialty> specs = site.Specialties.Where(s => s.Active && list.Any(x => x.Specialty == s.Key)).OrderBy(s => s.Order).ToList();
            StringBuilder sb = new StringBuilder();
            if (st.Pref != null)
            {
                sb.Append("<p class=\"bk-info-box bk-pref\">Showing services with " + Ui.Enc(st.Pref.Name) + ". <a href=\"Book\">Show every service</a></p>");
            }
            if (specs.Count > 1)
            {
                sb.Append("<div class=\"bk-chips bk-spf\"><button type=\"button\" class=\"bk-chip bk-act\" onclick=\"BookJs.filter(this,'')\">All</button>");
                foreach (Specialty s in specs)
                {
                    sb.Append("<button type=\"button\" class=\"bk-chip\" onclick=\"BookJs.filter(this,'" + Ui.Js(s.Key) + "')\"><i style=\"background:" + Ui.Attr(s.Color) + "\"></i>" + Ui.Enc(s.Name) + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"bk-opts\">");
            foreach (CareService sv in list)
            {
                Specialty? sp = Ui.SpecialtyOf(site, sv.Specialty);
                string price = Price(site, sv);
                sb.Append("<button type=\"button\" class=\"bk-opt\" data-sp=\"" + Ui.Attr(sv.Specialty) + "\" style=\"--c:" + Ui.Attr(sp?.Color ?? "#1F7A74") + "\" onclick=\"BookJs.pickService('" + Ui.Js(sv.Key) + "')\"><span><b>" + Ui.Enc(sv.Name) + "</b><small>" + Ui.Enc(sv.Short) + "</small></span><em>" + Ui.Dur(sv.Minutes) + (price != string.Empty ? "<br>" + price : string.Empty) + "</em></button>");
            }
            sb.Append("</div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bk-none\">Nothing can be booked online right now. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            return sb.ToString();
        }

        private static string ProviderPick(SiteData site, Engine e, St st)
        {
            CareService sv = st.Sv!;
            List<Clinician> ps = e.For(sv, true);
            StringBuilder sb = new StringBuilder("<div class=\"bk-popts\">");
            (DateTime Start, Clinician P)? any = e.Next(sv, null, 28);
            sb.Append("<button type=\"button\" class=\"bk-popt bk-any\" onclick=\"BookJs.pickProvider('any')\"><span class=\"bk-anyic\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/></svg></span><span><b>First available</b><small>Any of our " + ps.Count + " " + Ui.Enc(site.Settings.ProvidersWord.ToLowerInvariant()) + " for this service</small><em>" + NextText(site, any, e) + "</em></span></button>");
            foreach (Clinician p in ps)
            {
                (DateTime Start, Clinician P)? n = e.Next(sv, p, 28);
                sb.Append("<button type=\"button\" class=\"bk-popt\" onclick=\"BookJs.pickProvider('" + Ui.Js(p.Id) + "')\">" + Ui.Face("bk", p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + (p.Languages.Count > 1 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</small><em>" + NextText(site, n, e) + "</em></span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Form(SiteData site, St st)
        {
            Specialty? sp = Ui.SpecialtyOf(site, st.Sv!.Specialty);
            List<Question> qs = sp?.Questions ?? new List<Question>();
            StringBuilder sb = new StringBuilder("<form class=\"bk-form bk-dform\" id=\"bk-dform\" data-qn=\"" + qs.Count + "\" onsubmit=\"return BookJs.confirm()\" novalidate>");
            sb.Append("<div class=\"bk-seg\" role=\"radiogroup\" aria-label=\"Have you been here before?\"><label><input type=\"radio\" name=\"bk-new\" value=\"back\" checked><span>I have been here before</span></label><label><input type=\"radio\" name=\"bk-new\" value=\"new\"" + (st.Sv.NewOk ? string.Empty : " disabled") + "><span>I am a new " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</span></label></div>");
            if (!st.Sv.NewOk)
            {
                sb.Append("<p class=\"bk-hint\">This service is for existing " + Ui.Enc(site.Settings.PatientsWord.ToLowerInvariant()) + ". New here? Book a first visit instead.</p>");
            }
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>First name</span><input type=\"text\" id=\"bk-first\" maxlength=\"40\" autocomplete=\"given-name\"><em class=\"bk-err\" id=\"bk-e-first\"></em></label><label class=\"bk-field\"><span>Last name</span><input type=\"text\" id=\"bk-last\" maxlength=\"40\" autocomplete=\"family-name\"><em class=\"bk-err\" id=\"bk-e-last\"></em></label></div>");
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>Mobile phone</span><input type=\"tel\" id=\"bk-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"bk-err\" id=\"bk-e-phone\"></em></label><label class=\"bk-field\"><span>Email</span><input type=\"email\" id=\"bk-email\" maxlength=\"120\" autocomplete=\"email\"><em class=\"bk-err\" id=\"bk-e-email\"></em></label></div>");
            List<(string, string)> ins = new List<(string, string)> { (string.Empty, "Choose one") };
            ins.AddRange(site.Settings.Insurance.Select(x => (x, x)));
            ins.Add(("Other", "Other or not sure"));
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>Best way to reach you</span><select id=\"bk-contact\">" + Ui.Opts(new[] { ("text", "Text message"), ("phone", "Phone call"), ("email", "Email") }, "text") + "</select></label><label class=\"bk-field\"><span>Insurance</span><select id=\"bk-ins\">" + Ui.Opts(ins, string.Empty) + "</select></label></div>");
            for (int i = 0; i < qs.Count; i++)
            {
                Question q = qs[i];
                if (q.Kind == "yesno")
                {
                    sb.Append("<div class=\"bk-field bk-q\" data-kind=\"yesno\"><span>" + Ui.Enc(q.Text) + (q.Required ? string.Empty : " <i>(optional)</i>") + "</span><div class=\"bk-yn\"><label><input type=\"radio\" name=\"bk-q" + i + "\" value=\"Yes\"><span>Yes</span></label><label><input type=\"radio\" name=\"bk-q" + i + "\" value=\"No\"><span>No</span></label></div><em class=\"bk-err\" id=\"bk-e-q" + i + "\"></em></div>");
                }
                else
                {
                    sb.Append("<label class=\"bk-field bk-q\" data-kind=\"text\"><span>" + Ui.Enc(q.Text) + (q.Required ? string.Empty : " <i>(optional)</i>") + "</span><input type=\"text\" id=\"bk-q" + i + "\" maxlength=\"120\"><em class=\"bk-err\" id=\"bk-e-q" + i + "\"></em></label>");
                }
            }
            sb.Append("<label class=\"bk-field\"><span>Anything the front desk should know? <i>(optional)</i></span><textarea id=\"bk-note\" rows=\"3\" maxlength=\"400\" placeholder=\"For example: I need step-free access, or I may be a few minutes late.\"></textarea><small class=\"bk-hint\">Please keep medical details for your visit.</small></label>");
            sb.Append("<label class=\"bk-agree\"><input type=\"checkbox\" id=\"bk-agree\"><span>I can come at this time, and I will cancel at least " + site.Settings.CancelHours + " hours before if I can't.</span></label><em class=\"bk-err\" id=\"bk-e-agree\"></em><em class=\"bk-err\" id=\"bk-e-form\" role=\"alert\"></em>");
            sb.Append("<button type=\"submit\" class=\"bk-btn bk-btn-accent bk-cbtn\">Confirm booking</button></form>");
            return sb.ToString();
        }

        private static string Summary(SiteData site, Engine e, St st)
        {
            StringBuilder sb = new StringBuilder("<h2>Your visit</h2>");
            if (st.Sv == null)
            {
                sb.Append("<p class=\"bk-hint\">Your choices appear here as you go.</p>");
            }
            else
            {
                Clinician? p = st.P == "any" ? st.Pa : Ui.ProviderOf(site, st.P);
                sb.Append("<div class=\"bk-sumtop\">" + (st.Sv.Image != string.Empty ? Ui.Pic(st.Sv.Image, string.Empty, "bk-sumimg", true) : string.Empty) + "<div><b>" + Ui.Enc(st.Sv.Name) + "</b><small>" + Ui.Dur(st.Sv.Minutes) + (Price(site, st.Sv) != string.Empty ? " &middot; " + Price(site, st.Sv) : string.Empty) + "</small></div></div><dl class=\"bk-sumdl\">");
                sb.Append("<div><dt>With</dt><dd>" + (p != null ? Ui.Enc(p.Name) : st.P == "any" ? "First available" : "&mdash;") + "</dd></div>");
                sb.Append("<div><dt>When</dt><dd>" + (st.T != null ? Ui.Day(st.T.Value, false) + "<br>" + Ui.Hm(site, st.T.Value) : "&mdash;") + "</dd></div>");
                ClinicRoom? r = p != null ? Ui.RoomOf(site, p.Room) : null;
                sb.Append("<div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Name) + (r != null ? "<br><small>" + Ui.Enc(r.Name) + "</small>" : string.Empty) + "</dd></div></dl>");
            }
            sb.Append("<p class=\"bk-sumnote\">Free to change or cancel online up to " + site.Settings.CancelHours + " hours before.</p>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Step()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite(Readable(GetDataValue("sbx")));
            Engine e = new Engine(site);
            St st = Norm(site, e, Ui.Clip(GetDataValue("sv"), 60), Ui.Clip(GetDataValue("p"), 40), Ui.Clip(GetDataValue("pa"), 40), Ui.Clip(GetDataValue("t"), 20), Ui.Clip(GetDataValue("day"), 12), Ui.Clip(GetDataValue("wk"), 12), Ui.Clip(GetDataValue("pref"), 40));
            response.SetElementContents("bk-flow", Flow(site, e, st));
            response.SetElementContents("bk-sum", Summary(site, e, st));
            response.ExecuteScript("BookJs.after();");
            return response;
        }

        public async Task<ApiResponse> Confirm()
        {
            ApiResponse response = new ApiResponse();
            string first = Ui.Clip(GetDataValue("first"), 40);
            string last = Ui.Clip(GetDataValue("last"), 40);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string contact = GetDataValue("contact") is "phone" or "email" ? GetDataValue("contact") : "text";
            string insIn = Ui.Clip(GetDataValue("ins"), 60);
            string note = Ui.Clip(GetDataValue("note"), 400);
            bool isNew = GetDataValue("newp") == "new";
            SiteData look = await Store.LoadSite(Readable(GetDataValue("sbx")));
            CareService? svc = look.Services.FirstOrDefault(x => x.Key == GetDataValue("sv") && x.Active && x.Online);
            if (svc == null)
            {
                response.SetElementContents("bk-e-form", "This service can no longer be booked online. Please start again.");
                return response;
            }
            Dictionary<string, string> errs = new Dictionary<string, string>();
            if (first == string.Empty)
            {
                errs["first"] = "Enter your first name.";
            }
            if (last == string.Empty)
            {
                errs["last"] = "Enter your last name.";
            }
            if (Ui.Digits(phone).Length < 7)
            {
                errs["phone"] = "Enter a phone number we can reach you on.";
            }
            if (!Ui.Email(email))
            {
                errs["email"] = "Enter a valid email address.";
            }
            if (isNew && !svc.NewOk)
            {
                errs["form"] = "This service is for existing patients.";
            }
            List<Answer> answers = new List<Answer>();
            List<Question> qs = Ui.SpecialtyOf(look, svc.Specialty)?.Questions ?? new List<Question>();
            for (int i = 0; i < qs.Count; i++)
            {
                string v = Ui.Clip(GetDataValue("q" + i), 120);
                if (qs[i].Kind == "yesno" && v != "Yes" && v != "No")
                {
                    v = string.Empty;
                }
                if (qs[i].Required && v == string.Empty)
                {
                    errs["q" + i] = qs[i].Kind == "yesno" ? "Choose yes or no." : "Please answer this question.";
                }
                if (v != string.Empty)
                {
                    answers.Add(new Answer { Q = qs[i].Text, A = v });
                }
            }
            if (GetDataValue("agree") != "1")
            {
                errs["agree"] = "Please tick this box to continue.";
            }
            if (errs.Count > 0)
            {
                foreach (KeyValuePair<string, string> kv in errs)
                {
                    response.SetElementContents("bk-e-" + kv.Key, kv.Value);
                }
                response.ExecuteScript("BookJs.invalid();");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            Appointment appt;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(sbx);
                Engine e = new Engine(site);
                St st = Norm(site, e, svc.Key, Ui.Clip(GetDataValue("p"), 40), Ui.Clip(GetDataValue("pa"), 40), Ui.Clip(GetDataValue("t"), 20), string.Empty, Ui.Clip(GetDataValue("wk"), 12), string.Empty);
                if (st.T == null || st.Pa == null || st.Sv == null)
                {
                    response.SetElementContents("bk-flow", Flow(site, e, st));
                    response.SetElementContents("bk-sum", Summary(site, e, st));
                    response.ExecuteScript("BookJs.after();BookJs.toast('Sorry, that time was just taken. Please choose another.');");
                    return response;
                }
                string digits = Ui.Digits(phone);
                Patient? pt = site.Patients.FirstOrDefault(x => x.Last.Equals(last, StringComparison.OrdinalIgnoreCase) && (x.Email.Equals(email, StringComparison.OrdinalIgnoreCase) || (Ui.Digits(x.Phone).Length >= 7 && Ui.Digits(x.Phone) == digits)));
                if (pt != null)
                {
                    DateTime t0 = st.T.Value;
                    DateTime t1 = t0.AddMinutes(st.Sv.Minutes);
                    List<Appointment> mine = site.Appointments.Where(x => x.Patient == pt.Id && x.Start >= e.Now && (x.Status is "booked" or "confirmed")).ToList();
                    if (mine.Any(x => x.Start < t1 && x.Start.AddMinutes(x.Minutes) > t0))
                    {
                        response.SetElementContents("bk-e-form", "You already have an appointment at this time. Open My appointment to change it.");
                        return response;
                    }
                    if (mine.Count(x => x.Source == "web") >= 4)
                    {
                        response.SetElementContents("bk-e-form", "You already have four upcoming online bookings. Please call us to book more.");
                        return response;
                    }
                    if (pt.Email == string.Empty)
                    {
                        pt.Email = email;
                    }
                    if (pt.Phone == string.Empty)
                    {
                        pt.Phone = phone;
                    }
                    if (insIn != string.Empty)
                    {
                        pt.Insurance = insIn;
                    }
                }
                else
                {
                    pt = new Patient { Id = Codes.Id("pt"), First = first, Last = last, Phone = phone, Email = email, Contact = contact, Insurance = insIn, Created = DateTime.UtcNow };
                    site.Patients.Add(pt);
                }
                appt = new Appointment
                {
                    Id = Codes.Id("ap"),
                    Code = Codes.New(site),
                    Patient = pt.Id,
                    Service = st.Sv.Key,
                    Provider = st.Pa.Id,
                    Room = st.Pa.Room,
                    Start = st.T.Value,
                    Minutes = st.Sv.Minutes,
                    Status = "booked",
                    Source = "web",
                    NewPatient = isNew,
                    Note = note,
                    Answers = answers,
                    Created = DateTime.UtcNow,
                    By = "web",
                    History = new List<Hist> { new Hist { When = DateTime.UtcNow, By = "web", Text = "Booked online" } }
                };
                site.Appointments.Add(appt);
                await Store.SaveSite(site, sbx);
            }
            await Store.Log(sbx, "web", "book", appt.Code, true);
            Clinician? pr = Ui.ProviderOf(site, appt.Provider);
            CareService? s2 = Ui.ServiceOf(site, appt.Service);
            StringBuilder sb = new StringBuilder("<section class=\"bk-booked\" id=\"bk-booked\"" + IcsAttrs(site, appt) + "><div class=\"bk-tick\" aria-hidden=\"true\"></div><h2>You're booked, " + Ui.Enc(first) + "</h2><p>Keep your booking code. You need it with your email or phone to change or cancel.</p><div class=\"bk-code\">" + Ui.Enc(appt.Code) + "</div><dl class=\"bk-sumdl\">");
            sb.Append("<div><dt>Service</dt><dd>" + Ui.Enc(s2?.Name ?? string.Empty) + "</dd></div><div><dt>With</dt><dd>" + Ui.Enc(pr?.Name ?? string.Empty) + "</dd></div><div><dt>When</dt><dd>" + Ui.LongDay(appt.Start) + " at " + Ui.Hm(site, appt.Start) + "</dd></div><div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Address) + (Ui.RoomName(site, appt.Room) != string.Empty ? " &middot; " + Ui.Enc(Ui.RoomName(site, appt.Room)) : string.Empty) + "</dd></div></dl>");
            sb.Append("<p class=\"bk-info-box\">Please arrive 10 minutes early and bring photo ID and your insurance card.</p><div class=\"bk-bacts\"><button type=\"button\" class=\"bk-btn bk-btn-accent\" onclick=\"BookJs.ics('bk-booked')\">Add to calendar</button><a class=\"bk-btn bk-btn-ghost\" href=\"MyAppointment?c=" + Uri.EscapeDataString(appt.Code) + "\">Manage booking</a><a class=\"bk-btn bk-btn-ghost\" href=\"Book\">Book another</a></div>");
            if (sbx != string.Empty)
            {
                sb.Append("<p class=\"bk-hint bk-demo-note\">This booking is in your demo copy. <a href=\"AdminLogin\">Sign in to the admin</a> to see it on the day schedule.</p>");
            }
            sb.Append("</section>");
            response.SetElementContents("bk-flow", sb.ToString());
            response.SetElementContents("bk-sum", "<h2>What happens next</h2><ol class=\"bk-nextl\"><li>We hold this time for you.</li><li>The front desk may call or text to confirm.</li><li>Need to change? Use your code on <a href=\"MyAppointment?c=" + Uri.EscapeDataString(appt.Code) + "\">My appointment</a>.</li></ol>");
            response.ExecuteScript("BookJs.booked();");
            return response;
        }

        private static string Short(Clinician p)
        {
            return p.Name.StartsWith("Dr", StringComparison.Ordinal) ? "Dr. " + p.Name.Split(' ')[^1] : p.Name.Split(' ')[0];
        }

        private static string Strip(SiteData site, Engine e, CareService sv, Clinician? only, DateTime wk, DateTime day, string ignore)
        {
            DateTime last = e.Last(true);
            DateTime prev = wk.AddDays(-7) < e.Today ? e.Today : wk.AddDays(-7);
            StringBuilder sb = new StringBuilder("<div class=\"bk-wk\"><button type=\"button\" class=\"bk-wnav\" aria-label=\"Earlier days\" onclick=\"BookJs.week('" + Ui.Ymd(prev) + "')\"" + (wk <= e.Today ? " disabled" : string.Empty) + ">&lsaquo;</button><div class=\"bk-days\">");
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = wk.AddDays(i);
                bool closed = e.Clinic(d) == null;
                int n = d > last || closed ? 0 : e.Slots(sv, only, d, true, ignore).Count;
                total += n;
                string label = closed ? "Closed" : d > last ? "&mdash;" : n == 0 ? "Full" : n + (n == 1 ? " time" : " times");
                sb.Append("<button type=\"button\" class=\"bk-day" + (d == day && n > 0 ? " bk-act" : string.Empty) + "\" onclick=\"BookJs.day('" + Ui.Ymd(d) + "')\"" + (n == 0 ? " disabled" : string.Empty) + "><small>" + (d == e.Today ? "Today" : d.ToString("ddd", Ui.Inv)) + "</small><b>" + d.Day + "</b><small>" + d.ToString("MMM", Ui.Inv) + "</small><em>" + label + "</em></button>");
            }
            sb.Append("</div><button type=\"button\" class=\"bk-wnav\" aria-label=\"Later days\" onclick=\"BookJs.week('" + Ui.Ymd(wk.AddDays(7)) + "')\"" + (wk.AddDays(7) > last ? " disabled" : string.Empty) + ">&rsaquo;</button></div>");
            if (total == 0)
            {
                (DateTime Start, Clinician P)? n = null;
                for (int i = 7; i < 70 && n == null; i++)
                {
                    DateTime d = wk.AddDays(i);
                    if (d > last)
                    {
                        break;
                    }
                    List<(DateTime Start, Clinician P)> l = e.Slots(sv, only, d, true, ignore);
                    if (l.Count > 0)
                    {
                        n = l[0];
                    }
                }
                sb.Append("<div class=\"bk-nofree\"><p>No free times in these seven days.</p>" + (n != null ? "<button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-accent\" onclick=\"BookJs.week('" + Ui.Ymd(n.Value.Start.Date) + "')\">Jump to " + Ui.Day(n.Value.Start, false) + "</button>" : "<p class=\"bk-hint\">Please call " + Ui.Enc(site.Settings.Phone) + " and we will find you a time.</p>") + "</div>");
                return sb.ToString();
            }
            List<(DateTime Start, Clinician P)> slots = e.Slots(sv, only, day, true, ignore);
            sb.Append("<p class=\"bk-dayname\">" + Ui.LongDay(day) + "</p>");
            (string Name, int From, int To)[] parts = { ("Morning", 0, 720), ("Afternoon", 720, 1020), ("Evening", 1020, 1440) };
            foreach ((string name, int from, int to) in parts)
            {
                List<(DateTime Start, Clinician P)> part = slots.Where(x => x.Start.TimeOfDay.TotalMinutes >= from && x.Start.TimeOfDay.TotalMinutes < to).ToList();
                if (part.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"bk-tgroup\"><h3>" + name + "</h3><div class=\"bk-times\">");
                foreach ((DateTime t, Clinician p) in part)
                {
                    sb.Append("<button type=\"button\" class=\"bk-ts\" onclick=\"BookJs.pickTime('" + Ui.Stamp(t) + "','" + Ui.Js(p.Id) + "')\"><b>" + Ui.Hm(site, t) + "</b>" + (only == null ? "<small>" + Ui.Enc(Short(p)) + "</small>" : string.Empty) + "</button>");
                }
                sb.Append("</div></div>");
            }
            return sb.ToString();
        }

        private static string IcsAttrs(SiteData site, Appointment a)
        {
            CareService? sv = Ui.ServiceOf(site, a.Service);
            Clinician? p = Ui.ProviderOf(site, a.Provider);
            string title = (sv?.Name ?? "Appointment") + (p != null ? " with " + p.Name : string.Empty) + " | " + site.Settings.Name;
            return " data-code=\"" + Ui.Attr(a.Code) + "\" data-start=\"" + a.Start.ToString("yyyyMMdd'T'HHmmss", Ui.Inv) + "\" data-end=\"" + a.Start.AddMinutes(a.Minutes).ToString("yyyyMMdd'T'HHmmss", Ui.Inv) + "\" data-title=\"" + Ui.Attr(title) + "\" data-loc=\"" + Ui.Attr(site.Settings.Name + ", " + site.Settings.Address) + "\"";
        }
    }
}
