using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Digithought.Framework
{
	public static class Configurer
	{
		public static T Get<T>(T defaultConfig)
		{
			return Get(defaultConfig, typeof(T).Name);
		}

		public static T Get<T>(T defaultConfig, string name)
		{
			string config = null;
			var fileName = System.Reflection.Assembly.GetEntryAssembly().Location + "." + name + ".config";
			try
			{
				if (File.Exists(fileName))
				{
					config = File.ReadAllText(fileName);
					var copy = defaultConfig.Clone();
					var errorLoading = false;
					var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings 
						{ 
							Error = (o, e) => { 
								errorLoading = true;
								Logging.Error(e.ErrorContext.Error);
							}, 
							ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Reuse 
						};
					Newtonsoft.Json.JsonConvert.PopulateObject(config, copy, jsonSettings);
					if (errorLoading)
						try
						{
							SaveObject(copy, fileName);
						}
						catch (Exception e)
						{
							Logging.Error(e);
							// Don't rethrow - it's okay if we can't rewrite the config file
						}
					return copy;
				}
				else
				{
					SaveObject(defaultConfig, fileName);
				}
			}
			catch (Exception e)
			{
				Logging.Error(e);
				// Don't rethrow - we'll just use the default
			}
			finally
			{
				#if (TRACE_CONFIGURATION)
				Logging.Trace("Configuration", name + ": \t" + config);
				#endif
			}
			return defaultConfig;
		}

		/// <summary> Copies values from the root of the configuration object to any sub-configuration objects which have the same property names but no value. </summary>
		public static void ApplyInherited(object config)
		{
			foreach (var member in 
				config.GetType().FindMembers
				(
					MemberTypes.Field | MemberTypes.Property, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy, 
					(m, c) => !(m is PropertyInfo) || ((PropertyInfo)m).GetIndexParameters().Length == 0, 
					null
				)
			)
			{
				var value = member is FieldInfo ? ((FieldInfo)member).GetValue(config) : ((PropertyInfo)member).GetValue(config);
				if (value != null && typeof(IComparable).IsAssignableFrom(value.GetType()))
					InternalApply(member, config, value);
			}
		}

		private static void InternalApply(MemberInfo toApply, object config, object applyValue)
		{
			if (config == null || config.GetType().IsValueType)
				return;
			if (config is IEnumerable)
			{
				foreach (var item in (IEnumerable)config)
					InternalApply(toApply, item, applyValue);
			}
			else
			{
				foreach (var member in
					config.GetType().FindMembers
					(
						MemberTypes.Field | MemberTypes.Property, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
						(m, c) => !(m is PropertyInfo) || ((PropertyInfo)m).GetIndexParameters().Length == 0,
						null
					)
				)
				{
					var value = member is FieldInfo ? ((FieldInfo)member).GetValue(config) : ((PropertyInfo)member).GetValue(config);
					var type = member is FieldInfo ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
					if (toApply != member && member.Name == toApply.Name && type.IsAssignableFrom(applyValue.GetType()) 
						&& typeof(IComparable).IsAssignableFrom(type)
					)
					{
						// Set the value of the member if it isn't the default value for its type
						if (value == null || ((IComparable)value).CompareTo(type.IsValueType ? Activator.CreateInstance(type) : (object)null) == 0)
						{
							if (member is FieldInfo)
								((FieldInfo)member).SetValue(config, applyValue);
							else
								((PropertyInfo)member).SetValue(config, applyValue);
						}
					}
					else
						InternalApply(toApply, value, applyValue);
				}
			}
		}

		private static void SaveObject<T>(T instance, string fileName)
		{
			File.WriteAllText(fileName, Newtonsoft.Json.JsonConvert.SerializeObject(instance));
		}
	}
}
