namespace Clinic.Models
{
    public class SiteData
    {
        public DateTime Anchor { get; set; }
        public ClinicSettings Settings { get; set; } = new ClinicSettings();
        public List<Specialty> Specialties { get; set; } = new List<Specialty>();
        public List<CareService> Services { get; set; } = new List<CareService>();
        public List<Clinician> Providers { get; set; } = new List<Clinician>();
        public List<ClinicRoom> Rooms { get; set; } = new List<ClinicRoom>();
        public List<Patient> Patients { get; set; } = new List<Patient>();
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<Holiday> Holidays { get; set; } = new List<Holiday>();
        public List<Faq> Faqs { get; set; } = new List<Faq>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public Dictionary<string, ContentBlock> Content { get; set; } = new Dictionary<string, ContentBlock>();
    }

    public class ClinicSettings
    {
        public string Name { get; set; } = "Clinic";
        public string Tagline { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Urgent { get; set; } = string.Empty;
        public string Accent { get; set; } = "#1F7A74";
        public string Theme { get; set; } = "light";
        public string TimeZone { get; set; } = "America/New_York";
        public bool Clock24 { get; set; }
        public int SlotMinutes { get; set; } = 15;
        public int MinNoticeHours { get; set; } = 2;
        public int BookAheadDays { get; set; } = 60;
        public int CancelHours { get; set; } = 24;
        public bool ShowPrices { get; set; } = true;
        public string ProviderWord { get; set; } = "Provider";
        public string ProvidersWord { get; set; } = "Providers";
        public string RoomWord { get; set; } = "Room";
        public string RoomsWord { get; set; } = "Rooms";
        public string PatientWord { get; set; } = "Patient";
        public string PatientsWord { get; set; } = "Patients";
        public List<OpenDay> Hours { get; set; } = new List<OpenDay>();
        public List<string> Insurance { get; set; } = new List<string>();
    }

    public class OpenDay
    {
        public int Day { get; set; }
        public bool Open { get; set; }
        public string From { get; set; } = "08:00";
        public string To { get; set; } = "18:00";
    }

    public class Specialty
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Color { get; set; } = "#1F7A74";
        public int Order { get; set; }
        public bool Active { get; set; } = true;
        public List<Question> Questions { get; set; } = new List<Question>();
    }

    public class Question
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Kind { get; set; } = "yesno";
        public bool Required { get; set; }
    }

    public class CareService
    {
        public string Key { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Short { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Minutes { get; set; } = 30;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public List<string> Providers { get; set; } = new List<string>();
        public List<string> Expect { get; set; } = new List<string>();
        public bool Online { get; set; } = true;
        public bool NewOk { get; set; } = true;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class Clinician
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Credentials { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public List<string> Education { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public string Photo { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Color { get; set; } = "#1F7A74";
        public List<WorkDay> Hours { get; set; } = new List<WorkDay>();
        public List<TimeOff> TimeOff { get; set; } = new List<TimeOff>();
        public bool Accepting { get; set; } = true;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class WorkDay
    {
        public int Day { get; set; }
        public bool On { get; set; }
        public string From { get; set; } = "09:00";
        public string To { get; set; } = "17:00";
        public string BreakFrom { get; set; } = string.Empty;
        public string BreakTo { get; set; } = string.Empty;
    }

    public class TimeOff
    {
        public string Id { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ClinicRoom
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class Patient
    {
        public string Id { get; set; } = string.Empty;
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Contact { get; set; } = "phone";
        public string Insurance { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class Appointment
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Patient { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public int Minutes { get; set; } = 30;
        public string Status { get; set; } = "booked";
        public string Source { get; set; } = "web";
        public bool NewPatient { get; set; }
        public string Note { get; set; } = string.Empty;
        public string Desk { get; set; } = string.Empty;
        public List<Answer> Answers { get; set; } = new List<Answer>();
        public DateTime Created { get; set; }
        public string By { get; set; } = "web";
        public bool Seed { get; set; }
        public List<Hist> History { get; set; } = new List<Hist>();
    }

    public class Answer
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }

    public class Hist
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Holiday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Faq
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }

    public class Message
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Read { get; set; }
    }

    public class ContentBlock
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public List<ContentItem> Items { get; set; } = new List<ContentItem>();
    }

    public class ContentItem
    {
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public bool Own { get; set; }
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool Demo { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Activity
    {
        public DateTime When { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string Sbx { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Kind { get; set; } = "staff";
        public DateTime Exp { get; set; }
    }
}
