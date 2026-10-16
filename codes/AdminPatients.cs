using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminPatients : WebPage
    {
        private const string AreaKey = "patients";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Patients");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--pt-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("pt-menu", Auth.Menu("pt", "AdminPatients", c, site));
            response.SetElementContents("pt-who", Auth.Who("pt", c));
            response.SetElementContents("pt-demo", Auth.DemoPill("pt", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminPatientsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"pt-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"pt-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminPatientsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPatientsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("pt-main", NoAccess("Patients"));
                return response;
            }
            response.SetElementContents("pt-main", await MainHtml(c, site));
            response.ExecuteScript("AdminPatientsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"pt-tr pt-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 25;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<div class=\"pt-ptop\"><div><h1>" + Ui.Enc(site.Settings.PatientsWord) + "</h1><p>Contact details and visit history. Clinical notes stay in your clinical record system.</p></div><div class=\"pt-acts\">" + (edit ? "<button type=\"button\" class=\"pt-btn pt-btn-accent\" onclick=\"AdminPatientsJs.rec('')\">Add " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) + "</button>" : string.Empty) + "</div></div>");
            sb.Append("<div class=\"pt-bar\"><label>Search<input type=\"search\" id=\"pt-q\" placeholder=\"Name, phone or email\" maxlength=\"60\" oninput=\"AdminPatientsJs.search()\"></label><label>Show<select id=\"pt-pf\" onchange=\"AdminPatientsJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Everyone"), ("next", "With an upcoming visit"), ("recent", "Seen in the last 90 days"), ("new", "Added in the last 30 days"), ("lapsed", "Not seen for a year") }, string.Empty) + "</select></label><label>Insurance<select id=\"pt-pins\" onchange=\"AdminPatientsJs.list(1)\"><option value=\"\">Any</option>" + Ui.Opts(site.Settings.Insurance.Select(x => (x, x)), string.Empty) + "<option value=\"-\">None given</option></select></label></div><div class=\"pt-tbl pt-pttbl\" id=\"pt-list\"></div><div id=\"pt-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPatientsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            string q = Ui.Clip(GetDataValue("q"), 60);
            string f = Ui.Clip(GetDataValue("f"), 10);
            string ins = Ui.Clip(GetDataValue("ins"), 60);
            string d = Ui.Digits(q);
            ILookup<string, Appointment> by = site.Appointments.ToLookup(a => a.Patient);
            IEnumerable<Patient> all = site.Patients.Where(p => (q == string.Empty || (p.First + " " + p.Last).Contains(q, StringComparison.OrdinalIgnoreCase) || p.Email.Contains(q, StringComparison.OrdinalIgnoreCase) || (d.Length >= 3 && Ui.Digits(p.Phone).Contains(d, StringComparison.Ordinal))) && (ins == string.Empty || (ins == "-" ? p.Insurance == string.Empty : p.Insurance == ins)));
            List<(Patient P, int Visits, DateTime? Last, DateTime? Next)> rows = all.Select(p =>
            {
                List<Appointment> l = by[p.Id].ToList();
                DateTime? last = l.Where(a => a.Status == "done").Select(a => (DateTime?)a.Start).DefaultIfEmpty(null).Max();
                DateTime? next = l.Where(a => (a.Status is "booked" or "confirmed") && a.Start >= e.Now).Select(a => (DateTime?)a.Start).DefaultIfEmpty(null).Min();
                return (p, l.Count(a => a.Status == "done"), last, next);
            }).ToList();
            rows = f switch
            {
                "next" => rows.Where(r => r.Next != null).OrderBy(r => r.Next).ToList(),
                "recent" => rows.Where(r => r.Last != null && r.Last >= e.Today.AddDays(-90)).OrderByDescending(r => r.Last).ToList(),
                "new" => rows.Where(r => r.P.Created >= DateTime.UtcNow.AddDays(-30)).OrderByDescending(r => r.P.Created).ToList(),
                "lapsed" => rows.Where(r => r.Next == null && (r.Last == null || r.Last < e.Today.AddDays(-365))).OrderBy(r => r.P.Last).ToList(),
                _ => rows.OrderBy(r => r.P.Last).ThenBy(r => r.P.First).ToList()
            };
            int page = Ui.PageOf(GetDataValue("pg"), Size, rows.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Name", string.Empty), ("Phone", string.Empty), ("Insurance", string.Empty), ("Visits", "pt-num"), ("Last visit", string.Empty), ("Next visit", string.Empty), (string.Empty, string.Empty) }));
            if (rows.Count == 0)
            {
                sb.Append("<div class=\"pt-tr\"><span class=\"pt-none pt-wide\">No one matches.</span></div>");
            }
            foreach ((Patient p, int visits, DateTime? last, DateTime? next) in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"pt-tr\"><span class=\"pt-wide\"><b>" + Ui.Enc(Ui.Name(p)) + "</b><small>" + Ui.Enc(p.Email) + "</small></span><span data-l=\"Phone\">" + Ui.Enc(p.Phone) + "</span><span data-l=\"Insurance\">" + (p.Insurance != string.Empty ? Ui.Enc(p.Insurance) : "&mdash;") + "</span><span data-l=\"Visits\" class=\"pt-num\">" + visits + "</span><span data-l=\"Last visit\">" + (last != null ? Ui.Day(last.Value, last.Value.Year != e.Today.Year) : "&mdash;") + "</span><span data-l=\"Next visit\">" + (next != null ? Ui.Day(next.Value, false) : "&mdash;") + "</span><span><button type=\"button\" class=\"pt-btn pt-btn-sm pt-btn-ghost\" onclick=\"AdminPatientsJs.rec('" + Ui.Js(p.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("pt-list", sb.ToString());
            response.SetElementContents("pt-pager", Ui.Pager("pt", page, Size, rows.Count, site.Settings.PatientWord.ToLowerInvariant(), site.Settings.PatientsWord.ToLowerInvariant(), "AdminPatientsJs.list"));
            return response;
        }

        public async Task<ApiResponse> Record()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPatientsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "view", "patient", "You don't have access to patient records.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Engine e = new Engine(site);
            Patient? p = site.Patients.FirstOrDefault(x => x.Id == GetDataValue("id"));
            bool edit = Access.Can(c.U, AreaKey, 'E');
            if (p == null && !edit)
            {
                return response;
            }
            Patient v = p ?? new Patient();
            string ro = edit ? string.Empty : " readonly";
            List<(string, string)> ins = new List<(string, string)> { (string.Empty, "None given") };
            ins.AddRange(site.Settings.Insurance.Select(x => (x, x)));
            if (v.Insurance != string.Empty && !site.Settings.Insurance.Contains(v.Insurance))
            {
                ins.Add((v.Insurance, v.Insurance));
            }
            StringBuilder sb = new StringBuilder("<h2>" + (p == null ? "Add " + Ui.Enc(site.Settings.PatientWord.ToLowerInvariant()) : Ui.Enc(Ui.Name(p))) + "</h2>");
            if (p != null)
            {
                List<Appointment> l = site.Appointments.Where(a => a.Patient == p.Id).ToList();
                int nDone = l.Count(a => a.Status == "done");
                int nNo = l.Count(a => a.Status == "noshow");
                sb.Append("<div class=\"pt-pstats\"><span>" + nDone + (nDone == 1 ? " visit" : " visits") + "</span><span>" + l.Count(a => (a.Status is "booked" or "confirmed") && a.Start >= e.Now) + " upcoming</span><span>" + nNo + (nNo == 1 ? " no-show" : " no-shows") + "</span><span>" + l.Count(a => a.Status == "cancelled") + " cancelled</span><span>Added " + Ui.Time(p.Created) + "</span></div>");
            }
            sb.Append("<div class=\"pt-form\"><div class=\"pt-f2\"><label class=\"pt-field\"><span>First name</span><input type=\"text\" id=\"pt-pfirst\" maxlength=\"40\" value=\"" + Ui.Attr(v.First) + "\"" + ro + "></label><label class=\"pt-field\"><span>Last name</span><input type=\"text\" id=\"pt-plast\" maxlength=\"40\" value=\"" + Ui.Attr(v.Last) + "\"" + ro + "></label></div><div class=\"pt-f2\"><label class=\"pt-field\"><span>Phone</span><input type=\"tel\" id=\"pt-pphone\" maxlength=\"30\" value=\"" + Ui.Attr(v.Phone) + "\"" + ro + "></label><label class=\"pt-field\"><span>Email</span><input type=\"email\" id=\"pt-pemail\" maxlength=\"120\" value=\"" + Ui.Attr(v.Email) + "\"" + ro + "></label></div><div class=\"pt-f2\"><label class=\"pt-field\"><span>Prefers</span><select id=\"pt-pcontact\"" + (edit ? string.Empty : " disabled") + ">" + Ui.Opts(new[] { ("text", "Text message"), ("phone", "Phone call"), ("email", "Email") }, v.Contact) + "</select></label><label class=\"pt-field\"><span>Insurance</span><select id=\"pt-pinsx\"" + (edit ? string.Empty : " disabled") + ">" + Ui.Opts(ins, v.Insurance) + "</select></label></div><label class=\"pt-field\"><span>Front-desk note</span><input type=\"text\" id=\"pt-pnote\" maxlength=\"200\" value=\"" + Ui.Attr(v.Note) + "\" placeholder=\"For example: prefers mornings, needs step-free access\"" + ro + "></label><p class=\"pt-hint\">Contact and admin details only. Keep clinical notes in your clinical record system.</p><em class=\"pt-err\" id=\"pt-e-pt\"></em></div>");
            if (p != null)
            {
                List<Appointment> l = site.Appointments.Where(a => a.Patient == p.Id).OrderByDescending(a => a.Start).ToList();
                bool open = Access.Can(c.U, "appointments", 'V');
                sb.Append("<h3>Visits</h3>");
                if (l.Count == 0)
                {
                    sb.Append("<p class=\"pt-none\">No visits yet.</p>");
                }
                else
                {
                    sb.Append("<div class=\"pt-vis\">");
                    foreach (Appointment a in l.Take(60))
                    {
                        string tag = open ? "a" : "div";
                        sb.Append("<" + tag + " class=\"pt-vrow\"" + (open ? " href=\"AdminAppointments?a=" + Uri.EscapeDataString(a.Id) + "\"" : string.Empty) + "><span><b>" + Ui.Day(a.Start, a.Start.Year != e.Today.Year) + "</b><small>" + Ui.Hm(site, a.Start) + "</small></span><span><b>" + Ui.Enc(Ui.ServiceName(site, a.Service)) + "</b><small>" + Ui.Enc(Ui.ProviderName(site, a.Provider)) + "</small></span>" + Ui.StatusPill("pt", a) + "</" + tag + ">");
                    }
                    sb.Append("</div>");
                }
            }
            sb.Append("<div class=\"pt-mfoot\">" + (p != null && Access.Can(c.U, "appointments", 'E') ? "<a class=\"pt-btn pt-btn-ghost\" href=\"AdminAppointments?new=" + Uri.EscapeDataString(p.Id) + "\">Book an appointment</a>" : string.Empty) + "<button type=\"button\" class=\"pt-btn pt-btn-ghost\" onclick=\"AdminPatientsJs.closeModal()\">Close</button>" + (edit ? "<button type=\"button\" class=\"pt-btn pt-btn-accent\" onclick=\"AdminPatientsJs.save('" + Ui.Js(p?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("pt-mbody", sb.ToString());
            response.ExecuteScript("AdminPatientsJs.openModal();times(document.getElementById('pt-mbody'));");
            return response;
        }

        public async Task<ApiResponse> SavePt()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPatientsJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "patient", id, "You can view but not change patient details.");
            }
            string first = Ui.Clip(GetDataValue("first"), 40);
            string last = Ui.Clip(GetDataValue("last"), 40);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string err = first == string.Empty || last == string.Empty ? "Enter the first and last name." : email != string.Empty && !Ui.Email(email) ? "Check the email address." : Ui.Digits(phone).Length < 7 && email == string.Empty ? "Enter a phone number or an email address." : string.Empty;
            if (err != string.Empty)
            {
                response.SetElementContents("pt-e-pt", err);
                return response;
            }
            string name = first + " " + last;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Patient? p = site.Patients.FirstOrDefault(x => x.Id == id);
                if (p == null)
                {
                    if (id != string.Empty)
                    {
                        return response;
                    }
                    p = new Patient { Id = Codes.Id("pt"), Created = DateTime.UtcNow };
                    site.Patients.Add(p);
                }
                p.First = first;
                p.Last = last;
                p.Phone = phone;
                p.Email = email;
                p.Contact = GetDataValue("contact") is "phone" or "email" ? GetDataValue("contact") : "text";
                string ins = Ui.Clip(GetDataValue("ins"), 60);
                p.Insurance = ins == string.Empty || site.Settings.Insurance.Contains(ins) || ins == p.Insurance ? ins : p.Insurance;
                p.Note = Ui.Clip(GetDataValue("note"), 200);
                id = p.Id;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "patient", name, true);
            response.ExecuteScript("AdminPatientsJs.closeModal();AdminPatientsJs.list(1);AdminPatientsJs.toast('Saved');");
            return response;
        }
    }
}
