using System;
using System.IO;

namespace Digithought.Framework
{
    public static class Configurer
	{
		public static T Get<T>(T defaultConfig)
		{
			string config = null;
			var fileName = System.Reflection.Assembly.GetEntryAssembly().Location + "." + typeof(T).Name + ".config";
			try
			{
				if (File.Exists(fileName))
				{
					config = File.ReadAllText(fileName);
					var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig));
					Newtonsoft.Json.JsonConvert.PopulateObject(config, copy);
					return copy;
				}
				else
				{
					config = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig);
					File.WriteAllText(fileName, config);
				}
			}
			catch (Exception e)
			{
				Logging.Error(e);
				// Don't rethrow - this isn't critical
			}
			finally
			{
				#if (TRACE_CONFIGURATION)
				Logging.Trace("Configuration", typeof(T).Name + ": \t" + config);
				#endif
			}
			return defaultConfig;
		}

	}
}
