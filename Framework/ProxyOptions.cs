using System;

namespace Digithought.Framework
{
	public class ProxyOptions
	{
		/// <summary> Optional class to inherit from.  Calls to this class will not be intercepted, and the resulting proxy will need to be cast in order to access the base class. </summary>
		public Type BaseClass = null;

		/// <summary> Additional interfaces to inherit from. </summary>
		public Type[] AdditionalInterfaces = null;
	}
}