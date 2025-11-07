using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;
using SecureBootWatcher.Shared.Validation;

namespace SecureBootDashboard.Api.GraphQL.Mutations;

/// <summary>
/// GraphQL mutations for ingesting reports.
/// </summary>
public class Mutation
{
    /// <summary>
    /// Ingest a new Secure Boot status report.
    /// </summary>
    public async Task<IngestReportPayload> IngestReport(
        IngestReportInput input,
        [Service] IReportStore reportStore,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken)
    {
        if (input.Report == null)
        {
            return new IngestReportPayload
            {
                Success = false,
                Errors = new[] { "Report payload is null." }
            };
        }

        if (!ReportValidator.TryValidate(input.Report, out var errors))
        {
            return new IngestReportPayload
            {
                Success = false,
                Errors = errors.ToList()
            };
        }

        try
        {
            var id = await reportStore.SaveAsync(input.Report, cancellationToken);

            logger.LogInformation(
                "Successfully ingested report {ReportId} for device {MachineName}",
                id,
                input.Report.Device.MachineName);

            return new IngestReportPayload
            {
                Success = true,
                ReportId = id
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to ingest secure boot report for machine {Machine}",
                input.Report.Device.MachineName);

            return new IngestReportPayload
            {
                Success = false,
                Errors = new[] { "Failed to persist report." }
            };
        }
    }
}

/// <summary>
/// Input for ingesting a report.
/// </summary>
public class IngestReportInput
{
    public SecureBootStatusReport? Report { get; set; }
}

/// <summary>
/// Payload returned after ingesting a report.
/// </summary>
public class IngestReportPayload
{
    public bool Success { get; set; }

    public Guid? ReportId { get; set; }

    public IReadOnlyList<string>? Errors { get; set; }
}
