using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Digithought.Framework
{
	public static class Logging
	{
		public static event Action<Exception> LogError;
		public static event Action<string, object> LogTrace;
		public static Func<string, object, bool> TraceFilter;

		public static void Error(Exception e)
		{
			var handler = LogError;
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
			var pass = true;
			var filter = TraceFilter;
			if (filter != null)
				pass = filter(category, message);

			if (pass)
			{
				System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("o") + ": " + message);

				var handler = LogTrace;
				if (handler != null)
					handler(category, message);
			}
		}
	}
}