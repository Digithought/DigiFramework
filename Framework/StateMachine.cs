using System;
using System.Collections.Generic;
using System.Linq;

namespace Digithought.Framework
{
	public class StateMachine<TState, TTrigger>
		where TState : struct
	{
		public class StateInfo
		{
			public TState State { get; private set; }
			public TState? Parent { get; private set; }
			public IReadOnlyCollection<Transition> Transitions { get; private set; }
			public StateChangedHandler Entered { get; private set; }
			public StateChangedHandler Exited { get; private set; }

			public StateInfo(TState state, TState? parent, Transition[] transitions, StateChangedHandler entered = null, StateChangedHandler exited = null)
			{
				State = state;
				Parent = parent;
				Transitions = Array.AsReadOnly(transitions ?? new Transition[0]);
				Entered = entered;
				Exited = exited;
			}
		}

		public class Transition
		{
			public TTrigger Trigger { get; private set; }
			public TState Target { get; private set; }
			public StateTransitionConditionHandler Condition { get; private set; }
			public Action<TState> SetupState { get; private set; }

			public Transition(TTrigger trigger, TState target, StateTransitionConditionHandler condition = null, Action<TState> setupState = null)
			{
				Trigger = trigger;
				Target = target;
				Condition = condition;
				SetupState = setupState;
			}
		}

		public IDictionary<TState, StateInfo> States { get; private set; }
		public TState State { get; private set; }
		public bool Transitioning { get; private set; }
		public Action<Exception> UnhandledError { get; set; }
		public event StateChangedHandler StateChanged;

		public StateMachine(IEnumerable<StateInfo> states, TState initial = default(TState))
		{
			States = states.ToDictionary(si => si.State);
			State = initial;
		}

		public void Update()
		{
			if (!Transitioning)
			{
				Transitioning = true;
				try
				{
					while (InternalUpdate()) /* repeat until no more transitions are applicable. */;
				}
				finally
				{
					Transitioning = false;
				}
			}
		}

		private bool InternalUpdate()
		{
			var oldState = GetState(State);
			var transition = oldState.Transitions
				.Where(t => t.Condition != null && WrapCallback(() => t.Condition(State, t), false))
				.FirstOrDefault();
			if (transition != null)
			{
				InternalTransition(oldState, transition);
				return true;
			}
			return false;
		}

		private void InternalTransition(StateInfo oldState, Transition transition)
		{
			var newState = GetState(transition.Target);
			DoTransitionEvents(oldState, newState, s => { if (s.Exited != null) s.Exited(oldState.State, transition); });
			if (transition.SetupState != null)
				transition.SetupState(transition.Target);
			State = transition.Target;
			DoTransitionEvents(newState, oldState, s => { if (s.Entered != null) s.Entered(oldState.State, transition); });
			if (StateChanged != null)
				StateChanged(oldState.State, transition);
		}

		public bool InState(TState state)
		{
			return StateIn(State, state);
		}

		public bool StateIn(TState state, TState target)
		{
			if (state.Equals(target))
				return true;
			var parent = GetState(state).Parent;
			return parent != null && StateIn(parent.Value, target);
		}

		public void Fire(TTrigger trigger)
		{
			if (!Transitioning)
			{
				var oldState = GetState(State);
				var state = oldState;
				Transitioning = true;
				try
				{
					while (state != null)
					{
						var transition = state.Transitions
							.Where(t => t.Condition == null && t.Trigger.Equals(trigger))
							.FirstOrDefault();
						if (transition != null)
						{
							InternalTransition(oldState, transition);
							while (InternalUpdate()) /* repeat until no more transitions are applicable. */;
							break;
						}
						else 
						{
							state = state.Parent == null ? null : GetState(state.Parent.Value);
							if (state == null)
								Logging.Trace(FrameworkLoggingCategory.States, "WARNING: Trigger " + trigger + " fired and has no transitions.");
						}
					}
				}
				finally
				{
					Transitioning = false;
				}
			}
			else
				DoHandleError(new FrameworkException("Cannot trigger " + trigger + " while transitioning (state " + State + ")."));
		}

		private void WrapCallback(Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				DoHandleError(e);
			}
		}

		private T WrapCallback<T>(Func<T> func, T defaultResult)
		{
			try
			{
				return func();
			}
			catch (Exception e)
			{
				DoHandleError(e);
				return defaultResult;
			}
		}

		private void DoHandleError(Exception e)
		{
			if (UnhandledError != null)
				UnhandledError(e);
		}

		private void DoTransitionEvents(StateInfo source, StateInfo target, Action<StateInfo> each)
		{
			var ancestors = FindPathFromCommon(source, target);
			if (ancestors != null)
			{
				foreach (var a in ancestors)
					WrapCallback(() => each(a));
			}
			else 
				WrapCallback(() => each(source));
		}

		private IEnumerable<StateInfo> FindPathFromCommon(StateInfo source, StateInfo target)
		{
			var current = target;
			while (current.Parent != null && !source.State.Equals(current.State))
				current = GetState(current.Parent.Value);
			if (!source.State.Equals(current.State) && source.Parent != null)
			{
				var ancestors = FindPathFromCommon(GetState(source.Parent.Value), target);
				if (ancestors != null)
					return new[] { source }.Concat(ancestors);

			}
			else if (source.State.Equals(current.State))
				return new StateInfo[0];
			return null;
		}

		private StateInfo GetState(TState state)
		{
			StateInfo info;
			if (States.TryGetValue(state, out info))
			{
				return info;
			}
			throw new FrameworkException("State not configured: " + state);
		}

		public delegate void StateChangedHandler(TState oldState, Transition transition);
		public delegate bool StateTransitionConditionHandler(TState oldState, Transition transition);
	}
}
