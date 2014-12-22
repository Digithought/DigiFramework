using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Digithought.Framework
{
	/// <summary> Queues work onto a single worker thread. </summary>
	/// <remarks> This is based on the WorkerQueue in Dataphor. </remarks>
	public class WorkerQueue
	{
		private Queue<System.Action> _asyncQueue = new Queue<System.Action>();
		private Thread _asyncThread;
		private ThreadPriority _priority;

		/// <summary> Event which is executed (on the async thread) each time the async operation queue becomes empty. </summary>
		public event EventHandler AsyncOperationsComplete;

		/// <summary> Event which is executed (on the async thread) each time the async operation queue begins to be serviced. </summary>
		public event EventHandler AsyncOperationsStarted;

		public ThreadPriority Priority
		{
			get { return _priority; }
		}

		public WorkerQueue(ThreadPriority priority = ThreadPriority.Normal)
		{
			_priority = priority;
		}

		public void Queue(System.Action action)
		{
			if (action != null)
			{
				lock (_asyncQueue)
				{
					_asyncQueue.Enqueue(action);

					if (_asyncThread == null)
					{
						_asyncThread = new Thread(new ThreadStart(AsyncQueueServiceThread));
						_asyncThread.Priority = _priority;
						_asyncThread.Start();
					}
				}
			}
		}

		public void Execute(System.Action action)
		{
			var waitFor = new ManualResetEvent(false);
			Exception error = null;
			Queue
			(
				() =>
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						error = e;
					}
					finally
					{
						waitFor.Set();
					}
				}
			);
			waitFor.WaitOne();
			waitFor.Close();
			if (error != null)
			{
				throw error;
			}
		}

		private void AsyncQueueServiceThread()
		{
			DoAsyncOperationsStarted();

			while (true)
			{
				System.Action nextAction;
				lock (_asyncQueue)
				{
					if (_asyncQueue.Count > 0)
						nextAction = _asyncQueue.Dequeue();
					else
					{
						_asyncThread = null;
						break;
					}
				}
				try
				{
					nextAction();
				}
				catch (Exception exception)
				{
					System.Diagnostics.Debug.WriteLine(exception.ToString());
					// Don't allow exceptions to leave this thread or the application will terminate
				}
			}

			DoAsyncOperationsComplete();
		}

		private void DoAsyncOperationsStarted()
		{
			try
			{
				if (AsyncOperationsStarted != null)
					AsyncOperationsStarted(this, EventArgs.Empty);
			}
			catch (Exception exception)
			{
				System.Diagnostics.Debug.WriteLine(exception.ToString());
				// Don't allow exceptions to leave this thread or the application will terminate
			}
		}

		/// <summary> Notifies that all async operations are complete. </summary>
		private void DoAsyncOperationsComplete()
		{
			try
			{
				if (AsyncOperationsComplete != null)
					AsyncOperationsComplete(this, EventArgs.Empty);
			}
			catch (Exception exception)
			{
				System.Diagnostics.Debug.WriteLine(exception.ToString());
				// Don't allow exceptions to leave this thread or the application will terminate
			}
		}

		/// <summary> Take no further action after the current worker action completes. </summary>
		public void Clear()
		{
			lock (_asyncQueue)
			{
				_asyncQueue.Clear();
			}
		}

		/// <summary> Executes a set of actions, returning only when all actions have completed (or errored). </summary>
		/// <remarks> In the case of one or more exceptions being thrown by actions, the last exception
		/// throw will be re-thrown on the calling thread. </remarks>
		public void DoMany(Action[] actions)
		{
			// Create a set of wait handles
			var events = Enumerable.Range(0, actions.Length).Select(e => new ManualResetEvent(false)).ToArray();
			
			Exception exception = null;
			for (int i = 0; i < actions.Length; i++)
			{
				var action = actions[i];
				var resetEvent = events[i];
				if (action != null)
				{
					ThreadPool.QueueUserWorkItem
					(
						(s) =>
						{
							try
							{
								action();
							}
							catch (Exception localException)
							{
								exception = localException;
							}
							resetEvent.Set();
						}
					);
				}
			}

			WaitHandle.WaitAll(events);

			// Clean up all wait handles
			foreach (var resetEvent in events)
				resetEvent.Close();

			// Re-throw last caught exception
			if (exception != null)
				throw exception;
		}
	}
}
