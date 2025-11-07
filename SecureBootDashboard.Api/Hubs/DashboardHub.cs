using Microsoft.AspNetCore.SignalR;

namespace SecureBootDashboard.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time dashboard updates.
/// Broadcasts device status changes, compliance updates, and report notifications.
/// </summary>
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to device updates for a specific device.
    /// </summary>
    public async Task SubscribeToDevice(Guid deviceId)
    {
        var groupName = $"device-{deviceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to device {DeviceId}", Context.ConnectionId, deviceId);
    }

    /// <summary>
    /// Unsubscribe from device updates for a specific device.
    /// </summary>
    public async Task UnsubscribeFromDevice(Guid deviceId)
    {
        var groupName = $"device-{deviceId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from device {DeviceId}", Context.ConnectionId, deviceId);
    }

    /// <summary>
    /// Subscribe to global dashboard updates (compliance metrics, device counts).
    /// </summary>
    public async Task SubscribeToDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Client {ConnectionId} subscribed to dashboard updates", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from global dashboard updates.
    /// </summary>
    public async Task UnsubscribeFromDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from dashboard updates", Context.ConnectionId);
    }

    /// <summary>
    /// Send a ping to test connection.
    /// </summary>
    public Task<string> Ping()
    {
        _logger.LogDebug("Ping received from {ConnectionId}", Context.ConnectionId);
        return Task.FromResult("pong");
    }
}

/// <summary>
/// Extension methods for DashboardHub to broadcast updates.
/// </summary>
public static class DashboardHubExtensions
{
    /// <summary>
    /// Broadcast device status update to subscribed clients.
    /// </summary>
    public static async Task BroadcastDeviceUpdate(
        this IHubContext<DashboardHub> hubContext,
        Guid deviceId,
        string machineName,
        string deploymentState,
        DateTimeOffset lastSeenUtc)
    {
        var groupName = $"device-{deviceId}";
        await hubContext.Clients.Group(groupName).SendAsync("DeviceUpdated", new
        {
            deviceId,
            machineName,
            deploymentState,
            lastSeenUtc,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Broadcast new report notification to subscribed clients.
    /// </summary>
    public static async Task BroadcastNewReport(
        this IHubContext<DashboardHub> hubContext,
        Guid deviceId,
        Guid reportId,
        string machineName)
    {
        var groupName = $"device-{deviceId}";
        await hubContext.Clients.Group(groupName).SendAsync("NewReportReceived", new
        {
            deviceId,
            reportId,
            machineName,
            timestamp = DateTimeOffset.UtcNow
        });

        // Also notify dashboard group
        await hubContext.Clients.Group("dashboard").SendAsync("NewReportReceived", new
        {
            deviceId,
            reportId,
            machineName,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Broadcast compliance metrics update to dashboard clients.
    /// </summary>
    public static async Task BroadcastComplianceUpdate(
        this IHubContext<DashboardHub> hubContext,
        int totalDevices,
        int compliantDevices,
        int pendingDevices,
        int errorDevices,
        int activeDevices,
        int inactiveDevices)
    {
        await hubContext.Clients.Group("dashboard").SendAsync("ComplianceUpdated", new
        {
            totalDevices,
            compliantDevices,
            pendingDevices,
            errorDevices,
            activeDevices,
            inactiveDevices,
            compliancePercentage = totalDevices > 0
                ? Math.Round((double)compliantDevices / totalDevices * 100, 2)
                : 0,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Broadcast device count update to dashboard clients.
    /// </summary>
    public static async Task BroadcastDeviceCountUpdate(
        this IHubContext<DashboardHub> hubContext,
        int totalDevices)
    {
        await hubContext.Clients.Group("dashboard").SendAsync("DeviceCountUpdated", new
        {
            totalDevices,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Broadcast alert notification to all connected clients.
    /// </summary>
    public static async Task BroadcastAlert(
        this IHubContext<DashboardHub> hubContext,
        string alertType,
        string message,
        string severity)
    {
        await hubContext.Clients.All.SendAsync("AlertReceived", new
        {
            alertType,
            message,
            severity,
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
