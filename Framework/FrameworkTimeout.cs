using System;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
    [Serializable]
	public class FrameworkTimeout : FrameworkFault
	{
		public FrameworkTimeout(string message) : base(message)
		{
		}

		protected FrameworkTimeout(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}