using System;
using WorkdayCalendarSync.Models;

namespace WorkdayCalendarSync.ExtractAndUpdate
{
    public interface IWorkdayExtractor
    {
        WorkdayRootObject FetchWorkdayRootObject();
        // The TimeOffEntry strings should be in this format: yyyy-MM-dd
        string GetEventId(string employeeName, string eventType, string startTimeOffEntry, string endTimeOffEntry);
    }
}
