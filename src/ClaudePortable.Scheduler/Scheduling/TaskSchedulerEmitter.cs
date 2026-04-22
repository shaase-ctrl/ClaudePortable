using System.Globalization;
using System.Text;
using System.Xml;

namespace ClaudePortable.Scheduler.Scheduling;

public static class TaskSchedulerEmitter
{
    public const string DefaultTaskNamePrefix = "ClaudePortable-";

    public static string ToXml(ScheduleSpec spec, DateTimeOffset referenceDate)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(false),
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Task", "http://schemas.microsoft.com/windows/2004/02/mit/task");
            writer.WriteAttributeString("version", "1.4");

            writer.WriteStartElement("RegistrationInfo");
            writer.WriteElementString("Date", referenceDate.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            writer.WriteElementString("Author", Environment.UserName);
            writer.WriteElementString("Description", spec.Description ?? "ClaudePortable scheduled backup");
            writer.WriteEndElement();

            writer.WriteStartElement("Triggers");
            writer.WriteStartElement("CalendarTrigger");
            var startLocal = DateTime.SpecifyKind(DateTime.Today.Add(spec.DailyStart.ToTimeSpan()), DateTimeKind.Unspecified);
            writer.WriteElementString("StartBoundary", startLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            writer.WriteElementString("Enabled", "true");
            writer.WriteStartElement("ScheduleByDay");
            writer.WriteElementString("DaysInterval", "1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("Principals");
            writer.WriteStartElement("Principal");
            writer.WriteAttributeString("id", "Author");
            writer.WriteElementString("LogonType", "InteractiveToken");
            writer.WriteElementString("RunLevel", "LeastPrivilege");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("Settings");
            writer.WriteElementString("MultipleInstancesPolicy", "IgnoreNew");
            writer.WriteElementString("DisallowStartIfOnBatteries", "true");
            writer.WriteElementString("StopIfGoingOnBatteries", "true");
            writer.WriteElementString("AllowHardTerminate", "true");
            writer.WriteElementString("StartWhenAvailable", "true");
            writer.WriteElementString("RunOnlyIfNetworkAvailable", "false");
            writer.WriteElementString("WakeToRun", spec.WakeToRun ? "true" : "false");
            writer.WriteElementString("ExecutionTimeLimit", "PT2H");
            writer.WriteElementString("Enabled", "true");
            writer.WriteEndElement();

            writer.WriteStartElement("Actions");
            writer.WriteAttributeString("Context", "Author");
            writer.WriteStartElement("Exec");
            writer.WriteElementString("Command", spec.ExecutablePath);
            if (spec.Arguments.Count > 0)
            {
                writer.WriteElementString("Arguments", JoinArguments(spec.Arguments));
            }
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    public static string JoinArguments(IReadOnlyList<string> args)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < args.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }
            builder.Append(QuoteArg(args[i]));
        }
        return builder.ToString();
    }

    private static string QuoteArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }
        if (!arg.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return arg;
        }
        var escaped = arg.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
