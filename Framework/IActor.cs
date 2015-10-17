using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework
{
	public interface IActor<TActor>
	{
		/// <summary> Performs operations against the actor within the actor's thread context. </summary>
		void Atomically(Action<TActor> actor);
	}
}
