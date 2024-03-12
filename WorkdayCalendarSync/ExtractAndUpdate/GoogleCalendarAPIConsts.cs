namespace WorkdayCalendarSync.ExtractAndUpdate
{
    public class GoogleCalendarAPIConsts
    {
        public static readonly string[] OutOfOfficeStrings = new[] { "Out of office", "Out of Office", "Professional Development", "Work From Home" };
        public const string OutOfOfficeCalendarId = "pof.com_3934373331373138313838@resource.calendar.google.com";
        public const string UserEmail = "ooffice@Pof.com";
        public const int MaxResults = 2500;
        public const int MaxBatchRequests = 500;
    }
}
