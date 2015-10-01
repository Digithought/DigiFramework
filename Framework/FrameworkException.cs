using System;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
    [Serializable]
	public class FrameworkException : Exception, ISerializable
	{
		public FrameworkException(string message) : base(message)
		{
		}

		public FrameworkException(string message, Exception inner)
			: base(message, inner)
		{
		}

		protected FrameworkException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}