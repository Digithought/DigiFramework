using System;
using System.Diagnostics;
using System.Reflection;

namespace Digithought.Framework
{
	/// <summary> Provides a base actor that manages its proxy and handles its own errors. </summary>
	/// <typeparam name="TActor"> The specific actor interface being implemented. </typeparam>
	public abstract class ActorBase<TActor> : IActor<TActor>
		where TActor : IActor<TActor>
	{
		private readonly TActor _actor;
		private readonly WorkerQueue _worker;

		public ActorBase(WorkerQueue worker = null, System.Threading.ThreadPriority? priority = null)
		{
			_actor = ProxyBuilder.Create<TActor>(this.Invoke);
			_worker = worker ?? new WorkerQueue(priority == null ? System.Threading.ThreadPriority.Normal : priority.Value);
		}

		protected WorkerQueue Worker
		{
			get {  return _worker; }
		}

		/// <summary> Occurs when an error happens while processing an actor message. </summary>
		public event Action<Exception> ErrorOccurred;

		/// <summary> The proxy for the actor with which to send "messages" (invoke calls on) the actor. </summary>
		public TActor Actor
		{
			get { return this._actor; }
		}

		/// <summary> Act provides a generic way to execute something within the context of the actor. </summary>
		/// <remarks> This is used within actor implementation when a message needs to be dispatched onto the 
		/// actor's thread.  Another use case is to invoke something after the current logical step completes.
		public void Act(Action action)
		{
			_worker.Queue(() => InvokeHandlingErrors(action));
		}

		public void Atomically(Action<TActor> action)
		{
			Act(() => action(Actor));
		}

		/// <summary> Allows an implementation to override the handling of a fault. </summary>
		/// <remarks> There is no default behavior. </remarks>
		protected abstract void HandleFault(Exception e);

		/// <summary> Allows an implementation to override the handling of a timeout. </summary>
		/// <remarks> The default behavior is to treat timeouts as a fault. </remarks>
		protected virtual void HandleTimeout(Exception e)
		{
			HandleFault(e);
		}

		/// <summary> Allows an implementation to override the handling of internal errors (not timeouts or faults). </summary>
		/// <remarks> There is no default behavior. </remarks>
		protected abstract void HandleError(Exception e);

		/// <summary> Called when any error occurs. </summary>
		protected virtual void HandleException(Exception e)
		{
			try
			{
				Logging.Error(e);

				if (e is FrameworkFault)
					HandleFault(e);
				else if (e is FrameworkTimeout)
					HandleTimeout(e);
				else
					HandleError(e);

				if (ErrorOccurred != null)
					ErrorOccurred(e);
			}
			catch (Exception secondary)
			{
				// Don't let secondary problem in error handling propagate
				Trace.WriteLine("Error handling exception: " + secondary.Message);
				#if (DEBUG)
				// Error handling should not be throwing exceptions... fix the problem
				Debugger.Break();
				#endif
			}
		}

		protected virtual void InvokeHandlingErrors(Action call)
		{
			try
			{
				call();
			}
			catch (Exception e)
			{
				HandleException(e);
			}
		}

		private static void UnravelTargetException(Action call)
		{
			// Exceptions from dynamic invoke calls are wrapped in a TargetInvocationException, we must unravel them
			try
			{
				call();
			}
			catch (TargetInvocationException e)
			{
				throw e.InnerException;
			}
		}

		// All actor calls come through this method
		// WARNING: this occurs in a different thread in general
		public virtual object Invoke(MethodInfo method, params object[] parameters)
		{
			#if (TRACE_ACTS)
			// REPLACED FOR PERFORMANCE: Newtonsoft.Json.JsonConvert.SerializeObject(parameters));
			// Use this rather than ToString() if more detailed parameter logging is needed
			Logging.Trace(FrameworkLoggingCategory.Acts, "Call to " + GetType().Name + "[" + GetHashCode() + "]." + method.Name + "(" + String.Join(",", parameters) + ")");
			#endif

			object result = null;
			Action work;
			var voidReturn = method.ReturnType == typeof(void);
			if (voidReturn)
				work = () => InvokeHandlingErrors(() => UnravelTargetException(() => InnerInvoke(() => method.Invoke(this, parameters), method, parameters)));
			else
			{
				result = GetDefaultReturnValue(method);
				work = () => InvokeHandlingErrors(() => UnravelTargetException(() => InnerInvoke(() => { result = method.Invoke(this, parameters); }, method, parameters)));
			}
			// Perform synchronously or asynchronously depending on whether the current thread is the actor's
			if (_worker.CurrentThreadOn())
				work();
			else
			{
				if (voidReturn)
					_worker.Queue(work);
				else
					_worker.Execute(work);
			}

			return result;
		}

		/// <summary> This performs the actual invocation, already within the actor thread.  Use this for conditional execution or other cross-cutting operations that might access actor state. </summary>
		protected virtual void InnerInvoke(Action defaultInvocation, MethodInfo method, params object[] parameters)
		{
			defaultInvocation();
		}

		protected static object GetDefaultReturnValue(MethodInfo method)
		{
			if (method.ReturnType != typeof(void) && method.ReturnType.IsValueType && (method.ReturnType.IsPrimitive || !method.ReturnType.IsNotPublic))
				return Activator.CreateInstance(method.ReturnType);
			else
				return null;
		}

		protected void DoIn(int milliseconds, Action callback)
		{
			System.Threading.ThreadPool.QueueUserWorkItem(s => { System.Threading.Thread.Sleep(milliseconds); Act(callback); });
		}

		protected void Continue<T>(System.Threading.Tasks.Task<T> task, Action<T> action)
		{
			task.ContinueWith(t =>
				{
					try
					{
						var result = t.Result;
						Act(() => action(result));
					}
					catch (Exception e)
					{
						Logging.Error(e);
						Act(() => { throw e; });
					}
				}
			);
		}

		protected void Continue(System.Threading.Tasks.Task task, Action action)
		{
			task.ContinueWith(t =>
			{
				try
				{
					Act(() => action());
				}
				catch (Exception e)
				{
					Logging.Error(e);
					Act(() => { throw e; });
				}
			}
			);
		}
	}
}