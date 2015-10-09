using System;
using System.Runtime.Serialization;

namespace Digithought.Framework
{
	[Serializable]
	public class FrameworkWatchedStateException : FrameworkException
	{
		public object Other { get; private set; }
        
        public FrameworkWatchedStateException(string message) : base(message)
		{
		}

        public FrameworkWatchedStateException(string message, object other) : this(message)
        {
            Other = other;
        }

        protected FrameworkWatchedStateException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}