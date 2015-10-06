using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework
{
	public static class ObjectUtility
	{
		/// <summary> Makes a deep copy of an object using a serializer/deserializer. </summary>
		public static T Clone<T>(this T value)
		{
			return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value));
		}

		/// <summary> Makes a deep copy of an object and provides an opportunity to perform additional initialization on the result. </summary>
		public static T Clone<T>(this T value, Action<T> initializer)
		{
			var result = value.Clone();
			if (initializer != null)
				initializer(result);
			return result;
		}
	}
}
