using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminServices : WebPage
    {
        private const string AreaKey = "services";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Services");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--ss-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ss-menu", Auth.Menu("ss", "AdminServices", c, site));
            response.SetElementContents("ss-who", Auth.Who("ss", c));
            response.SetElementContents("ss-demo", Auth.DemoPill("ss", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminServicesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ss-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ss-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminServicesJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ss-main", NoAccess("Services"));
                return response;
            }
            response.SetElementContents("ss-main", await MainHtml(c, site));
            response.ExecuteScript("AdminServicesJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ss-tr ss-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Slug(string v)
        {
            string s = Regex.Replace(Regex.Replace((v ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-"), "^-+|-+$", string.Empty);
            return s.Length > 30 ? s.Substring(0, 30).Trim('-') : s;
        }

        private static string Hhmm(string v)
        {
            return Clock.Hm(v) != null ? Clock.Text(Clock.Hm(v)!.Value) : string.Empty;
        }

        private const int Size = 10;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"ss-ptop\"><div><h1>Services</h1><p>What patients can book, how long each visit takes and who offers it. Teams group services on the website.</p></div><div class=\"ss-acts\">" + (edit ? "<button type=\"button\" class=\"ss-btn ss-btn-ghost\" onclick=\"AdminServicesJs.editTeam('')\">Add team</button><button type=\"button\" class=\"ss-btn ss-btn-accent\" onclick=\"AdminServicesJs.editSv('')\">Add service</button>" : string.Empty) + "</div></div><div class=\"ss-tabs\"><button type=\"button\" class=\"ss-tab ss-act\" onclick=\"AdminServicesJs.view(this,'sv')\">Services<em>" + site.Services.Count + "</em></button><button type=\"button\" class=\"ss-tab\" onclick=\"AdminServicesJs.view(this,'team')\">Teams<em>" + site.Specialties.Count + "</em></button></div><input type=\"hidden\" id=\"ss-tabv\" value=\"sv\"><div class=\"ss-bar\"><label>Search<input type=\"search\" id=\"ss-q\" placeholder=\"Name or description\" maxlength=\"60\" oninput=\"AdminServicesJs.search()\"></label><label id=\"ss-tml\">Team<select id=\"ss-tm\" onchange=\"AdminServicesJs.list(1)\"><option value=\"\">All teams</option>" + Ui.Opts(site.Specialties.OrderBy(x => x.Order).Select(x => (x.Key, x.Name)), string.Empty) + "</select></label></div><div id=\"ss-list\"></div><div id=\"ss-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            string q = Ui.Clip(GetDataValue("q"), 60);
            string team = Ui.Clip(GetDataValue("team"), 40);
            StringBuilder sb = new StringBuilder();
            string pager;
            if (GetDataValue("tab") == "team")
            {
                List<Specialty> teams = site.Specialties.Where(t => q == string.Empty || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Blurb.Contains(q, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Order).ToList();
                int tp = Ui.PageOf(GetDataValue("pg"), Size, teams.Count);
                pager = Ui.Pager("ss", tp, Size, teams.Count, "team", "teams", "AdminServicesJs.list");
                sb.Append("<div class=\"ss-tbl ss-tmtbl\">" + Head(new[] { ("Team", string.Empty), ("Services", "ss-num"), (site.Settings.ProvidersWord, "ss-num"), ("Questions", "ss-num"), ("Status", string.Empty), (string.Empty, string.Empty) }));
                if (teams.Count == 0)
                {
                    sb.Append("<div class=\"ss-tr\"><span class=\"ss-none ss-wide\">No teams match.</span></div>");
                }
                foreach (Specialty t in teams.Skip((tp - 1) * Size).Take(Size))
                {
                    sb.Append("<div class=\"ss-tr\"><span class=\"ss-wide\"><b><i class=\"ss-dot\" style=\"background:" + Ui.Attr(t.Color) + "\"></i>" + Ui.Enc(t.Name) + "</b><small>" + Ui.Enc(t.Blurb) + "</small></span><span data-l=\"Services\" class=\"ss-num\">" + site.Services.Count(s => s.Specialty == t.Key) + "</span><span data-l=\"" + Ui.Attr(site.Settings.ProvidersWord) + "\" class=\"ss-num\">" + site.Providers.Count(p => p.Specialty == t.Key) + "</span><span data-l=\"Questions\" class=\"ss-num\">" + t.Questions.Count + "</span><span data-l=\"Status\"><span class=\"ss-st ss-st-" + (t.Active ? "on\">Shown" : "off\">Hidden") + "</span></span><span><button type=\"button\" class=\"ss-btn ss-btn-sm ss-btn-ghost\" onclick=\"AdminServicesJs.editTeam('" + Ui.Js(t.Key) + "')\">" + (edit ? "Edit" : "View") + "</button></span></div>");
                }
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<div class=\"ss-tbl ss-svtbl\">" + Head(new[] { ("Service", string.Empty), ("Team", string.Empty), ("Length", string.Empty), ("Price", "ss-num"), ("Online", string.Empty), (site.Settings.ProvidersWord, "ss-num"), (string.Empty, string.Empty) }));
                List<Specialty> specs = site.Specialties.OrderBy(x => x.Order).ToList();
                List<CareService> list = site.Services.Where(x => (team == string.Empty || x.Specialty == team) && (q == string.Empty || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Short.Contains(q, StringComparison.OrdinalIgnoreCase))).OrderBy(x => specs.FindIndex(t => t.Key == x.Specialty)).ThenBy(x => x.Order).ToList();
                int sp = Ui.PageOf(GetDataValue("pg"), Size, list.Count);
                pager = Ui.Pager("ss", sp, Size, list.Count, "service", "services", "AdminServicesJs.list");
                if (list.Count == 0)
                {
                    sb.Append("<div class=\"ss-tr\"><span class=\"ss-none ss-wide\">No services match.</span></div>");
                }
                foreach (CareService s in list.Skip((sp - 1) * Size).Take(Size))
                {
                    Specialty? t = Ui.SpecialtyOf(site, s.Specialty);
                    sb.Append("<div class=\"ss-tr\"" + (s.Active ? string.Empty : " style=\"opacity:.55\"") + "><span class=\"ss-wide\"><b>" + Ui.Enc(s.Name) + (s.Active ? string.Empty : " (hidden)") + "</b><small>" + Ui.Enc(s.Short) + "</small></span><span data-l=\"Team\"><i class=\"ss-dot\" style=\"background:" + Ui.Attr(t?.Color ?? "#999999") + "\"></i>" + Ui.Enc(t?.Name ?? s.Specialty) + "</span><span data-l=\"Length\">" + Ui.Dur(s.Minutes) + "</span><span data-l=\"Price\" class=\"ss-num\">" + (s.Price > 0 ? Ui.Money(s.Price) : "&mdash;") + "</span><span data-l=\"Online\">" + (s.Online ? "Yes" : "Phone only") + "</span><span data-l=\"" + Ui.Attr(site.Settings.ProvidersWord) + "\" class=\"ss-num\">" + s.Providers.Count(id => site.Providers.Any(p => p.Id == id && p.Active)) + "</span><span><button type=\"button\" class=\"ss-btn ss-btn-sm ss-btn-ghost\" onclick=\"AdminServicesJs.editSv('" + Ui.Js(s.Key) + "')\">" + (edit ? "Edit" : "View") + "</button></span></div>");
                }
                sb.Append("</div>");
            }
            response.SetElementContents("ss-list", sb.ToString());
            response.SetElementContents("ss-pager", pager);
            return response;
        }

        public async Task<ApiResponse> SvForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            CareService? s = site.Services.FirstOrDefault(x => x.Key == GetDataValue("k"));
            bool edit = Access.Can(c.U, AreaKey, 'E');
            if (s == null && !edit)
            {
                return response;
            }
            CareService v = s ?? new CareService { Specialty = site.Specialties.FirstOrDefault()?.Key ?? string.Empty, Order = site.Services.Count };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            List<(string, string)> mins = Enumerable.Range(1, 48).Select(i => ((i * 5).ToString(Ui.Inv), Ui.Dur(i * 5))).ToList();
            StringBuilder sb = new StringBuilder("<h2>" + (s == null ? "Add a service" : Ui.Enc(s.Name)) + "</h2><div class=\"ss-form\"><div class=\"ss-f2\"><label class=\"ss-field\"><span>Name</span><input type=\"text\" id=\"ss-sname\" maxlength=\"60\" value=\"" + Ui.Attr(v.Name) + "\"" + ro + "></label><label class=\"ss-field\"><span>Team</span><select id=\"ss-steam\"" + dis + ">" + Ui.Opts(site.Specialties.OrderBy(x => x.Order).Select(x => (x.Key, x.Name)), v.Specialty) + "</select></label></div>");
            sb.Append("<label class=\"ss-field\"><span>Short line (cards and lists)</span><input type=\"text\" id=\"ss-sshort\" maxlength=\"100\" value=\"" + Ui.Attr(v.Short) + "\"" + ro + "></label><label class=\"ss-field\"><span>Description</span><textarea id=\"ss-stext\" rows=\"3\" maxlength=\"800\"" + ro + ">" + Ui.Enc(v.Text) + "</textarea></label>");
            sb.Append("<div class=\"ss-f3\"><label class=\"ss-field\"><span>Length</span><select id=\"ss-smin\"" + dis + ">" + Ui.Opts(mins, v.Minutes.ToString(Ui.Inv)) + "</select></label><label class=\"ss-field\"><span>Price from ($, 0 = hide)</span><input type=\"number\" id=\"ss-sprice\" min=\"0\" max=\"99999\" step=\"1\" value=\"" + v.Price.ToString("0.##", Ui.Inv) + "\"" + ro + "></label><label class=\"ss-field\"><span>Order</span><input type=\"number\" id=\"ss-sorder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"" + ro + "></label></div>");
            sb.Append("<label class=\"ss-field\"><span>What to expect (one per line)</span><textarea id=\"ss-sexp\" rows=\"4\" maxlength=\"800\"" + ro + ">" + Ui.Enc(Ui.Lines(v.Expect)) + "</textarea></label><div class=\"ss-f3\"><label class=\"ss-chk\"><input type=\"checkbox\" id=\"ss-sonline\"" + (v.Online ? " checked" : string.Empty) + dis + "> Bookable online</label><label class=\"ss-chk\"><input type=\"checkbox\" id=\"ss-snew\"" + (v.NewOk ? " checked" : string.Empty) + dis + "> New patients can book</label><label class=\"ss-chk\"><input type=\"checkbox\" id=\"ss-sactive\"" + (s == null || v.Active ? " checked" : string.Empty) + dis + "> Show on the website</label></div>");
            sb.Append("<div><span class=\"ss-hint\">Who offers it</span><div class=\"ss-cbs\" id=\"ss-sprov\">");
            foreach (Clinician p in site.Providers.OrderBy(p => p.Order))
            {
                sb.Append("<label><input type=\"checkbox\" value=\"" + Ui.Attr(p.Id) + "\"" + (v.Providers.Contains(p.Id) ? " checked" : string.Empty) + dis + "> " + Ui.Enc(p.Name) + " <small>" + Ui.Enc(p.Title) + (p.Active ? string.Empty : " &middot; hidden") + "</small></label>");
            }
            sb.Append("</div></div><div><span class=\"ss-hint\">Picture</span>" + Ui.Picker("ss", "ss-simg", "full", v.Image, !edit, false) + "</div><em class=\"ss-err\" id=\"ss-e-sv\" role=\"alert\"></em></div><div class=\"ss-mfoot\"><button type=\"button\" class=\"ss-btn ss-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (edit ? "<button type=\"button\" class=\"ss-btn ss-btn-accent\" onclick=\"AdminServicesJs.saveSv('" + Ui.Js(s?.Key ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ss-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> SaveSv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "service", GetDataValue("k"), "You can view but not change services.");
            }
            string k = Ui.Clip(GetDataValue("k"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("name"), 60), "\\s+", " ");
            int min = int.TryParse(GetDataValue("min"), out int m) ? m : 0;
            if (name.Length < 2)
            {
                response.SetElementContents("ss-e-sv", "Enter a name.");
                return response;
            }
            if (min < 5 || min > 240 || min % 5 != 0)
            {
                response.SetElementContents("ss-e-sv", "Choose a length.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                CareService? s = site.Services.FirstOrDefault(x => x.Key == k);
                if (s == null)
                {
                    if (k != string.Empty)
                    {
                        return response;
                    }
                    string slug = Slug(name);
                    string nk = slug == string.Empty ? "service" : slug;
                    for (int i = 2; site.Services.Any(x => x.Key == nk); i++)
                    {
                        nk = slug + "-" + i;
                    }
                    s = new CareService { Key = nk };
                    site.Services.Add(s);
                }
                s.Name = name;
                s.Specialty = site.Specialties.Any(t => t.Key == GetDataValue("team")) ? GetDataValue("team") : s.Specialty;
                s.Short = Ui.Clip(GetDataValue("short"), 100);
                s.Text = Ui.Clip(GetDataValue("text"), 800);
                s.Minutes = min;
                s.Price = decimal.TryParse(GetDataValue("price"), System.Globalization.NumberStyles.Number, Ui.Inv, out decimal pr) ? Math.Round(Math.Clamp(pr, 0, 99999), 2) : s.Price;
                s.Order = int.TryParse(GetDataValue("order"), out int ord) ? Math.Clamp(ord, 0, 99) : s.Order;
                s.Expect = Ui.SplitLines(GetDataValue("expect"), 8, 120);
                string img = GetDataValue("img");
                s.Image = Access.SafeImage(img) && Ui.Images("full").Contains(img) ? img : s.Image;
                s.Providers = GetDataValue("provs").Split(',').Where(id => site.Providers.Any(p => p.Id == id)).Distinct().ToList();
                s.Online = GetDataValue("online") == "1";
                s.NewOk = GetDataValue("newok") == "1";
                s.Active = GetDataValue("active") == "1";
                k = s.Key;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "service", name, true);
            response.ExecuteScript("AdminServicesJs.closeModal();AdminServicesJs.list();AdminServicesJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> TeamForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Specialty? t = site.Specialties.FirstOrDefault(x => x.Key == GetDataValue("k"));
            bool edit = Access.Can(c.U, AreaKey, 'E');
            if (t == null && !edit)
            {
                return response;
            }
            Specialty v = t ?? new Specialty { Order = site.Specialties.Count, Color = "#2F8FB0" };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            string row = "<div class=\"ss-qrow\" data-id=\"{id}\"><input type=\"text\" maxlength=\"120\" value=\"{text}\" placeholder=\"Question for patients\" aria-label=\"Question\"" + ro + "><select aria-label=\"Answer type\"" + dis + "><option value=\"yesno\"{yn}>Yes or no</option><option value=\"text\"{tx}>Short answer</option></select><label class=\"ss-chk\"><input type=\"checkbox\"{rq}" + dis + "> Required</label>" + (edit ? "<button type=\"button\" class=\"ss-ibtn\" aria-label=\"Remove question\" onclick=\"AdminServicesJs.dropQ(this)\">&times;</button>" : "<span></span>") + "</div>";
            StringBuilder sb = new StringBuilder("<h2>" + (t == null ? "Add a team" : Ui.Enc(t.Name)) + "</h2><div class=\"ss-form\"><div class=\"ss-f3\"><label class=\"ss-field\"><span>Name</span><input type=\"text\" id=\"ss-tname\" maxlength=\"40\" value=\"" + Ui.Attr(v.Name) + "\"" + ro + "></label><label class=\"ss-field\"><span>Colour</span><input type=\"color\" id=\"ss-tcolor\" value=\"" + Ui.Attr(Ui.SafeColor(v.Color) ? v.Color : "#2F8FB0") + "\"" + dis + "></label><label class=\"ss-field\"><span>Order</span><input type=\"number\" id=\"ss-torder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"" + ro + "></label></div><label class=\"ss-field\"><span>One-line description</span><input type=\"text\" id=\"ss-tblurb\" maxlength=\"120\" value=\"" + Ui.Attr(v.Blurb) + "\"" + ro + "></label><label class=\"ss-chk\"><input type=\"checkbox\" id=\"ss-tactive\"" + (t == null || v.Active ? " checked" : string.Empty) + dis + "> Show on the website</label>");
            sb.Append("<div><span class=\"ss-hint\">Booking questions (asked when someone books online with this team). Keep them practical; don't ask for medical details.</span><div class=\"ss-qs\" id=\"ss-qs\" style=\"margin-top:8px\">");
            foreach (Question q in v.Questions)
            {
                sb.Append(row.Replace("{id}", Ui.Attr(q.Id)).Replace("{text}", Ui.Attr(q.Text)).Replace("{yn}", q.Kind == "yesno" ? " selected" : string.Empty).Replace("{tx}", q.Kind == "text" ? " selected" : string.Empty).Replace("{rq}", q.Required ? " checked" : string.Empty));
            }
            sb.Append("</div>" + (edit ? "<button type=\"button\" class=\"ss-btn ss-btn-sm ss-btn-ghost\" style=\"margin-top:8px\" onclick=\"AdminServicesJs.addQ()\">Add question</button><template id=\"ss-qtpl\">" + row.Replace("{id}", string.Empty).Replace("{text}", string.Empty).Replace("{yn}", string.Empty).Replace("{tx}", string.Empty).Replace("{rq}", string.Empty) + "</template>" : string.Empty) + "</div><div><span class=\"ss-hint\">Picture</span>" + Ui.Picker("ss", "ss-timg", "full", v.Image, !edit, false) + "</div><em class=\"ss-err\" id=\"ss-e-tm\" role=\"alert\"></em></div><div class=\"ss-mfoot\"><button type=\"button\" class=\"ss-btn ss-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (edit ? "<button type=\"button\" class=\"ss-btn ss-btn-accent\" onclick=\"AdminServicesJs.saveTeam('" + Ui.Js(t?.Key ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ss-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> SaveTeam()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "team", GetDataValue("k"), "You can view but not change teams.");
            }
            string k = Ui.Clip(GetDataValue("k"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("name"), 40), "\\s+", " ");
            if (name.Length < 2)
            {
                response.SetElementContents("ss-e-tm", "Enter a name.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Specialty? t = site.Specialties.FirstOrDefault(x => x.Key == k);
                if (t == null)
                {
                    if (k != string.Empty)
                    {
                        return response;
                    }
                    string slug = Slug(name);
                    string nk = slug == string.Empty ? "team" : slug;
                    for (int i = 2; site.Specialties.Any(x => x.Key == nk); i++)
                    {
                        nk = slug + "-" + i;
                    }
                    t = new Specialty { Key = nk };
                    site.Specialties.Add(t);
                }
                t.Name = name;
                t.Blurb = Ui.Clip(GetDataValue("blurb"), 120);
                t.Color = Ui.SafeColor(GetDataValue("color")) ? GetDataValue("color") : t.Color;
                t.Order = int.TryParse(GetDataValue("order"), out int ord) ? Math.Clamp(ord, 0, 99) : t.Order;
                t.Active = GetDataValue("active") == "1";
                string img = GetDataValue("img");
                t.Image = Access.SafeImage(img) && Ui.Images("full").Contains(img) ? img : t.Image;
                List<Question> qs = new List<Question>();
                foreach (string part in GetDataValue("qs").Split('~').Take(6))
                {
                    string[] x = part.Split('|');
                    if (x.Length != 4 || Ui.Clip(x[1], 120) == string.Empty)
                    {
                        continue;
                    }
                    string qid = t.Questions.Any(q => q.Id == x[0]) ? x[0] : Codes.Id("q");
                    qs.Add(new Question { Id = qid, Text = Ui.Clip(x[1], 120), Kind = x[2] == "text" ? "text" : "yesno", Required = x[3] == "1" });
                }
                t.Questions = qs;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "team", name, true);
            response.ExecuteScript("AdminServicesJs.closeModal();AdminServicesJs.list();AdminServicesJs.toast('Saved');");
            return response;
        }
    }
}
