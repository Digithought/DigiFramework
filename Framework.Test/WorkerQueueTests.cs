using Microsoft.VisualStudio.TestTools.UnitTesting;
using Digithought.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Digithought.Framework.Tests
{
	[TestClass()]
	public class WorkerQueueTests
	{
		[TestMethod()]
		public void CurrentThreadOnTest()
		{
			var inCheck = false;
			var outCheck = false;
			var blocker = new ManualResetEvent(false);
			var worker = new WorkerQueue();

			worker.Queue(() => { inCheck = worker.CurrentThreadOn(); blocker.Set(); });
			blocker.WaitOne();

			outCheck = worker.CurrentThreadOn();

			Assert.IsTrue(inCheck);
			Assert.IsFalse(outCheck);
		}

		[TestMethod()]
		public void QueueTest()
		{
			var sequence = 0;
			var blocker = new ManualResetEvent(false);
			var worker = new WorkerQueue();

			worker.Queue(() => { Assert.AreEqual(0, sequence); Thread.Sleep(100); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(1, sequence); Thread.Sleep(10); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(2, sequence); Thread.Sleep(0); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(3, sequence); Thread.Sleep(50); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(4, sequence); Thread.Sleep(150); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(5, sequence); Thread.Sleep(1); ++sequence; blocker.Set(); });
			blocker.WaitOne();

			Assert.AreEqual(6, sequence);
		}

		[TestMethod()]
		public void ExecuteTest()
		{
			var sequence = 0;
			var worker = new WorkerQueue();

			worker.Execute(() => { Assert.AreEqual(0, sequence); Thread.Sleep(10); ++sequence; });
			worker.Execute(() => { Assert.AreEqual(1, sequence); Thread.Sleep(10); ++sequence; });
			worker.Execute(() => { Assert.AreEqual(2, sequence); Thread.Sleep(10); ++sequence; });
			worker.Execute(() => { Assert.AreEqual(3, sequence); Thread.Sleep(10); ++sequence; });
			worker.Execute(() => { Assert.AreEqual(4, sequence); Thread.Sleep(10); ++sequence; });

			Assert.AreEqual(5, sequence);
		}

		[TestMethod()]
		public void ClearTest()
		{
			var sequence = 0;
			var blocker = new ManualResetEvent(true);
			var worker = new WorkerQueue();

			worker.Queue(() => { Assert.AreEqual(0, sequence); blocker.WaitOne(); worker.Clear(); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(1, sequence); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(2, sequence); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(3, sequence); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(4, sequence); ++sequence; });
			worker.Queue(() => { Assert.AreEqual(5, sequence); ++sequence; });
			Thread.Sleep(100);

			Assert.AreEqual(1, sequence);
		}
	}
}