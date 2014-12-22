using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Digithought.Framework
{
	public interface IStatefulActor<TState, TTrigger>
		where TState : struct
	{
		TState State { get; }
		event StateMachine<TState, TTrigger>.StateChangedHandler StateChanged;
	}
}
