using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using SecureBootDashboard.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecureBootDashboard.Web.Pages
{
    public class ClientVersionsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ClientVersionsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
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

                // Get API base URL
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                // Create HTTP client
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(apiBaseUrl);

                // Fetch devices from API
                var response = await client.GetAsync("/api/Devices");

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = $"Failed to fetch devices from API: {response.StatusCode}";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var devices = JsonSerializer.Deserialize<List<DeviceVersionSummary>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<DeviceVersionSummary>();

                // Calculate days since last seen
                foreach (var device in devices)
                {
                    device.DaysSinceLastSeen = (int)(DateTimeOffset.UtcNow - device.LastSeenUtc).TotalDays;
                }

                TotalDevices = devices.Count;

                // Group devices by version
                var versionGroups = devices
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
