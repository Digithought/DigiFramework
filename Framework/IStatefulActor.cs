namespace Digithought.Framework
{
	public interface IStatefulActor<TState, TTrigger>
		where TState : struct
	{
		TState State { get; }
		event StateMachine<TState, TTrigger>.StateChangedHandler StateChanged;
		bool InState(TState state);
	}
}
