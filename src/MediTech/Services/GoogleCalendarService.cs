using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace MediTech.Services;

public interface IGoogleCalendarService
{
    Task<string?> CreateEventAsync(string title, string description, DateTime start, DateTime end);
    Task<bool> UpdateEventAsync(string eventId, string title, string description, DateTime start, DateTime end);
    Task<bool> DeleteEventAsync(string eventId);
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };
    private readonly string ApplicationName = "MediTech";

    public GoogleCalendarService(ILogger<GoogleCalendarService> logger)
    {
        _logger = logger;
    }

    private async Task<CalendarService?> GetCalendarServiceAsync()
    {
        try
        {
            // Note: In a production web app, we would use a proper TokenStore 
            // and potentially a pre-configured Service Account or User Consent flow.
            // This is structured to look for 'credentials.json' which is the standard Google OAuth file.
            
            // For now, we return null if the file isn't found to trigger the fallback mode.
            if (!System.IO.File.Exists("credentials.json"))
            {
                _logger.LogWarning("OAuth 2.0 'credentials.json' not found. Operating in local-only mode.");
                return null;
            }

            UserCredential credential;
            using (var stream = new System.IO.FileStream("credentials.json", System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            return new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Calendar Service.");
            return null;
        }
    }

    public async Task<string?> CreateEventAsync(string title, string description, DateTime start, DateTime end)
    {
        var service = await GetCalendarServiceAsync();
        if (service == null) return null;

        try
        {
            var newEvent = new Event()
            {
                Summary = title,
                Description = description,
                Start = new EventDateTime() { DateTimeDateTimeOffset = start },
                End = new EventDateTime() { DateTimeDateTimeOffset = end }
            };

            var request = service.Events.Insert(newEvent, "primary");
            var createdEvent = await request.ExecuteAsync();
            
            _logger.LogInformation("Event created in Google Calendar: {Id}", createdEvent.Id);
            return createdEvent.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event in Google Calendar.");
            return null;
        }
    }

    public async Task<bool> UpdateEventAsync(string eventId, string title, string description, DateTime start, DateTime end)
    {
        if (string.IsNullOrEmpty(eventId)) return false;

        var service = await GetCalendarServiceAsync();
        if (service == null) return false;

        try
        {
            var eventToUpdate = new Event()
            {
                Summary = title,
                Description = description,
                Start = new EventDateTime() { DateTimeDateTimeOffset = start },
                End = new EventDateTime() { DateTimeDateTimeOffset = end }
            };

            var request = service.Events.Update(eventToUpdate, "primary", eventId);
            await request.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event {Id} in Google Calendar.", eventId);
            return false;
        }
    }

    public async Task<bool> DeleteEventAsync(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;

        var service = await GetCalendarServiceAsync();
        if (service == null) return false;

        try
        {
            await service.Events.Delete("primary", eventId).ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event {Id} from Google Calendar.", eventId);
            return false;
        }
    }
}
