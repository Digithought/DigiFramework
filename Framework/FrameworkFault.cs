using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
	[Serializable]
	public class FrameworkFault : FrameworkException, ISerializable
	{
		public FrameworkFault(string message) : base(message)
		{
		}

		protected FrameworkFault(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}