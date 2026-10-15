using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class MyAppointment : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("c"), 80), Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ma-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "ma", "MyAppointment", false))
                .Replace("{plhd_drawer}", Nav(site, "ma", "MyAppointment", true))
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
                return "<i class=\"ma-shut\"></i>Closed now &middot; opens today at " + Ui.Hm(site, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = e.Clinic(now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"ma-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Inv)) + " at " + Ui.Hm(site, n.Value.From);
                }
            }
            return "<i class=\"ma-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site)
        {
            int today = (int)Clock.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.From) + " &ndash; " + Ui.HmText(site, o.To) : "Closed";
                sb.Append("<span class=\"ma-hrow" + (d == today ? " ma-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("MyAppointmentJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80), Ui.Clip(GetDataValue("argC"), 80));
            response.SetElementContents("ma-main", v.Main);
            response.SetElementContents("ma-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ma-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("ma-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ma-today", TodayLine(site));
            response.SetElementContents("ma-sphone", Ui.Enc(site.Settings.Phone));
            response.SetElementContents("ma-fhours", HoursRows(site));
            response.SetElementContents("ma-fprov", Ui.Enc(site.Settings.ProvidersWord));
            response.SetElementContents("ma-urgent", Ui.Enc(site.Settings.Urgent));
            response.SetElementContents("ma-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MyAppointmentJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("MyAppointmentJs.sandboxed('" + sbx + "');");
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
            return sp == null ? string.Empty : "<span class=\"ma-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#1F7A74") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
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
            return "<article class=\"ma-card\"><a class=\"ma-card-img\" href=\"" + href + "\">" + Ui.Pic(sv.Image, sv.Name, string.Empty, true) + "</a><div class=\"ma-card-body\">" + SpecTag(site, sv.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><div class=\"ma-facts\"><span>" + Ui.Dur(sv.Minutes) + "</span>" + (price != string.Empty ? "<span>" + price + "</span>" : string.Empty) + "</div><div class=\"ma-cfoot\"><div><small>" + (sv.Online ? "Next available" : "Book by phone") + "</small><b>" + (sv.Online ? NextText(site, n, e) : Ui.Enc(site.Settings.Phone)) + "</b></div>" + (sv.Online ? "<a class=\"ma-btn ma-btn-sm ma-btn-accent\" href=\"Book?sv=" + Uri.EscapeDataString(sv.Key) + "\">Book</a>" : string.Empty) + "</div></div></article>";
        }

        private static string ProviderCard(SiteData site, Clinician p, Engine e)
        {
            string href = "Provider?id=" + Uri.EscapeDataString(p.Id);
            CareService? quick = site.Services.Where(s => s.Active && s.Online && s.Providers.Contains(p.Id)).OrderBy(s => s.Minutes).FirstOrDefault();
            (DateTime Start, Clinician P)? best = quick != null && p.Accepting ? e.Next(quick, p, 14) : null;
            return "<article class=\"ma-pcard\"><a href=\"" + href + "\">" + Ui.Face("ma", p) + "</a><div class=\"ma-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) + "</a></h3><p>" + Ui.Enc(p.Title) + (p.Languages.Count > 0 ? " &middot; " + Ui.Enc(string.Join(", ", p.Languages)) : string.Empty) + "</p><div class=\"ma-next\">" + (p.Accepting ? "Next online time<b>" + NextText(site, best, e) + "</b>" : "<b>Not taking online bookings</b>Please call the clinic") + "</div></div></article>";
        }

        private View Render(SiteData site, string a, string b, string c)
        {
            string code = Regex.Replace(a.ToUpperInvariant(), "[^A-Z0-9-]", string.Empty);
            StringBuilder sb = new StringBuilder("<section class=\"ma-phead\"><div class=\"ma-wrap\"><span class=\"ma-kick\">Your booking</span><h1>My appointment</h1><p class=\"ma-lead\">Check the details, pick a new time or cancel. You need your booking code and the email or phone you booked with.</p></div></section>");
            sb.Append("<div class=\"ma-wrap ma-magrid\"><form class=\"ma-box ma-find\" onsubmit=\"return MyAppointmentJs.find()\" novalidate><h2>Find your booking</h2><label class=\"ma-field\"><span>Booking code</span><input type=\"text\" id=\"ma-code\" maxlength=\"12\" autocomplete=\"off\" autocapitalize=\"characters\" spellcheck=\"false\" placeholder=\"" + Codes.Prefix(site) + "-XXXXXX\" value=\"" + Ui.Attr(code) + "\"></label><label class=\"ma-field\"><span>Email or phone you booked with</span><input type=\"text\" id=\"ma-who\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"ma-err\" id=\"ma-e-find\" role=\"alert\"></em><button type=\"submit\" class=\"ma-btn ma-btn-accent ma-btn-wide\">Find my booking</button><p class=\"ma-hint\">Lost your code? Call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.</p></form><div class=\"ma-res\" id=\"ma-res\"><div class=\"ma-empty\"><h2>Your booking appears here</h2><p>Changes and cancellations are free up to " + site.Settings.CancelHours + " hours before your visit.</p><a class=\"ma-btn ma-btn-ghost ma-btn-sm\" href=\"Book\">Make a new booking</a></div></div></div>");
            return new View { Title = "My appointment", Desc = "Check, change or cancel your booking at " + site.Settings.Name + ".", Main = sb.ToString() };
        }

        private static Appointment? Verify(SiteData site, string code, string who)
        {
            Appointment? a = site.Appointments.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (a == null || who == string.Empty)
            {
                return null;
            }
            Patient? p = site.Patients.FirstOrDefault(x => x.Id == a.Patient);
            if (p == null)
            {
                return null;
            }
            if (who.Contains('@'))
            {
                return p.Email != string.Empty && p.Email.Equals(who, StringComparison.OrdinalIgnoreCase) ? a : null;
            }
            string d = Ui.Digits(who);
            string mine = Ui.Digits(p.Phone);
            if (d.Length < 7 || mine.Length < 7)
            {
                return null;
            }
            return mine.EndsWith(d.Length > 10 ? d.Substring(d.Length - 10) : d, StringComparison.Ordinal) || d.EndsWith(mine, StringComparison.Ordinal) ? a : null;
        }

        private static bool CanChange(SiteData site, Engine e, Appointment a)
        {
            return (a.Status is "booked" or "confirmed") && (a.Start - e.Now).TotalHours >= Math.Max(0, site.Settings.CancelHours);
        }

        private static string Card(SiteData site, Engine e, Appointment a)
        {
            CareService? sv = Ui.ServiceOf(site, a.Service);
            Clinician? p = Ui.ProviderOf(site, a.Provider);
            Patient? pt = site.Patients.FirstOrDefault(x => x.Id == a.Patient);
            string room = Ui.RoomName(site, a.Room);
            StringBuilder sb = new StringBuilder("<article class=\"ma-box ma-appt\" id=\"ma-appt\"" + IcsAttrs(site, a) + "><div class=\"ma-atop\">" + Ui.StatusPill("ma", a) + "<span class=\"ma-acode\">" + Ui.Enc(a.Code) + "</span></div><div class=\"ma-amain\">" + Ui.Face("ma", p) + "<div><h2>" + Ui.Enc(sv?.Name ?? "Appointment") + "</h2><p>with " + Ui.Enc(p?.Name ?? "our team") + (p != null ? " &middot; " + Ui.Enc(p.Title) : string.Empty) + "</p></div></div><dl class=\"ma-sumdl\">");
            sb.Append("<div><dt>When</dt><dd>" + Ui.LongDay(a.Start) + "<br>" + Ui.Hm(site, a.Start) + " &ndash; " + Ui.Hm(site, a.Start.AddMinutes(a.Minutes)) + "</dd></div><div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Name) + ", " + Ui.Enc(site.Settings.Address) + (room != string.Empty ? "<br><small>" + Ui.Enc(room) + "</small>" : string.Empty) + "</dd></div><div><dt>For</dt><dd>" + Ui.Enc(pt != null ? pt.First + " " + (pt.Last.Length > 0 ? pt.Last.Substring(0, 1) + "." : string.Empty) : string.Empty) + "</dd></div></dl>");
            string again = "Book?sv=" + Uri.EscapeDataString(a.Service) + "&amp;p=" + Uri.EscapeDataString(a.Provider);
            if ((a.Status is "booked" or "confirmed") && a.Start > e.Now)
            {
                if (CanChange(site, e, a))
                {
                    sb.Append("<div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.resched()\">Change time</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button><button type=\"button\" class=\"ma-btn ma-btn-danger\" onclick=\"MyAppointmentJs.cancel()\">Cancel appointment</button></div>");
                }
                else
                {
                    sb.Append("<p class=\"ma-warn-box\">Your visit is less than " + site.Settings.CancelHours + " hours away, so it can't be changed online. To change or cancel, please call " + Ui.Enc(site.Settings.Phone) + ".</p><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button></div>");
                }
            }
            else if (a.Status == "cancelled")
            {
                sb.Append("<p class=\"ma-info-box\">This appointment was cancelled.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book again</a></div>");
            }
            else if (a.Status == "done")
            {
                sb.Append("<p class=\"ma-ok-box\">This visit is complete. Thank you for coming in.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book a follow-up</a></div>");
            }
            else if (a.Status == "noshow")
            {
                sb.Append("<p class=\"ma-warn-box\">We missed you at this visit.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book a new time</a></div>");
            }
            else if (a.Status is "arrived" or "inroom")
            {
                sb.Append("<p class=\"ma-ok-box\">You are checked in. Please take a seat; we will call you shortly.</p>");
            }
            sb.Append("<div id=\"ma-move\"></div></article>");
            return sb.ToString();
        }

        private (string Code, string Who) Keys()
        {
            return (Regex.Replace(Ui.Clip(GetDataValue("code"), 12).ToUpperInvariant(), "[^A-Z0-9-]", string.Empty), Ui.Clip(GetDataValue("who"), 120));
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            (string code, string who) = Keys();
            if (code == string.Empty || who == string.Empty)
            {
                response.SetElementContents("ma-e-find", "Enter your booking code and the email or phone you booked with.");
                return response;
            }
            if (Auth.Locked("appt:" + code))
            {
                response.SetElementContents("ma-e-find", "Too many tries. Please wait a few minutes or call us.");
                return response;
            }
            SiteData site = await Store.LoadSite(Readable(GetDataValue("sbx")));
            Appointment? a = Verify(site, code, who);
            if (a == null)
            {
                Auth.Fail("appt:" + code);
                response.SetElementContents("ma-e-find", "We couldn't find a booking with those details. Check the code and try the email or phone you used.");
                return response;
            }
            response.SetElementContents("ma-res", Card(site, new Engine(site), a));
            response.ExecuteScript("MyAppointmentJs.found();");
            return response;
        }

        public async Task<ApiResponse> Times()
        {
            ApiResponse response = new ApiResponse();
            (string code, string who) = Keys();
            SiteData site = await Store.LoadSite(Readable(GetDataValue("sbx")));
            Engine e = new Engine(site);
            Appointment? a = Verify(site, code, who);
            CareService? sv = a != null ? Ui.ServiceOf(site, a.Service) : null;
            Clinician? p = a != null ? Ui.ProviderOf(site, a.Provider) : null;
            if (a == null || sv == null || p == null || !CanChange(site, e, a))
            {
                response.ExecuteScript("MyAppointmentJs.toast('This booking can no longer be changed online.');");
                return response;
            }
            if (!e.Online(p, sv))
            {
                response.SetElementContents("ma-move", "<p class=\"ma-warn-box\">This visit can't be moved online right now. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
                return response;
            }
            DateTime wk = Ui.ParseDay(GetDataValue("wk")) ?? e.Today;
            wk = wk < e.Today ? e.Today : wk > e.Last(true) ? e.Last(true) : wk;
            DateTime? day = Ui.ParseDay(GetDataValue("day"));
            if (day == null || day < wk || day > wk.AddDays(6))
            {
                day = null;
                for (int i = 0; i < 7 && day == null; i++)
                {
                    if (e.Slots(sv, p, wk.AddDays(i), true, a.Id).Count > 0)
                    {
                        day = wk.AddDays(i);
                    }
                }
            }
            response.SetElementContents("ma-move", "<div class=\"ma-mv\" id=\"ma-mv\" data-wk=\"" + Ui.Ymd(wk) + "\"><div class=\"ma-mvh\"><h3>Pick a new time with " + Ui.Enc(p.Name) + "</h3><button type=\"button\" class=\"ma-btn ma-btn-sm ma-btn-ghost\" onclick=\"MyAppointmentJs.closeMove()\">Keep my time</button></div>" + Strip(site, e, sv, p, wk, day ?? wk, a.Id) + "</div>");
            response.ExecuteScript("MyAppointmentJs.moving();");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            (string code, string who) = Keys();
            DateTime? t = Ui.ParseStart(GetDataValue("t"));
            SiteData look = await Store.LoadSite(Readable(GetDataValue("sbx")));
            Engine le = new Engine(look);
            Appointment? la = Verify(look, code, who);
            if (la == null || t == null || !CanChange(look, le, la))
            {
                response.ExecuteScript("MyAppointmentJs.toast('This booking can no longer be changed online.');");
                return response;
            }
            Clinician? lp = Ui.ProviderOf(look, la.Provider);
            CareService? ls = Ui.ServiceOf(look, la.Service);
            if (lp == null || ls == null || !le.Fits(lp, ls, t.Value, la.Minutes, true, la.Id))
            {
                response.ExecuteScript("MyAppointmentJs.toast('Sorry, that time is no longer free.');");
                return response;
            }
            if (GetDataValue("go") != "1")
            {
                response.SetElementContents("ma-move", "<div class=\"ma-mv ma-mvok\"><h3>Move your visit?</h3><p><s>" + Ui.Day(la.Start, false) + " at " + Ui.Hm(look, la.Start) + "</s><br><b>" + Ui.LongDay(t.Value) + " at " + Ui.Hm(look, t.Value) + "</b></p><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.move('" + Ui.Stamp(t.Value) + "',1)\">Yes, move it</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.resched()\">Pick another time</button></div></div>");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            SiteData site;
            Appointment? a;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(sbx);
                Engine e = new Engine(site);
                a = Verify(site, code, who);
                Clinician? p = a != null ? Ui.ProviderOf(site, a.Provider) : null;
                CareService? sv = a != null ? Ui.ServiceOf(site, a.Service) : null;
                if (a == null || p == null || sv == null || !CanChange(site, e, a) || !e.Fits(p, sv, t.Value, a.Minutes, true, a.Id))
                {
                    response.ExecuteScript("MyAppointmentJs.toast('Sorry, that time is no longer free.');");
                    return response;
                }
                a.History.Add(new Hist { When = DateTime.UtcNow, By = "web", Text = "Moved online from " + Ui.Day(a.Start, false) + " " + Ui.Hm(site, a.Start) });
                a.Start = t.Value;
                a.Status = "booked";
                a.Seed = false;
                await Store.SaveSite(site, sbx);
            }
            await Store.Log(sbx, "web", "move", a.Code, true);
            response.SetElementContents("ma-res", Card(site, new Engine(site), a));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Done. Your visit has been moved.');");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            (string code, string who) = Keys();
            string sbx = Store.Demo ? Store.Sandbox(GetDataValue("sbx")) : string.Empty;
            SiteData look = await Store.LoadSite(sbx);
            Appointment? la = Verify(look, code, who);
            if (la == null || !CanChange(look, new Engine(look), la))
            {
                response.ExecuteScript("MyAppointmentJs.toast('This booking can no longer be cancelled online.');");
                return response;
            }
            sbx = await Writable(response, GetDataValue("sbx"));
            SiteData site;
            Appointment? a;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(sbx);
                a = Verify(site, code, who);
                if (a == null || !CanChange(site, new Engine(site), a))
                {
                    response.ExecuteScript("MyAppointmentJs.toast('This booking can no longer be cancelled online.');");
                    return response;
                }
                a.Status = "cancelled";
                a.Seed = false;
                a.History.Add(new Hist { When = DateTime.UtcNow, By = "web", Text = "Cancelled online" });
                await Store.SaveSite(site, sbx);
            }
            await Store.Log(sbx, "web", "cancel", a.Code, true);
            response.SetElementContents("ma-res", Card(site, new Engine(site), a));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Your appointment has been cancelled.');");
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
            StringBuilder sb = new StringBuilder("<div class=\"ma-wk\"><button type=\"button\" class=\"ma-wnav\" aria-label=\"Earlier days\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(prev) + "')\"" + (wk <= e.Today ? " disabled" : string.Empty) + ">&lsaquo;</button><div class=\"ma-days\">");
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = wk.AddDays(i);
                bool closed = e.Clinic(d) == null;
                int n = d > last || closed ? 0 : e.Slots(sv, only, d, true, ignore).Count;
                total += n;
                string label = closed ? "Closed" : d > last ? "&mdash;" : n == 0 ? "Full" : n + (n == 1 ? " time" : " times");
                sb.Append("<button type=\"button\" class=\"ma-day" + (d == day && n > 0 ? " ma-act" : string.Empty) + "\" onclick=\"MyAppointmentJs.day('" + Ui.Ymd(d) + "')\"" + (n == 0 ? " disabled" : string.Empty) + "><small>" + (d == e.Today ? "Today" : d.ToString("ddd", Ui.Inv)) + "</small><b>" + d.Day + "</b><small>" + d.ToString("MMM", Ui.Inv) + "</small><em>" + label + "</em></button>");
            }
            sb.Append("</div><button type=\"button\" class=\"ma-wnav\" aria-label=\"Later days\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(wk.AddDays(7)) + "')\"" + (wk.AddDays(7) > last ? " disabled" : string.Empty) + ">&rsaquo;</button></div>");
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
                sb.Append("<div class=\"ma-nofree\"><p>No free times in these seven days.</p>" + (n != null ? "<button type=\"button\" class=\"ma-btn ma-btn-sm ma-btn-accent\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(n.Value.Start.Date) + "')\">Jump to " + Ui.Day(n.Value.Start, false) + "</button>" : "<p class=\"ma-hint\">Please call " + Ui.Enc(site.Settings.Phone) + " and we will find you a time.</p>") + "</div>");
                return sb.ToString();
            }
            List<(DateTime Start, Clinician P)> slots = e.Slots(sv, only, day, true, ignore);
            sb.Append("<p class=\"ma-dayname\">" + Ui.LongDay(day) + "</p>");
            (string Name, int From, int To)[] parts = { ("Morning", 0, 720), ("Afternoon", 720, 1020), ("Evening", 1020, 1440) };
            foreach ((string name, int from, int to) in parts)
            {
                List<(DateTime Start, Clinician P)> part = slots.Where(x => x.Start.TimeOfDay.TotalMinutes >= from && x.Start.TimeOfDay.TotalMinutes < to).ToList();
                if (part.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"ma-tgroup\"><h3>" + name + "</h3><div class=\"ma-times\">");
                foreach ((DateTime t, Clinician p) in part)
                {
                    sb.Append("<button type=\"button\" class=\"ma-ts\" onclick=\"MyAppointmentJs.pickTime('" + Ui.Stamp(t) + "','" + Ui.Js(p.Id) + "')\"><b>" + Ui.Hm(site, t) + "</b>" + (only == null ? "<small>" + Ui.Enc(Short(p)) + "</small>" : string.Empty) + "</button>");
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
