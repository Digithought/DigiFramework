namespace Digithought.Framework
{
	public interface IStatefulActor<TActor, TState, TTrigger> : IActor<TActor>
		where TState : struct
	{
		TState State { get; }
		event StateMachine<TState, TTrigger>.StateChangedHandler StateChanged;
		bool InState(TState state);
		bool StateIn(TState state, TState target);
	}
}
