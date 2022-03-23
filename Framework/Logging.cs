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
			handler?.Invoke(e);
		}

		/// <summary> Logs a trace message.  FRAMEWORK_TRACING must be defined in your compiler defines or this method call will be skipped.  </summary>
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
				var handler = LogTrace;	// Capture
				handler?.Invoke(category, message);
			}
		}
	}

	public delegate void LogErrorHandler(Exception e);

	public delegate void LogTraceHandler(string category, object message);

	public delegate bool LogTraceFilterHandler(string category, object message);
}