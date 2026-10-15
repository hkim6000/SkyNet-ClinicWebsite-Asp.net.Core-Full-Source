using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinic.Core;
using Clinic.Models;
using SkyNet;

namespace Clinic.codes
{
    public class AdminRooms : WebPage
    {
        private const string AreaKey = "rooms";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Rooms");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--rm-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("rm-menu", Auth.Menu("rm", "AdminRooms", c, site));
            response.SetElementContents("rm-who", Auth.Who("rm", c));
            response.SetElementContents("rm-demo", Auth.DemoPill("rm", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminRoomsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"rm-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"rm-panel\"><p>You don't have access to this area. Ask the clinic manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminRoomsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("rm-main", NoAccess("Rooms"));
                return response;
            }
            response.SetElementContents("rm-main", await MainHtml(c, site));
            response.ExecuteScript("AdminRoomsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"rm-tr rm-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Engine e = new Engine(site);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            (TimeSpan From, TimeSpan To)? open = e.Clinic(e.Today);
            double from = open?.From.TotalMinutes ?? 480;
            double to = open?.To.TotalMinutes ?? 1080;
            double span = Math.Max(60, to - from);
            List<Appointment> today = site.Appointments.Where(a => a.Start.Date == e.Today && a.Status is not ("cancelled" or "noshow")).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"rm-ptop\"><div><h1>" + Ui.Enc(site.Settings.RoomsWord) + "</h1><p>Where visits happen. Each " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) + " has a usual " + Ui.Enc(site.Settings.RoomWord.ToLowerInvariant()) + "; booking checks that two visits never share one at the same time.</p></div><div class=\"rm-acts\"><a class=\"rm-btn rm-btn-ghost\" href=\"AdminSchedule?view=room\">Today by " + Ui.Enc(site.Settings.RoomWord.ToLowerInvariant()) + "</a>" + (edit ? "<button type=\"button\" class=\"rm-btn rm-btn-accent\" onclick=\"AdminRoomsJs.edit('')\">Add " + Ui.Enc(site.Settings.RoomWord.ToLowerInvariant()) + "</button>" : string.Empty) + "</div></div><div class=\"rm-rcards\">");
            foreach (ClinicRoom r in site.Rooms.OrderByDescending(r => r.Active).ThenBy(r => r.Order))
            {
                List<Appointment> list = today.Where(a => a.Room == r.Id).OrderBy(a => a.Start).ToList();
                double used = list.Sum(a => (double)a.Minutes);
                Appointment? now = list.FirstOrDefault(a => a.Start <= e.Now && a.Start.AddMinutes(a.Minutes) > e.Now);
                Appointment? next = list.FirstOrDefault(a => a.Start > e.Now);
                List<Clinician> who = site.Providers.Where(p => p.Room == r.Id && p.Active).ToList();
                sb.Append("<article class=\"rm-rc\"><div class=\"rm-rch\"><div><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(r.Kind) + "</small></div>" + (r.Active ? "<span class=\"rm-st rm-st-on\">In use</span>" : "<span class=\"rm-st rm-st-off\">Closed</span>") + "</div>");
                sb.Append("<div class=\"rm-tl\" title=\"Today\">");
                foreach (Appointment a in list)
                {
                    double l = (a.Start.TimeOfDay.TotalMinutes - from) / span * 100;
                    double w = a.Minutes / span * 100;
                    sb.Append("<i style=\"left:" + Math.Max(0, l).ToString("0.##", Ui.Inv) + "%;width:" + w.ToString("0.##", Ui.Inv) + "%;--c:" + Ui.ColorOf(site, a) + "\"></i>");
                }
                if (open != null && e.Now.TimeOfDay.TotalMinutes >= from && e.Now.TimeOfDay.TotalMinutes <= to)
                {
                    sb.Append("<em style=\"left:" + ((e.Now.TimeOfDay.TotalMinutes - from) / span * 100).ToString("0.##", Ui.Inv) + "%\"></em>");
                }
                sb.Append("</div><div class=\"rm-tlx\"><span>" + Ui.Hm(site, TimeSpan.FromMinutes(from)) + "</span><span>" + (int)Math.Round(Math.Min(100, used / span * 100)) + "% booked today</span><span>" + Ui.Hm(site, TimeSpan.FromMinutes(to)) + "</span></div>");
                sb.Append("<p class=\"rm-rnow\">" + (now != null ? "Now: <b>" + Ui.Enc(Ui.ProviderName(site, now.Provider)) + "</b> until " + Ui.Hm(site, now.Start.AddMinutes(now.Minutes)) : next != null ? "Free now &middot; next at <b>" + Ui.Hm(site, next.Start) + "</b>" : "Free for the rest of today") + "</p>");
                sb.Append("<p class=\"rm-rcl\">" + (who.Count > 0 ? "Usual " + Ui.Enc(site.Settings.ProvidersWord.ToLowerInvariant()) + ": " + Ui.Enc(string.Join(", ", who.Select(p => p.Name))) : "No usual " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant())) + (r.Note != string.Empty ? " &middot; " + Ui.Enc(r.Note) : string.Empty) + "</p>" + (edit ? "<div><button type=\"button\" class=\"rm-btn rm-btn-sm rm-btn-ghost\" onclick=\"AdminRoomsJs.edit('" + Ui.Js(r.Id) + "')\">Edit</button></div>" : string.Empty) + "</article>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "room", "form", "You can view but not change rooms.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            ClinicRoom? r = site.Rooms.FirstOrDefault(x => x.Id == GetDataValue("id"));
            ClinicRoom v = r ?? new ClinicRoom { Order = site.Rooms.Count };
            string sb = "<h2>" + (r == null ? "Add " + Ui.Enc(site.Settings.RoomWord.ToLowerInvariant()) : Ui.Enc(r.Name)) + "</h2><div class=\"rm-form\"><div class=\"rm-f2\"><label class=\"rm-field\"><span>Name</span><input type=\"text\" id=\"rm-rname\" maxlength=\"40\" value=\"" + Ui.Attr(v.Name) + "\"></label><label class=\"rm-field\"><span>Kind</span><input type=\"text\" id=\"rm-rkind\" maxlength=\"40\" list=\"rm-kinds\" value=\"" + Ui.Attr(v.Kind) + "\">" + Ui.DataList("rm-kinds", site.Rooms.Select(x => x.Kind)) + "</label></div><div class=\"rm-f2\"><label class=\"rm-field\"><span>Note</span><input type=\"text\" id=\"rm-rnote\" maxlength=\"80\" value=\"" + Ui.Attr(v.Note) + "\" placeholder=\"For example: ground floor, has a hoist\"></label><label class=\"rm-field\"><span>Order</span><input type=\"number\" id=\"rm-rorder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"></label></div><label class=\"rm-chk\"><input type=\"checkbox\" id=\"rm-ractive\"" + (r == null || v.Active ? " checked" : string.Empty) + "> In use</label><em class=\"rm-err\" id=\"rm-e-room\"></em></div><div class=\"rm-mfoot\"><button type=\"button\" class=\"rm-btn rm-btn-ghost\" onclick=\"AdminRoomsJs.closeModal()\">Cancel</button><button type=\"button\" class=\"rm-btn rm-btn-accent\" onclick=\"AdminRoomsJs.save('" + Ui.Js(r?.Id ?? string.Empty) + "')\">Save</button></div>";
            response.SetElementContents("rm-mbody", sb);
            response.ExecuteScript("AdminRoomsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "room", GetDataValue("id"), "You can view but not change rooms.");
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("name"), 40), "\\s+", " ");
            if (name.Length < 2)
            {
                response.SetElementContents("rm-e-room", "Enter a name.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                ClinicRoom? r = site.Rooms.FirstOrDefault(x => x.Id == id);
                if (r == null)
                {
                    if (id != string.Empty)
                    {
                        return response;
                    }
                    int n = site.Rooms.Count + 1;
                    while (site.Rooms.Any(x => x.Id == "r-" + n))
                    {
                        n++;
                    }
                    r = new ClinicRoom { Id = "r-" + n };
                    site.Rooms.Add(r);
                }
                r.Name = name;
                r.Kind = Ui.Clip(GetDataValue("kind"), 40);
                r.Note = Ui.Clip(GetDataValue("note"), 80);
                r.Order = int.TryParse(GetDataValue("order"), out int ord) ? Math.Clamp(ord, 0, 99) : r.Order;
                r.Active = GetDataValue("active") == "1";
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "room", name, true);
            response.ExecuteScript("AdminRoomsJs.closeModal();AdminRoomsJs.list();AdminRoomsJs.toast('Saved');");
            return response;
        }
    }
}
