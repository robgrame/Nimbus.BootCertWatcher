using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.GraphQL.Types;

/// <summary>
/// GraphQL type representing a Windows event related to Secure Boot.
/// </summary>
public class EventType
{
    public Guid Id { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public int EventId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string? Level { get; set; }

    public string? Message { get; set; }

    public string? RawXml { get; set; }

    public static EventType FromEntity(SecureBootEventEntity entity)
    {
        return new EventType
        {
            Id = entity.Id,
            ProviderName = entity.ProviderName,
            EventId = entity.EventId,
            TimestampUtc = entity.TimestampUtc,
            Level = entity.Level,
            Message = entity.Message,
            RawXml = entity.RawXml
        };
    }
}
