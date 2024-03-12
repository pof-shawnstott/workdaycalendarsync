using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WorkdayCalendarSync.Models
{
    public class WorkdayRootObject
    {
        public List<ReportEntry> Report_Entry { get; set; }
    }
}
