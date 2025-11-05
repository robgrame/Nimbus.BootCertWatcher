using System;

namespace SecureBootDashboard.Api.Data
{
    public sealed class SecureBootEventEntity
    {
        public Guid Id { get; set; }

        public Guid ReportId { get; set; }

        public SecureBootReportEntity? Report { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public int EventId { get; set; }

    public string? Level { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string? Message { get; set; }

    public string? RawXml { get; set; }
    }
}
