using Google.Apis.Calendar.v3.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using WorkdayCalendarSync.ExtractAndUpdate;
using WorkdayCalendarSync.Models;

namespace WorkdayCalendarSync.Services
{
    public class SyncingService : ISyncingService
    {
        private readonly ILogger<SyncingService> _logger;
        private readonly IGoogleCalendarAPI _googleCalendarAPI;
        private readonly IWorkdayExtractor _workdayExtractor;

        private readonly int _secondsInAMinute = 60;
        private readonly int _minutesInAnHour = 60;
        private readonly int _millisecondsInASecond = 1000;
        private readonly Timer _timer;

        public SyncingService(ILogger<SyncingService> logger, IGoogleCalendarAPI googleCalendarAPI, IWorkdayExtractor workdayExtractor)
        {
            _logger = logger;
            _googleCalendarAPI = googleCalendarAPI;
            _workdayExtractor = workdayExtractor;
            _timer = new Timer(_millisecondsInASecond * _secondsInAMinute *  _minutesInAnHour);
            ElapsedEventHandler handler = new ElapsedEventHandler(OnTimedEvent);
            _timer.Elapsed += handler;
        }

        public void Start()
        {
            Task.Run(() => { StartProcessing(); });
        }

        public void StartProcessing()
        {
            _logger.LogInformation("StartProcessing() called");
            SyncWorkdayAndGoogle();
            _timer.Start();
        }

        public void StopProcessing()
        {
            _timer.Stop();
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                SyncWorkdayAndGoogle();
            } catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        public async void SyncWorkdayAndGoogle()
        {
            try
            {
                _logger.LogInformation("Started hourly sync.");

                var googleEventList = _googleCalendarAPI.GetOutOfOfficeEvents().ToList();
                var googleEventsDictionary = BuildEventDictionary(googleEventList);

                var workdayNameDateIds = new HashSet<string>();
                var workdayRootObject = _workdayExtractor.FetchWorkdayRootObject();

                if (workdayRootObject.Report_Entry.Count == 0)
                {
                    return;
                }

                await _googleCalendarAPI.AddGoogleEvents(GenerateGoogleEventsUsingWorkdayEntries(googleEventsDictionary, workdayNameDateIds, workdayRootObject).ToList());
                await _googleCalendarAPI.DeleteGoogleEvents(GetGoogleEventsToDelete(googleEventList, googleEventsDictionary, workdayNameDateIds).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private Dictionary<string, Event> BuildEventDictionary(IEnumerable<Event> googleEventList)
        {
            var dict = new Dictionary<string, Event>();
            foreach(var e in googleEventList)
            {
                var key = MakeGoogleEventKey(e);
                if(!dict.ContainsKey(key))
                {
                    dict.Add(key, e);
                }
            }
            return dict;
        }

        //Iterate over Workday entries for each employee to create Google events.
        public IEnumerable<Event> GenerateGoogleEventsUsingWorkdayEntries(Dictionary<string, Event> googleEventsDictionary, HashSet<string> workdayNameDateIds, WorkdayRootObject workdayRootObject)
        {
            foreach (ReportEntry reportEntry in workdayRootObject.Report_Entry)
            {
                if (reportEntry.Time_Off_Request_group != null)
                {
                    foreach(var timeOffPeriod in FindTimeOffPeriods(reportEntry.Time_Off_Request_group))
                    {
                        var start = timeOffPeriod.Item1;
                        var end = timeOffPeriod.Item2;
                        var e = CreateGoogleEventFromWorkdayEntries(start.Time_Off_Entry, end.Time_Off_Entry, reportEntry.Name, end.Time_Off_Type, googleEventsDictionary, workdayNameDateIds);
                        if (e != null)
                        {
                            yield return e;
                        }
                    }
                }
            }
        }

        //Find sets of sequential bookings with identical time-off types and returns the first and last requests of each set
        private IEnumerable<(TimeOffRequestGroup, TimeOffRequestGroup)> FindTimeOffPeriods(IEnumerable<TimeOffRequestGroup> groups)
        {
            using (var e = groups.GetEnumerator())
            {
                for (bool more = e.MoveNext(); more;)
                {
                    var first = e.Current;
                    var last = e.Current;
                    while ((more = e.MoveNext()) && RequestsAreSequential(e.Current, last))
                        last = e.Current;
                    yield return (first, last);
                }
            }
        }

        private bool RequestsAreSequential(TimeOffRequestGroup r1, TimeOffRequestGroup r2)
        {
            return (r1.Time_Off_Entry - r2.Time_Off_Entry).TotalDays == 1 && r1.Time_Off_Type == r2.Time_Off_Type;
        }

        public Event CreateGoogleEventFromWorkdayEntries(DateTime startTime, DateTime endTime, string employeeName, string timeOffType, Dictionary<string, Event> googleEventsDictionary, HashSet<string> workdayNameDateIds)
        {
            if ((endTime - startTime).TotalDays >= 1)
            {
                endTime = endTime.AddDays(1);
            }

            string eventId = _workdayExtractor.GetEventId(employeeName, timeOffType, startTime.ToString("yyyy-MM-dd"), endTime.ToString("yyyy-MM-dd"));
            workdayNameDateIds.Add(eventId);

            if (!googleEventsDictionary.ContainsKey(eventId))
            {
                EventDateTime start = new EventDateTime();
                start.Date = startTime.ToString("yyyy-MM-dd");

                EventDateTime end = new EventDateTime();
                end.Date = endTime.ToString("yyyy-MM-dd");

                Event calendarEvent = new Event()
                {
                    Summary = employeeName + " - " + timeOffType,
                    Reminders = null,
                    Location = "Out of Office",
                    Start = start,
                    End = end
                };

                return calendarEvent;
            }

            return null;
        }

        public IEnumerable<Event> GetGoogleEventsToDelete(IEnumerable<Event> googleEventsList, Dictionary<string, Event> googleEventsDictionary, HashSet<string> workdayNameDateIds)
        {
            var duplicateEvents = FindDuplicateGoogleEvents(googleEventsList);
            var eventsNotInWorkday = googleEventsDictionary
                .Where(kvp => !workdayNameDateIds.Contains(kvp.Key))
                .Select(kvp => kvp.Value);

            return duplicateEvents.Concat(eventsNotInWorkday);
        }

        public IEnumerable<Event> FindDuplicateGoogleEvents(IEnumerable<Event> googleCalendarEvents)
        {
            HashSet<string> eventKeysProcessed = new HashSet<string>();

            foreach (var e in googleCalendarEvents)
            {
                var key = MakeGoogleEventKey(e);
                if (eventKeysProcessed.Contains(key))
                {
                    yield return e;
                }

                eventKeysProcessed.Add(key);
            }
        }

        private string MakeGoogleEventKey(Event e)
        {
            return e.Summary + " " + DateStringFromEventDateTime(e.Start) + " " + DateStringFromEventDateTime(e.End);
        }

        private string DateStringFromEventDateTime(EventDateTime eventDateTime)
        {
            if(eventDateTime.Date != null)
            {
                return eventDateTime.Date;
            }
            else if(eventDateTime.DateTime.HasValue)
            {
                return eventDateTime.DateTime.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                throw new Exception("EventDateTime object has no date information");
            }
        }
    }
}
