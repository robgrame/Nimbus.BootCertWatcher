using Serilog.Events;
using Serilog.Formatting;
using System;
using System.IO;

namespace SecureBootWatcher.Client.Logging
{
    /// <summary>
    /// CMTrace-compatible log formatter for Serilog.
    /// Produces logs in the format expected by Microsoft CMTrace.exe viewer.
    /// </summary>
    public sealed class CMTraceFormatter : ITextFormatter
    {
        private const string Component = "SecureBootWatcher.Client";

        public void Format(LogEvent logEvent, TextWriter output)
        {
            // CMTrace format:
            // <![LOG[Message]LOG]!><time="HH:mm:ss.fff+000" date="MM-dd-yyyy" component="Component" context="" type="1" thread="0" file="">

            // Extract message
            var message = logEvent.RenderMessage();
            
            // Include exception if present
            if (logEvent.Exception != null)
            {
                message = $"{message}{Environment.NewLine}{logEvent.Exception}";
            }

            // NOTE: Do NOT escape XML characters in the message.
            // The <![LOG[...]LOG]!> is a CDATA-like section and CMTrace handles special characters correctly.
            // Escaping them causes &quot;, &lt;, etc. to appear in the log viewer.

            // Format timestamp
            var timestamp = logEvent.Timestamp.ToLocalTime();
            var time = timestamp.ToString("HH:mm:ss.fff");
            var timezoneOffset = timestamp.ToString("zzz").Replace(":", ""); // +0100 format
            var date = timestamp.ToString("MM-dd-yyyy");

            // Map Serilog level to CMTrace type
            // 1 = Information (normal)
            // 2 = Warning (yellow)
            // 3 = Error (red)
            int type = GetCMTraceType(logEvent.Level);

            // Get thread ID if available
            string thread = "0";
            if (logEvent.Properties.TryGetValue("ThreadId", out var threadIdProperty))
            {
                thread = threadIdProperty.ToString().Trim('"');
            }

            // Write CMTrace format
            output.Write("<![LOG[");
            output.Write(message);
            output.Write("]LOG]!><time=\"");
            output.Write(time);
            output.Write(timezoneOffset);
            output.Write("\" date=\"");
            output.Write(date);
            output.Write("\" component=\"");
            output.Write(Component);
            output.Write("\" context=\"\" type=\"");
            output.Write(type);
            output.Write("\" thread=\"");
            output.Write(thread);
            output.Write("\" file=\"\">");
            output.WriteLine();
        }

        private static int GetCMTraceType(LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                case LogEventLevel.Information:
                    return 1; // Information

                case LogEventLevel.Warning:
                    return 2; // Warning

                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    return 3; // Error

                default:
                    return 1;
            }
        }
    }
}
