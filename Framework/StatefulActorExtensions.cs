namespace Digithought.Framework
{
	public static class StatefulActorExtensions
	{
		public static void WaitForState<TActor, TState, TTrigger>(this IStatefulActor<TActor, TState, TTrigger> actor, TState desired, int timeout)
			where TState : struct
		{
			var completionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();
			void waitForUnstarted(TState oldState, StateMachine<TState, TTrigger>.Transition transition)
			{
				if (actor.InState(desired))
					completionSource.TrySetResult(true);
			};
			actor.StateChanged += waitForUnstarted;

			// Check for synchronous completion
			if (actor.InState(desired))
				completionSource.TrySetResult(true);

			completionSource.Task.Wait(timeout);
			actor.StateChanged -= waitForUnstarted;
		}
	}
}
