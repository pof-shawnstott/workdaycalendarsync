namespace WorkdayCalendarSync.Services
{
    public interface ISyncingService
    {
        void Start();
        void StartProcessing();
        void StopProcessing();
    }
}
