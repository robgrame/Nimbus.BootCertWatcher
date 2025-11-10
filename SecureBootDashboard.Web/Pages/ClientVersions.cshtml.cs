using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using SecureBootDashboard.Web.Models;
using SecureBootDashboard.Web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecureBootDashboard.Web.Pages
{
    public class ClientVersionsModel : PageModel
    {
        private readonly ISecureBootApiClient _apiClient;
        private readonly IConfiguration _configuration;

        public ClientVersionsModel(ISecureBootApiClient apiClient, IConfiguration configuration)
        {
            _apiClient = apiClient;
            _configuration = configuration;
        }

        public List<ClientVersionInfo> VersionGroups { get; set; } = new();
        public int TotalDevices { get; set; }
        public int OutdatedDevices { get; set; }
        public int UnsupportedDevices { get; set; }
        public int UpToDateDevices { get; set; }
        public int UnknownVersionDevices { get; set; }
        public string LatestVersion { get; set; } = "1.0.0.0";
        public string MinimumVersion { get; set; } = "1.0.0.0";
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Get version configuration from API settings
                LatestVersion = _configuration["ClientUpdate:LatestVersion"] ?? "1.0.0.0";
                MinimumVersion = _configuration["ClientUpdate:MinimumVersion"] ?? "1.0.0.0";

                var latestVer = Version.Parse(LatestVersion);
                var minimumVer = Version.Parse(MinimumVersion);

                // Fetch devices from API using the existing API client
                var devices = await _apiClient.GetDevicesAsync(HttpContext.RequestAborted);

                // Calculate days since last seen and map to DeviceVersionSummary
                var deviceVersionList = devices.Select(d => new DeviceVersionSummary
                {
                    Id = d.Id,
                    MachineName = d.MachineName,
                    DomainName = d.DomainName,
                    FleetId = d.FleetId,
                    Manufacturer = d.Manufacturer,
                    Model = d.Model,
                    ClientVersion = d.ClientVersion,
                    LastSeenUtc = d.LastSeenUtc,
                    DaysSinceLastSeen = (int)(DateTimeOffset.UtcNow - d.LastSeenUtc).TotalDays
                }).ToList();

                TotalDevices = deviceVersionList.Count;

                // Group devices by version
                var versionGroups = deviceVersionList
                    .GroupBy(d => d.ClientVersion ?? "Unknown")
                    .Select(g => new ClientVersionInfo
                    {
                        Version = g.Key,
                        DeviceCount = g.Count(),
                        Devices = g.OrderBy(d => d.MachineName).ToList(),
                        OldestReportDate = g.Min(d => d.LastSeenUtc),
                        NewestReportDate = g.Max(d => d.LastSeenUtc)
                    })
                    .ToList();

                // Determine status for each version group
                foreach (var group in versionGroups)
                {
                    if (group.Version == "Unknown")
                    {
                        group.IsOutdated = true;
                        group.IsUnsupported = true;
                        UnknownVersionDevices += group.DeviceCount;
                        continue;
                    }

                    try
                    {
                        var currentVer = Version.Parse(group.Version);

                        group.IsLatest = currentVer >= latestVer;
                        group.IsOutdated = currentVer < latestVer;
                        group.IsUnsupported = currentVer < minimumVer;

                        if (group.IsUnsupported)
                        {
                            UnsupportedDevices += group.DeviceCount;
                        }
                        else if (group.IsOutdated)
                        {
                            OutdatedDevices += group.DeviceCount;
                        }
                        else
                        {
                            UpToDateDevices += group.DeviceCount;
                        }
                    }
                    catch (Exception)
                    {
                        // Invalid version format
                        group.IsOutdated = true;
                        group.IsUnsupported = true;
                        UnknownVersionDevices += group.DeviceCount;
                    }
                }

                // Sort: Unsupported first, then Outdated, then Latest, then Unknown
                VersionGroups = versionGroups
                    .OrderByDescending(g => g.IsUnsupported)
                    .ThenByDescending(g => g.IsOutdated)
                    .ThenByDescending(g => g.IsLatest)
                    .ThenBy(g => g.Version)
                    .ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading client versions: {ex.Message}";
            }
        }
    }
}
