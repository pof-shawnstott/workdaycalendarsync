using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Google.Apis.Calendar.v3.EventsResource;

namespace WorkdayCalendarSync.ExtractAndUpdate
{
    public class GoogleCalendarAPI : IGoogleCalendarAPI
    {
        private static string[] Scopes = { CalendarService.Scope.Calendar };
        private static string ApplicationName = "Google Calendar API .NET";

        private readonly ILogger _logger;
        private CalendarService _calendarService { get; set; }
        private readonly CalendarListEntry _calendarListEntry;

        public GoogleCalendarAPI(ILogger<GoogleCalendarAPI> logger)
        {
            _logger = logger;

            GoogleCredential googleCredential;
            var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
            var pathToContentRoot = Path.GetDirectoryName(pathToExe);
            string pathToSecret = null;

            if (Program.isService)
            {
                pathToSecret = pathToContentRoot + "\\googleSecret.json";
            } else
            {
                pathToSecret = "googleSecret.json";
            }

            using (var stream =
                new FileStream(pathToSecret, FileMode.Open, FileAccess.Read))
                {
                    googleCredential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
                    googleCredential = googleCredential.CreateWithUser(GoogleCalendarAPIConsts.UserEmail);
                    _calendarService = new CalendarService(new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = googleCredential,
                        ApplicationName = ApplicationName,
                    });
                }
            
            _calendarListEntry = _calendarService.CalendarList.Get(GoogleCalendarAPIConsts.OutOfOfficeCalendarId).Execute();
            
        }

        private void QueueAddCalendarEventToBatchRequest(Event e, BatchRequest batchRequest)
        {
            if (e == null || batchRequest == null)
            {
                _logger.LogError($"Null param passed into {nameof(QueueAddCalendarEventToBatchRequest)}");
                return;
            }

            batchRequest.Queue<Event>(_calendarService.Events.Insert(e, _calendarListEntry.Id),
                (content, error, i, message) =>
                {
                    if (error != null)
                    {
                        _logger.LogError(error.Code + ": " + error.Message + " Name: " + e.Summary + ", Date: " + e.Start.Date);
                    }
                    else if (content != null)
                    {
                        _logger.LogInformation("Event has been sucessfully published. The event id is: " + content.Id);
                    }
                });
        }

        private void QueueDeleteCalendarEventToBatchRequest(Event e, BatchRequest batchRequest)
        {
            if (e == null || batchRequest == null)
            {
                _logger.LogError($"Null param passed into {nameof(QueueDeleteCalendarEventToBatchRequest)}");
                return;
            }

            batchRequest.Queue<Event>(_calendarService.Events.Delete(_calendarListEntry.Id, e.Id),
                (content, error, i, message) =>
                {
                    if (error != null)
                    {
                        _logger.LogError(error.Code + ": " + error.Message);
                    } else 
                    {
                        _logger.LogInformation("Successfully deleted event. The event id is: " + e.Id);
                    }
                });
        }

        public IEnumerable<Event> GetOutOfOfficeEvents()
        {
            string pageToken = null;
            do
            {
                ListRequest listRequest = _calendarService.Events.List(_calendarListEntry.Id);
                listRequest.MaxResults = GoogleCalendarAPIConsts.MaxResults;
                listRequest.PageToken = pageToken;
                Events events = listRequest.Execute();

                foreach(var outOfOfficeEvent in events.Items.Where(EventContainsOutOfOfficeString))
                {
                    yield return outOfOfficeEvent;
                }

                pageToken = events.NextPageToken;
            } while (pageToken != null);
        }

        private bool EventContainsOutOfOfficeString(Event e)
        {
            return e.Summary != null && GoogleCalendarAPIConsts.OutOfOfficeStrings.Any(s => e.Summary.Contains(s));
        }

        public async Task AddGoogleEvents(IEnumerable<Event> googleCalendarEvents)
        {
            foreach (var batch in Batch(googleCalendarEvents, GoogleCalendarAPIConsts.MaxBatchRequests))
            {
                BatchRequest batchRequest = new BatchRequest(_calendarService);
                foreach (var e in batch)
                {
                    QueueAddCalendarEventToBatchRequest(e, batchRequest);
                }
                await batchRequest.ExecuteAsync();
            }
        }

        public async Task DeleteGoogleEvents(IEnumerable<Event> googleCalendarEvents)
        {
            foreach(var batch in Batch(googleCalendarEvents, GoogleCalendarAPIConsts.MaxBatchRequests))
            {
                BatchRequest batchRequest = new BatchRequest(_calendarService);
                foreach (var e in batch)
                {
                    QueueDeleteCalendarEventToBatchRequest(e, batchRequest);
                }
                await batchRequest.ExecuteAsync();
            }
        }

        private IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> elements, int batchSize)
        {
            var batch = elements.Take(batchSize);
            var batches = 0;
            while(batch.Any())
            {
                yield return batch;
                batches++;
                batch = elements.Skip(batches * batchSize).Take(batchSize);
            }
        }
    }
}
