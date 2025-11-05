using System;
using System.Collections.Generic;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Shared.Validation
{
    public static class ReportValidator
    {
        public static bool TryValidate(SecureBootStatusReport report, out IReadOnlyCollection<string> errors)
        {
            if (report == null)
            {
                errors = new[] { "Report payload is null." };
                return false;
            }

            var list = new List<string>();

            if (string.IsNullOrWhiteSpace(report.Device.MachineName))
            {
                list.Add("Device.MachineName is required.");
            }

            if (report.Events == null)
            {
                list.Add("Events collection must be initialized.");
            }

            if (report.CreatedAtUtc == default)
            {
                list.Add("CreatedAtUtc must be set.");
            }

            errors = list;
            return list.Count == 0;
        }
    }
}
