using System.Collections.Generic;

namespace WorkdayCalendarSync.Models
{
    public class ReportEntry
    {
        public string Name { get; set; }
        public List<TimeOffRequestGroup> Time_Off_Request_group { get; set; }
    }
}
