using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Requests;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WorkdayCalendarSync.ExtractAndUpdate
{
    public interface IGoogleCalendarAPI
    {
        IEnumerable<Event> GetOutOfOfficeEvents();
        Task AddGoogleEvents(IEnumerable<Event> googleCalendarEvents);
        Task DeleteGoogleEvents(IEnumerable<Event> googleCalendarEvents);
    }
}
