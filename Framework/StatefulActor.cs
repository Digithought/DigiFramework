using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Digithought.Framework
{
	public abstract class StatefulActor<TActor, TState, TTrigger> : ActorBase<TActor>, IStatefulActor<TActor, TState, TTrigger>
		where TActor : IActor<TActor>
		where TState : struct
		where TTrigger : struct
	{
		public StatefulActor(WorkerQueue worker = null, System.Threading.ThreadPriority? priority = null)
			: base(worker, priority)
		{
			_states = InitializeStates();
			_states.UnhandledError = HandleException;
			_states.UnhandledTrigger = HandleFailedTrigger;
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

		/// <summary> Is invoked whenever a state transition occurs. </summary>
		public event StateMachine<TState, TTrigger>.StateChangedHandler StateChanged
		{
			add { _states.StateChanged += value; }
			remove { _states.StateChanged -= value; }
		}

		/// <summary> Tests to see if the current state is the given state, or some sub-state thereof. </summary>
		public bool InState(TState state)
		{
			return _states.InState(state);
		}

		/// <summary> Fires the given trigger against the state machine, delayed if already transitioning. </summary>
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
			Logging.Error(e);
		}

		protected virtual void HandleFailedTrigger(TTrigger trigger)
		{
			Logging.Trace(FrameworkLoggingCategory.States, GetType().Name + "[" + GetHashCode() + "]: WARNING: Trigger " + trigger + " fired and has no transitions.");
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

		protected override void InnerInvoke(Action defaultInvocation, MethodInfo method, params object[] parameters)
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
					Fire(command.Trigger.Value);
				}
				else
					defaultInvocation();
			}
			else
				StateException(new FrameworkException(String.Format("Invalid command '{0}' against {1} [{2}] when in state {3}.", method.Name, GetType().Name, GetHashCode(), State)));
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

		/// <summary> Calls back when actor leaves the given state (immediately if not in given state). </summary>
		public void WatchState(TState state, Action callback)
		{
			if (!InState(state))
				callback();
			else
				InternalWatchState(state, callback);
		}

		/// <summary> Calls back when actor leaves the current state. </summary>
		public void WatchState(Action callback)
		{
			InternalWatchState(State, callback);
		}

		private void InternalWatchState(TState state, Action callback)
		{
			List<Action> bucket;
			if (_watchers.TryGetValue(state, out bucket))
				bucket.Add(callback);
			else
				_watchers.Add(state, new List<Action> { callback });
		}

		/// <summary> Repeatedly invokes the a callback while in the current state 
		/// (or optionally given super-state). </summary>
		protected void RepeatWhileInState(int milliseconds, Action<float> callback, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			var watch = new System.Diagnostics.Stopwatch();
			watch.Start();
            var leftState = false;  // use flag rather than check to avoid falsely firing upon quick re-entry of state
            var timer = new System.Threading.Timer
				(
					s => Act(() => 
						{
							#if (TRACE_TIMERS)
							Framework.Logging.Trace("Timer", GetType().Name + ": Refresh triggered after " + watch.ElapsedMilliseconds + "ms.");
							#endif
							if (!leftState)
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
                    leftState = true;
					timer.Dispose();
				}
			);
		}

		/// <summary> Calls back (once) after a given interval if still in the current state 
		/// (or optionally given super-state). </summary>
		/// <remarks> If the callback is omitted, a timeout exception is thrown. </remarks>
		protected void TimeoutWhileInState(int milliseconds, Action callback = null, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			System.Threading.Timer timer = null;
            var leftState = false;  // use flag rather than check to avoid falsely firing upon quick re-entry of state
            timer = new System.Threading.Timer
				(
					s => Act(() =>
					{
						try
						{
							#if (TRACE_TIMERS)
							Framework.Logging.Trace("Timer", GetType().Name + ": Timeout triggered.");
							#endif
							if (!leftState)
							{
								leftState = true;	// under no circumstances, call back again
								if (callback != null)
									callback();
								else
									throw new FrameworkTimeout(GetType().Name + ": Timeout in state " + inState);
							}
						}
						finally
						{
							timer.Dispose();
						}
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
					leftState = true;
					timer.Dispose();
				}
			);
		}

		/// <summary> Checks a given condition whenever the given other actor changes state; if 
		/// the condition passes, a given action is invoked, but all of this only while in the 
		/// current state (or given super-state). </summary>
		/// <remarks> If the condition is already met, the callback is invoked immediately with null transition information. </remarks>
		protected void WatchOtherWhileInState<OA, OS, OT>(IStatefulActor<OA, OS, OT> other, WatchOtherCondition<OS, OT> condition, Action action, TState? whileIn = null)
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

		/// <summary> Checks to see if the current states transition's conditions are satisfied 
		/// in response to any state change in the given other actor, but only while in the 
		/// current state (or optionally given super-state). </summary>
		protected void WatchOtherAndUpdate<OA, OS, OT>(IStatefulActor<OA, OS, OT> other, TState? whileIn = null)
			where OS : struct
		{
			WatchOtherWhileInState(other, (s, t) => true, UpdateStates, whileIn);
		}

		/// <summary> Checks to see if the current states transition's conditions are satisfied 
		/// in response to any state change in the given other actor, but only while in the 
		/// current state (or optionally given super-state).  If the other actor goes to a given
		/// fault state, an exception is thrown for this actor. </summary>
		protected void WatchOtherAndUpdate<OA, OS, OT>(IStatefulActor<OA, OS, OT> other, OS errorState, TState? whileIn = null)
			where OS : struct
		{
			WatchOtherWhileInState
			(
				other, 
				(s, t) => true, 
				() =>
				{
					if (other.InState(errorState))
						throw new FrameworkWatchedStateException(other.GetType().Name + " unexpectedly went to state " + errorState, other);
					else
						UpdateStates();
				}, 
				whileIn
			);
		}

		/// <summary> Continues with a given delegate once the given task completes, but only 
		/// if still in the current state (or optionally given super-state). </summary>
		protected void ContinueWhileInState<T>(Task<T> task, Action<T> with, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			var leftState = false;
			WatchState(inState, () => { leftState = true; });
			if (InState(inState))
				Continue(task, v => { if (!leftState && InState(inState)) with(v); }, () => { throw new FrameworkException("Task canceled"); });
		}

		/// <summary> Continues with a given delegate once the given task completes, but only 
		/// if still in the current state (or optionally given super-state). </summary>
		protected void ContinueWhileInState(Task task, Action with, TState? whileIn = null)
		{
			var inState = whileIn ?? State;
			var leftState = false;
			WatchState(inState, () => { leftState = true; });
			if (InState(inState))
				Continue(task, () => { if (!leftState && InState(inState)) with(); }, () => { throw new FrameworkException("Task canceled"); });
		}
	}

	public delegate bool WatchOtherCondition<OS, OT>(OS newState, StateMachine<OS, OT>.Transition transition)
		where OS : struct;
}
