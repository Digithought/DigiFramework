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
		/// <summary> This is the longest this worker will hold on to an inactive thread. </summary>
		/// <remarks> The shorter this is, the less potential thread reuse.  The longer, the more time unused thread handles are held on to. </remarks>
		private const int ReuseThreadInterval = 20000;	// 20seconds

		private Queue<System.Action> _asyncQueue = new Queue<System.Action>();
		private volatile Thread _asyncThread;
		private ManualResetEvent _asyncEvent = new ManualResetEvent(false);
		private ThreadPriority _priority;

		public ThreadPriority Priority
		{
			get { return _priority; }
		}

		public WorkerQueue(ThreadPriority priority = ThreadPriority.Normal)
		{
			_priority = priority;
		}

		/// <summary> Returns true if the current thread is on this worker. </summary>
		public bool CurrentThreadOn()
		{
			var thread = _asyncThread;
			return thread != null && thread.ManagedThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId;
		}

		/// <summary> Invokes an action to be performed on the worker.  The work is guaranteed not be be done synchronously with this call. </summary>
		public void Queue(System.Action action)
		{
			if (action != null)
			{
				lock (_asyncQueue)
				{
					_asyncQueue.Enqueue(action);
					_asyncEvent.Set();
					if (_asyncThread == null)
					{
						_asyncThread = new Thread(new ThreadStart(AsyncQueueServiceThread))
						{
							IsBackground = true,    // Don't block process exit
							Priority = _priority,
							Name = action.Method.DeclaringType.Name,
						};
						_asyncThread.Start();
					}
                }
			}
		}

		/// <summary> Invokes an action on the worker and blocks the current thread until the action completes. </summary>
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
			while (true)
			{
				System.Action nextAction;
				lock (_asyncQueue)
				{
					nextAction = _asyncQueue.Count > 0 ? _asyncQueue.Dequeue() : null;
					if (_asyncQueue.Count == 0)
						_asyncEvent.Reset();
				}
				if (nextAction != null)
					try
					{
						nextAction();
					}
					catch (Exception exception)
					{
						System.Diagnostics.Debug.WriteLine(exception.ToString());
						// Don't allow exceptions to leave this thread or the application will terminate
					}
				else
				{
					if (!_asyncEvent.WaitOne(ReuseThreadInterval))
						lock (_asyncQueue)
							if (_asyncQueue.Count == 0)
							{
								_asyncThread = null;
								break;
							}
				}
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

		/// <summary> The number of enqueued requests. </summary>
		public int Count
		{
			get
			{
				lock (_asyncQueue)
				{
					return _asyncQueue.Count;
				}
			}
		}

		/// <summary> Blocks until all requests are complete. </summary>
		public void Wait()
		{
			var e = new ManualResetEvent(false);
			Queue(() => e.Set());
			e.WaitOne();
		}
	}
}
