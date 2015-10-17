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
		public Action<TTrigger> UnhandledTrigger { get; set; }
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
							if (state == null && UnhandledTrigger != null)
								UnhandledTrigger(trigger);
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

        private void InternalTransition(StateInfo oldState, Transition transition)
        {
            var newState = GetState(transition.Target);

            // Raise Exit events for each departing state
            foreach (var a in FindPathFromCommon(oldState, newState))
                if (a.Exited != null) 
                    WrapCallback(() => a.Exited(oldState.State, transition));

            // Raise SetupState
            if (transition.SetupState != null)
                transition.SetupState(transition.Target);

            State = transition.Target;
            
            // Raise Entered events for each arriving state
            foreach (var a in FindPathFromCommon(newState, oldState).Reverse())
                if (a.Entered != null)
                    WrapCallback(() => a.Entered(oldState.State, transition));

            // Raise StateChanged
            if (StateChanged != null)
                StateChanged(oldState.State, transition);
        }

        private void DoTransitionEvents(StateInfo source, StateInfo target, Action<StateInfo> each, bool entering)
		{
		}

        /// <summary> Returns the non-overlapping lineage relative to source. </summary>
		private IEnumerable<StateInfo> FindPathFromCommon(StateInfo source, StateInfo target)
		{
            var sourceLineage = LinqExtensions.Sequence(source, i => i.Parent != null ? GetState(i.Parent.Value) : null).ToList();
            var unmatchedCount = sourceLineage.Count;
            foreach (var targetItem in LinqExtensions.Sequence(target, i => i.Parent != null ? GetState(i.Parent.Value) : null))
            {
                var index = sourceLineage.IndexOf(targetItem);
                if (index >= 0)
                {
                    unmatchedCount = index;
                    break;
                }
            }
            return sourceLineage.Take(unmatchedCount);
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
