using System;
using System.Runtime.Serialization;

namespace WorkdayCalendarSync.Models
{
    public class TimeOffRequestGroup
    {
        public DateTime Time_Off_Entry { get; set; }
        public string Time_Off_Type { get; set; }
    }
}
