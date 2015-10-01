using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Digithought.Framework
{
    public abstract class StatefulActor<TActor, TState, TTrigger> : ActorBase<TActor>, IStatefulActor<TState, TTrigger>
		where TActor : class
		where TState : struct
		where TTrigger : struct
	{
		public StatefulActor(WorkerQueue worker = null, System.Threading.ThreadPriority? priority = null)
			: base(worker, priority)
		{
			_states = InitializeStates();
			_states.HandleError = StateException;
			_states.StateChanged += HandleStateChanged;
			_commands = InitializeCommands().ToDictionary(e => e.Key, e => e.Value);
		}

		private StateMachine<TState, TTrigger> _states;

		public TState State 
		{
			get { return _states.State; }
		}

		public bool Transitioning
		{
			get { return _states.Transitioning; }
		}

		public event StateMachine<TState, TTrigger>.StateChangedHandler StateChanged
		{
			add { _states.StateChanged += value; }
			remove { _states.StateChanged -= value; }
		}

		public bool InState(TState state)
		{
			return _states.InState(state);
		}

		protected void Fire(TTrigger trigger)
		{
			if (_states.Transitioning)
				Act(() => Fire(trigger));
			else
				_states.Fire(trigger);
		}

		/// <summary> Retests the state conditions. </summary>
		protected void UpdateStates()
		{
			_states.Update();
		}

		protected virtual void StateException(Exception e)
		{
			HandleException(e);
		}

		protected virtual void HandleStateChanged(TState oldState, StateMachine<TState, TTrigger>.Transition transition)
		{
			Logging.Trace(FrameworkLoggingCategory.States, GetType().Name + "[" + GetHashCode() + "]: " + oldState + " -> " + transition.Target + " (" + transition.Trigger + ").");

			// Call back any state watchers
			List<TState> toRemove = null;
			foreach (var watcher in _watchers)
				if (!InState(watcher.Key))
				{
					foreach (var act in watcher.Value)
						act();
					if (toRemove == null)
						toRemove = new List<TState>();
					toRemove.Add(watcher.Key);
				}
			if (toRemove != null)
				foreach (var r in toRemove)
					_watchers.Remove(r);
		}

		private IReadOnlyDictionary<string, Command<TState, TTrigger>> _commands;

		protected abstract IReadOnlyDictionary<string, Command<TState, TTrigger>> InitializeCommands();

		protected static Command<TState, TTrigger> NewCommand(TState[] validInStates = null, TTrigger? trigger = null)
		{
			return new Command<TState, TTrigger>(validInStates, trigger);
		}

		public override object Invoke(MethodInfo method, params object[] parameters)
		{
			Command<TState, TTrigger> command;
			var commandFound = _commands.TryGetValue(method.Name, out command);
			if (!commandFound || (commandFound && (command.ValidInStates == null || command.ValidInStates.Any(s => InState(s)))))
			{
				if (commandFound && command.Trigger.HasValue)
				{
					#if (TRACE_ACTS)
					Logging.Trace(FrameworkLoggingCategory.Acts, "Call to " + GetType().Name + "[" + GetHashCode() + "]." + method.Name + " - triggering command: " + command.Trigger.Value);
					#endif
					Act(() => Fire(command.Trigger.Value));	// Must queue this call otherwise this could lead to out of order invocation
				}
				else
					return base.Invoke(method, parameters);
			}
			else
				Worker.Queue(() => HandleException(new FrameworkException(String.Format("Invalid command '{0}' against {1} [{2}] when in state {3}.", method.Name, GetType().Name, GetHashCode(), State))));

			return GetDefaultReturnValue(method);
		}

		protected abstract StateMachine<TState, TTrigger> InitializeStates();
		
		protected static StateMachine<TState, TTrigger> NewStateMachine(IEnumerable<StateMachine<TState, TTrigger>.StateInfo> states, TState initial = default(TState))
		{
			return new StateMachine<TState, TTrigger>(states, initial);
		}

		protected static StateMachine<TState, TTrigger>.StateInfo NewState(TState state, TState? parent, StateMachine<TState, TTrigger>.Transition[] transitions, StateMachine<TState, TTrigger>.StateChangedHandler entered = null, StateMachine<TState, TTrigger>.StateChangedHandler exited = null)
		{
			return new StateMachine<TState, TTrigger>.StateInfo(state, parent, transitions, entered, exited);
		}

		protected static StateMachine<TState, TTrigger>.Transition NewTransition(TTrigger trigger, TState target, StateMachine<TState, TTrigger>.StateTransitionConditionHandler condition = null, Action<TState> setupState = null)
		{
			return new StateMachine<TState, TTrigger>.Transition(trigger, target, condition, setupState);
		}

		private Dictionary<TState, List<Action>> _watchers = new Dictionary<TState, List<Action>>();

		/// <summary> Calls back when actor leaves state (immediately if not in given state). </summary>
		public void WatchState(TState state, Action callback)
		{
			if (!InState(state))
				callback();
			else
			{
				List<Action> bucket;
				if (_watchers.TryGetValue(state, out bucket))
					bucket.Add(callback);
				else
					_watchers.Add(state, new List<Action> { callback });
			}
		}

		protected void RefreshWhileInState(int milliseconds, Action<float> callback, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			var watch = new System.Diagnostics.Stopwatch();
			watch.Start();
			var timer = new System.Threading.Timer
				(
					s => Act(() => 
						{
							#if (TRACE_TIMERS)
							Framework.Logging.Trace("Timer", GetType().Name + ": Refresh triggered after " + watch.ElapsedMilliseconds + "ms.");
							#endif
							if (InState(inState))
							{ 
								var ellapsed = (float)watch.ElapsedTicks / (float)System.Diagnostics.Stopwatch.Frequency;
								watch.Restart(); 
								callback(ellapsed); 
							}
						}), 
					null, 
					milliseconds, 
					milliseconds
				);
			WatchState
			(
				inState,
				() =>
				{
					#if (TRACE_TIMERS)
					Framework.Logging.Trace("Timer", GetType().Name + ": Refresh terminating due to state change.");
					#endif
					timer.Dispose();
				}
			);
		}

		protected void TimeoutWhileInState(int milliseconds, Action callback, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			System.Threading.Timer timer = null;
			timer = new System.Threading.Timer
				(
					s => Act(() =>
					{
						#if (TRACE_TIMERS)
						Framework.Logging.Trace("Timer", GetType().Name + ": Timeout triggered.");
						#endif
						if (InState(inState))
							callback();
						timer.Dispose();
					}),
					null,
					milliseconds, 
					System.Threading.Timeout.Infinite
				);
			WatchState
			(
				inState,
				() =>
				{
					#if (TRACE_TIMERS)
					Framework.Logging.Trace("Timer", GetType().Name + ": Timeout terminating due to state change.");
					#endif
					timer.Dispose();
				}
			);
		}

		protected void WatchOtherWhileInState<OS, OT>(IStatefulActor<OS, OT> other, WatchOtherCondition<OS, OT> condition, Action action, TState? whileIn = null)
			where OS : struct
		{
			var inState = whileIn ?? State;
			if (InState(inState))
			{
				StateMachine<OS, OT>.StateChangedHandler changedHandler = (OS oldState, StateMachine<OS, OT>.Transition transition) =>
				{
					Act(() =>
						{
							if (InState(inState) && condition(transition.Target, transition))
								action();
						}
					);
				};
				other.StateChanged += changedHandler;
				WatchState(inState, () => { other.StateChanged -= changedHandler; });

				if (condition(other.State, null))
					action();
			}
		}

        protected void WatchOtherAndUpdate<OS, OT>(IStatefulActor<OS, OT> other, TState? whileIn = null)
            where OS : struct
        {
            WatchOtherWhileInState(other, (s, t) => true, UpdateStates, whileIn);
        }
    }

	public delegate bool WatchOtherCondition<OS, OT>(OS newState, StateMachine<OS, OT>.Transition transition)
		where OS : struct;
}
