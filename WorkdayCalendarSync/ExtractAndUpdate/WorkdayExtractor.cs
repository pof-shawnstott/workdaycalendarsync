using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using WorkdayCalendarSync.Models;

namespace WorkdayCalendarSync.ExtractAndUpdate
{
    public class WorkdayExtractor : IWorkdayExtractor
    {
        private readonly ILogger _logger;
        private readonly IConfigurationRoot _config;
        private string responseString = null;

        public WorkdayExtractor(ILogger<WorkdayExtractor> logger)
        {
            _logger = logger;
            _config = Startup.Configuration;
        }

        public WorkdayRootObject FetchWorkdayRootObject()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_config["URL"]);
                request.Credentials = new NetworkCredential(_config["WorkdayUsername"], _config["WorkdayPassword"]);
                WebResponse json = request.GetResponse();
                Stream stream = json.GetResponseStream();
                StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
                responseString = streamReader.ReadToEnd();
            }
            catch (Exception e)
            {
                WorkdayRootObject emptyObject = new WorkdayRootObject();
                emptyObject.Report_Entry = new List<ReportEntry>();
                return emptyObject;
            }

            return JsonConvert.DeserializeObject<WorkdayRootObject>(responseString);
        }

        public string GetEventId(string employeeName, string eventType, string startTimeOffEntry, string endTimeOffEntry)
        {
            return employeeName + " - " + eventType + " " + startTimeOffEntry + " " + endTimeOffEntry;
        }
    }
}
