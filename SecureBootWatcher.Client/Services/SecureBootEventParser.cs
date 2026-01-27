using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Parses structured data from Secure Boot event log records based on Microsoft documentation:
    /// https://support.microsoft.com/en-us/topic/secure-boot-db-and-dbx-variable-update-events-37e47cf8-608b-4a87-8175-bdead630eb69
    /// </summary>
    internal static class SecureBootEventParser
    {
        // Regex patterns for parsing event message text
        private static readonly Regex UpdateTypeRegex = new Regex(@"UpdateType:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BucketConfidenceLevelRegex = new Regex(@"BucketConfidenceLevel:\s*(.+?)(?:\n|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BucketIdRegex = new Regex(@"Bucketld:\s*([a-fA-F0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HResultRegex = new Regex(@"HResult:\s*(.+?)(?:\.|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FirmwareManufacturerRegex = new Regex(@"FirmwareManufacturer:\s*(.+?)(?:\s+FirmwareVersion|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FirmwareVersionRegex = new Regex(@"FirmwareVersion:\s*(.+?)(?:\s+OEMModelNumber|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OEMModelNumberRegex = new Regex(@"OEMModelNumber:\s*(.+?)(?:\.|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OEMManufacturerNameRegex = new Regex(@"OEMManufacturerName:\s*(.+?)(?:\s+OSArchitecture|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OSArchitectureRegex = new Regex(@"OSArchitecture:\s*(.+?)(?:\s+Bucketld|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UpdatesAvailableRegex = new Regex(@"(\d+)\s+update", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ErrorCodeRegex = new Regex(@"(?:error|failed).*?(?:code|0x)([0-9a-fA-F]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Enriches a SecureBootEventRecord with structured data extracted from the event XML and message.
        /// </summary>
        public static void ParseEventData(
            int eventId,
            string? message,
            string? rawXml,
            out int? updateType,
            out string? bucketConfidenceLevel,
            out string? bucketId,
            out int? hResult,
            out string? firmwareManufacturer,
            out string? firmwareVersion,
            out string? oemModelNumber,
            out string? oemManufacturerName,
            out string? osArchitecture,
            out int? updatesAvailable,
            out int? errorCode,
            out bool? rebootRequired,
            out Dictionary<string, string>? additionalData)
        {
            // Initialize all out parameters
            updateType = null;
            bucketConfidenceLevel = null;
            bucketId = null;
            hResult = null;
            firmwareManufacturer = null;
            firmwareVersion = null;
            oemModelNumber = null;
            oemManufacturerName = null;
            osArchitecture = null;
            updatesAvailable = null;
            errorCode = null;
            rebootRequired = null;
            additionalData = null;

            // Parse based on event ID
            switch (eventId)
            {
                case 1808:
                    // Event 1808: Device has updated Secure Boot CA/keys (boot manager signed with CA 2023)
                    ParseEvent1808(message, out updateType, out bucketConfidenceLevel, out bucketId, 
                        out hResult, out firmwareManufacturer, out firmwareVersion, out oemModelNumber, 
                        out oemManufacturerName, out osArchitecture);
                    break;

                case 1036:
                    // Event 1036: Update applied to firmware, reboot required
                    rebootRequired = true;
                    ParseDeviceAttributes(message, out firmwareManufacturer, out firmwareVersion, 
                        out oemModelNumber, out oemManufacturerName, out osArchitecture);
                    ParseUpdateType(message, out updateType);
                    break;

                case 1037:
                    // Event 1037: Update applied after reboot
                    rebootRequired = false;
                    ParseDeviceAttributes(message, out firmwareManufacturer, out firmwareVersion, 
                        out oemModelNumber, out oemManufacturerName, out osArchitecture);
                    ParseUpdateType(message, out updateType);
                    break;

                case 1032:
                    // Event 1032: Update installation started
                    break;

                case 1033:
                    // Event 1033: Update installation succeeded
                    ParseHResult(message, out hResult);
                    break;

                case 1034:
                    // Event 1034: Update installation failed
                    ParseErrorCode(message, out errorCode);
                    ParseHResult(message, out hResult);
                    break;

                case 1043:
                    // Event 1043: Update not applicable to this device
                    ParseUpdateType(message, out updateType);
                    ParseErrorCode(message, out errorCode);
                    break;

                case 1044:
                    // Event 1044: More updates available after reboot
                    rebootRequired = true;
                    ParseUpdatesAvailable(message, out updatesAvailable);
                    ParseUpdateType(message, out updateType);
                    break;

                case 1045:
                    // Event 1045: All updates completed successfully
                    ParseUpdatesAvailable(message, out updatesAvailable);
                    ParseUpdateType(message, out updateType);
                    break;

                // Events 1795-1801: Boot/State events (less structured data)
                case 1795:
                case 1796:
                case 1797:
                case 1798:
                case 1799:
                case 1801:
                    // These events typically contain boot state information
                    // Parse any available HResult or error codes
                    ParseHResult(message, out hResult);
                    break;
            }

            // Try to extract additional data from XML if available
            if (!string.IsNullOrEmpty(rawXml))
            {
                TryParseXmlEventData(rawXml!, ref additionalData);
            }
        }

        private static void ParseEvent1808(
            string? message,
            out int? updateType,
            out string? bucketConfidenceLevel,
            out string? bucketId,
            out int? hResult,
            out string? firmwareManufacturer,
            out string? firmwareVersion,
            out string? oemModelNumber,
            out string? oemManufacturerName,
            out string? osArchitecture)
        {
            updateType = null;
            bucketConfidenceLevel = null;
            bucketId = null;
            hResult = null;
            firmwareManufacturer = null;
            firmwareVersion = null;
            oemModelNumber = null;
            oemManufacturerName = null;
            osArchitecture = null;

            if (string.IsNullOrEmpty(message))
                return;

            // Parse UpdateType
            ParseUpdateType(message, out updateType);

            // Parse BucketConfidenceLevel
            var bucketMatch = BucketConfidenceLevelRegex.Match(message);
            if (bucketMatch.Success)
            {
                bucketConfidenceLevel = bucketMatch.Groups[1].Value.Trim();
            }

            // Parse BucketId
            var bucketIdMatch = BucketIdRegex.Match(message);
            if (bucketIdMatch.Success)
            {
                bucketId = bucketIdMatch.Groups[1].Value.Trim();
            }

            // Parse HResult
            ParseHResult(message, out hResult);

            // Parse device attributes
            ParseDeviceAttributes(message, out firmwareManufacturer, out firmwareVersion, 
                out oemModelNumber, out oemManufacturerName, out osArchitecture);
        }

        private static void ParseUpdateType(string? message, out int? updateType)
        {
            updateType = null;
            if (string.IsNullOrEmpty(message))
                return;

            var match = UpdateTypeRegex.Match(message);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                updateType = value;
            }
        }

        private static void ParseHResult(string? message, out int? hResult)
        {
            hResult = null;
            if (string.IsNullOrEmpty(message))
                return;

            // Check for common success messages
            if (message.Contains("operation completed successfully", StringComparison.OrdinalIgnoreCase))
            {
                hResult = 0; // S_OK
                return;
            }

            var match = HResultRegex.Match(message);
            if (match.Success)
            {
                var hresultText = match.Groups[1].Value.Trim();
                if (hresultText.Contains("success", StringComparison.OrdinalIgnoreCase))
                {
                    hResult = 0;
                }
            }
        }

        private static void ParseDeviceAttributes(
            string? message,
            out string? firmwareManufacturer,
            out string? firmwareVersion,
            out string? oemModelNumber,
            out string? oemManufacturerName,
            out string? osArchitecture)
        {
            firmwareManufacturer = null;
            firmwareVersion = null;
            oemModelNumber = null;
            oemManufacturerName = null;
            osArchitecture = null;

            if (string.IsNullOrEmpty(message))
                return;

            var fmMatch = FirmwareManufacturerRegex.Match(message);
            if (fmMatch.Success)
            {
                firmwareManufacturer = fmMatch.Groups[1].Value.Trim();
            }

            var fvMatch = FirmwareVersionRegex.Match(message);
            if (fvMatch.Success)
            {
                firmwareVersion = fvMatch.Groups[1].Value.Trim();
            }

            var oemModelMatch = OEMModelNumberRegex.Match(message);
            if (oemModelMatch.Success)
            {
                oemModelNumber = oemModelMatch.Groups[1].Value.Trim();
            }

            var oemMfgMatch = OEMManufacturerNameRegex.Match(message);
            if (oemMfgMatch.Success)
            {
                oemManufacturerName = oemMfgMatch.Groups[1].Value.Trim();
            }

            var osArchMatch = OSArchitectureRegex.Match(message);
            if (osArchMatch.Success)
            {
                osArchitecture = osArchMatch.Groups[1].Value.Trim();
            }
        }

        private static void ParseUpdatesAvailable(string? message, out int? updatesAvailable)
        {
            updatesAvailable = null;
            if (string.IsNullOrEmpty(message))
                return;

            var match = UpdatesAvailableRegex.Match(message);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                updatesAvailable = value;
            }
        }

        private static void ParseErrorCode(string? message, out int? errorCode)
        {
            errorCode = null;
            if (string.IsNullOrEmpty(message))
                return;

            var match = ErrorCodeRegex.Match(message);
            if (match.Success)
            {
                var hexValue = match.Groups[1].Value;
                if (int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    errorCode = value;
                }
            }
        }

        private static void TryParseXmlEventData(string rawXml, ref Dictionary<string, string>? additionalData)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(rawXml);

                // Navigate to EventData node
                var eventDataNode = doc.SelectSingleNode("//EventData");
                if (eventDataNode == null)
                    return;

                additionalData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Extract Data elements
                var dataNodes = eventDataNode.SelectNodes("Data");
                if (dataNodes != null)
                {
                    foreach (XmlNode dataNode in dataNodes)
                    {
                        var nameAttr = dataNode.Attributes?["Name"];
                        if (nameAttr != null && !string.IsNullOrEmpty(dataNode.InnerText))
                        {
                            additionalData[nameAttr.Value] = dataNode.InnerText;
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore XML parsing errors
            }
        }
    }
}
