using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Digithought.Framework
{
	/// <summary> Provides a base actor that manages its proxy and handles its own errors. </summary>
	/// <typeparam name="TActor"> The specific actor interface being implemented. </typeparam>
	public abstract class ActorBase<TActor> : IActor<TActor>
		where TActor : IActor<TActor>
	{
		private readonly TActor _actor;
		private readonly IWorkerQueue _worker;

		public ActorBase(IWorkerQueue worker = null, System.Threading.ThreadPriority? priority = null)
		{
			_actor = ProxyBuilder.Create<TActor>(this.Invoke);
			_worker = worker ?? new WorkerQueue(priority == null ? System.Threading.ThreadPriority.Normal : priority.Value);
		}

		protected IWorkerQueue Worker
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
                try
                {
                    NotifyOfError(e);
                }
                finally
                {
                    if (e is FrameworkFault)
                        HandleFault(e);
                    else if (e is FrameworkTimeout)
                        HandleTimeout(e);
                    else
                        HandleError(e);
                }
			}
			catch (Exception secondary)
			{
				// Don't let secondary problem in error handling propagate. Don't write to trace - could recurse
				Debug.WriteLine("Error handling exception: " + secondary.Message);
				#if (DEBUG)
				// Error handling should not be throwing exceptions... fix the problem
				Debugger.Break();
				#endif
			}
		}

        protected virtual void NotifyOfError(Exception e)
        {
            Logging.Error(e);
			ErrorOccurred?.Invoke(e);
		}

        protected virtual void InvokeHandlingErrors(Action call, Func<string> getContext = null)
		{
			try
			{
				call();
			}
			catch (Exception e)
			{
				if (getContext != null)
					NotifyOfError(new Exception($"Error ({(e is AggregateException ae ? ae.Flatten().Message : e.Message)}) invoking: " + getContext()));
				HandleException(e);
			}
		}

		// All actor calls come through this method
		// WARNING: this occurs in a different thread in general
		public virtual object Invoke(MethodInfo method, params object[] parameters)
		{
			#if (TRACE_ACTS)
			// REPLACED FOR PERFORMANCE: Newtonsoft.Json.JsonConvert.SerializeObject(parameters));
			// Use this rather than ToString() if more detailed parameter logging is needed
			Logging.Trace(
				IsAccessor(method) ? FrameworkLoggingCategory.Accessors : FrameworkLoggingCategory.Acts, 
				$"Call to {GetType().Name}[{GetHashCode()}].{method.Name}({String.Join(",", parameters.Select(x => x ?? ""))})"
			);
			#endif

			string GetContext()
				=> method.DeclaringType.Name + "." + method.Name;

			object result = null;
			Action work;
			var voidReturn = method.ReturnType == typeof(void);
			if (voidReturn)
				work = () => InvokeHandlingErrors(
					() => ReflectionUtility.UnravelTargetException(() => InnerInvoke(() => method.Invoke(this, parameters), method, parameters)),
					GetContext
				);
			else
			{
				result = GetDefaultReturnValue(method);
				work = () => InvokeHandlingErrors(
					() => ReflectionUtility.UnravelTargetException(() => InnerInvoke(() => { result = method.Invoke(this, parameters); }, method, parameters)),
					GetContext
				);
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

        /// <summary> One-time timer based callback. </summary>
		protected void Timeout(int milliseconds, Action callback)
		{
			System.Threading.ThreadPool.QueueUserWorkItem(s => { System.Threading.Thread.Sleep(milliseconds); Act(callback); });
		}

		protected void Continue<T>(System.Threading.Tasks.Task<T> task, Action<T> action, Action canceled = null, Action<Exception> error = null)
		{
			task.ContinueWith(t =>
				{
					if (t.IsFaulted)
						Act(() => { 
							if (error != null) 
								error(t.Exception); 
							else
								throw t.Exception; 
						});
                    else if (t.IsCanceled)
                    {
                        if (canceled != null)
                            Act(canceled);
                    }
                    else   
                        try
					    {
						    var result = t.Result;
						    Act(() => action(result));
					    }
					    catch (Exception e)
					    {
						    Act(() => { throw e; });
					    }
				},
				System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously
			);
		}

		protected void Continue(System.Threading.Tasks.Task task, Action action, Action canceled = null, Action<Exception> error = null)
		{
			task.ContinueWith(t =>
			    {
                    if (t.IsFaulted)
						Act(() => {
							if (error != null)
								error(t.Exception);
							else
								throw t.Exception;
						});
					else if (t.IsCanceled)
                    {
                        if (canceled != null)
                            Act(canceled);
                    }
                    else
				        Act(action);
			    },
				System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously
			);
		}

		/// <summary> Accessors are defined as adds and removes from event handlers, and property getters.  Note that property getters could potentially have side effects or be important for tracing, so thise level can be turned on using the Accessors category. </summary>
		private bool IsAccessor(MethodInfo method)
			=> method.IsSpecialName && (method.Name.StartsWith("add_") || method.Name.StartsWith("remove_") || method.Name.StartsWith("get_"));
	}
}