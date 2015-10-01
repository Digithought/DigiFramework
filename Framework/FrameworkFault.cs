using System;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
    [Serializable]
	public class FrameworkFault : FrameworkException, ISerializable
	{
		public FrameworkFault(string message) : base(message)
		{
		}

		public FrameworkFault(string message, Exception inner)
			: base(message, inner)
		{
		}

		protected FrameworkFault(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}