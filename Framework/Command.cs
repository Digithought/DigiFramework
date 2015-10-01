namespace Digithought.Framework
{
    public class Command<TState, TTrigger>
		where TState : struct
		where TTrigger : struct
	{
		/// <summary> State's in which the command is valid, or null if all. </summary>
		public TState[] ValidInStates { get; private set; }

		/// <summary> Trigger to fire when command executed. </summary>
		/// <remarks> Any command parameters are ignored. </remarks>
		public TTrigger? Trigger { get; private set; }

		public Command(TState[] validInStates = null, TTrigger? trigger = null)
		{
			ValidInStates = validInStates;
			Trigger = trigger;
		}
	}
}
