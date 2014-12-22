using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
	[Serializable]
	public class FrameworkException : Exception, ISerializable
	{
		public FrameworkException(string message) : base(message)
		{
		}

		protected FrameworkException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}