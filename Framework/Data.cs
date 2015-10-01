using System;

namespace Digithought.Framework
{
	public static class Data
	{
		public static event LogDataHandler LogData;
		public static LogDataFilterHandler LogFilter;
		public static System.Text.RegularExpressions.Regex TopicMask = null;

		[System.Diagnostics.Conditional("FRAMEWORK_DATALOGGING")]
		public static void Log(DataHeader header, params object[] values)
		{
			LogTo(header, null, values);
		}

		public static void LogTo(DataHeader header, string fileName, params object[] values)
		{
			try
			{
				var filter = LogFilter;	// Capture
				var mask = TopicMask;	// Capture
				if
					(
						(filter == null || filter(header, values))
							&& (mask == null || mask.IsMatch(header.Topic))
					)
				{
					var handler = LogData;	// Capture
					if (handler != null)
						handler(header, fileName, values);
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine("Error attempting to log data: \r\n" + e.ToString());
				// Don't rethrow
			}
		}
	}

	public delegate void LogDataHandler(DataHeader header, string fileName, object[] values);

	public delegate bool LogDataFilterHandler(DataHeader header, object[] values);

	public class DataHeader
	{
		public DataHeader(string topic, params string[] valueDescriptions)
		{
			Topic = topic;
			ValueDescriptions = valueDescriptions;
		}

		public object Tag { get; set; }

		public string Topic { get; private set; }

		public string[] ValueDescriptions { get; private set; }
	}
}
