using System;

namespace Digithought.Framework
{
	public static class Logging
	{
		public static event LogErrorHandler LogError;
		public static event LogTraceHandler LogTrace;
		public static LogTraceFilterHandler TraceFilter;
		public static System.Text.RegularExpressions.Regex CategoryMask = null;

		public static void Error(Exception e)
		{
			var handler = LogError;	// Capture
			if (handler != null)
				handler(e);
			else
				System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("o") + ": " + e.ToString());
		}

		/// <summary> Logs a trace message. </summary>
		/// <param name="category"> Use LoggingCategory class for valid categories. </param>
		/// <param name="message"> Message.  Don't include timestamp. </param>
		[System.Diagnostics.Conditional("FRAMEWORK_TRACING")]
		public static void Trace(string category, object message)
		{
			// Filter as appropriate
			var filter = TraceFilter;	// Capture
			var mask = CategoryMask;	// Capture
			if 
			(
				(filter == null || filter(category, message))
					&& (mask == null || mask.IsMatch(category))
			)
			{
				System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("o") + ": " + message);

				var handler = LogTrace;	// Capture
				if (handler != null)
					handler(category, message);
			}
		}
	}

	public delegate void LogErrorHandler(Exception e);

	public delegate void LogTraceHandler(string category, object message);

	public delegate bool LogTraceFilterHandler(string category, object message);
}