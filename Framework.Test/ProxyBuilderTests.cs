using System;
using System.Reflection;
using Moq;
using Digithought.Framework;
using NUnit.Framework;

namespace WeedebudNet.Tests
{
	[TestFixture]
	public class ProxyBuilderTests
	{
		object o1 = new Object();
		object o2 = new Object();
		object o3 = new Object();

		public interface IMethodInterface
		{
			int GetValue(int valueArg1, float valueArg2);
			object GetReference(object referenceArg1, object referenceArg2);
			void GetVoid();
		}

		public interface IInvoker
		{
			object Invoke(MethodInfo method, params object[] parameters);
		}

		[Test]
		public void MethodInvocationTest()
		{
			var mockInvoker = new Mock<IInvoker>();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IMethodInterface).GetMethod("GetValue")), It.Is<object[]>(p => p.Length == 2 && (int)p[0] == 1 && (float)p[1] == 2f)))
				.Returns(5);
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IMethodInterface).GetMethod("GetReference")), It.Is<object[]>(p => p.Length == 2 && p[0] == o1 && p[1] == o2)))
				.Returns(o3);
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IMethodInterface).GetMethod("GetVoid")), It.Is<object[]>(p => p.Length == 0)))
				.Returns(null)
				.Verifiable();

			var invoker = mockInvoker.Object;
			var proxy = ProxyBuilder.Create<IMethodInterface>(invoker.Invoke);
			Assert.AreEqual(5, proxy.GetValue(1, 2f));
			Assert.AreEqual(o3, proxy.GetReference(o1, o2));
			proxy.GetVoid();

			mockInvoker.Verify();
		}

		public interface IPropertyInterface
		{
			int Value { get; set; }
			object Reference { get; set; }
		}

		[Test]
		public void PropertyEvaluationTest()
		{
			var mockInvoker = new Mock<IInvoker>();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IPropertyInterface).GetMethod("get_Value")), It.Is<object[]>(p => p.Length == 0)))
				.Returns(5);
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IPropertyInterface).GetMethod("set_Value")), It.Is<object[]>(p => p.Length == 1 && (int)p[0] == 123)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IPropertyInterface).GetMethod("get_Reference")), It.Is<object[]>(p => p.Length == 0)))
				.Returns(o1);
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IPropertyInterface).GetMethod("set_Reference")), It.Is<object[]>(p => p.Length == 1 && p[0] == o2)))
				.Verifiable();

			var invoker = mockInvoker.Object;
			var proxy = ProxyBuilder.Create<IPropertyInterface>(invoker.Invoke);
			Assert.AreEqual(5, proxy.Value);
			proxy.Value = 123;
			Assert.AreEqual(o1, proxy.Reference);
			proxy.Reference = o2;

			mockInvoker.Verify();
		}

		public interface IEventInterface
		{
			event Func<int, int> ValueEvent;
			event Func<object, object> ReferenceEvent;
			event Action VoidEvent;
		}

		private int ValueMethod(int v)
		{
			throw new NotImplementedException();
		}

		private object ReferenceMethod(object o)
		{
			throw new NotImplementedException();
		}

		private void VoidMethod()
		{
			throw new NotImplementedException();
		}

		[Test]
		public void EventEvaluationTest()
		{
			var mockInvoker = new Mock<IInvoker>();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("add_ValueEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Func<int, int>)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("remove_ValueEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Func<int, int>)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("add_ReferenceEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Func<object, object>)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("remove_ReferenceEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Func<object, object>)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("add_VoidEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Action)))
				.Verifiable();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m == typeof(IEventInterface).GetMethod("remove_VoidEvent")), It.Is<object[]>(p => p.Length == 1 && p[0] is Action)))
				.Verifiable();

			var invoker = mockInvoker.Object;
			var proxy = ProxyBuilder.Create<IEventInterface>(invoker.Invoke);
			proxy.ValueEvent += ValueMethod;
			proxy.ValueEvent -= ValueMethod;
			proxy.ReferenceEvent += ReferenceMethod;
			proxy.ReferenceEvent -= ReferenceMethod;
			proxy.VoidEvent += VoidMethod;
			proxy.VoidEvent -= VoidMethod;

			mockInvoker.Verify();
		}

		public interface IGenericBase<T>
		{
			T MethodOfT(T arg1);
			//TA BaseMethod<TA>(TA arg1);
		}

		public interface IGenericDerived : IGenericBase<int>
		{
			//TB DerivedMethod<TB>(TB arg1);
		}

		[Test]
		public void GenericMethodInvocationTest()
		{
			var mockInvoker = new Mock<IInvoker>();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m.Name == "MethodOfT"), It.Is<object[]>(p => p.Length == 1 && (int)p[0] == 1)))
				.Returns(5);
			//mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m.Name == "BaseMethod"), It.Is<object[]>(p => p.Length == 1 && (int)p[0] == 1)))
			//    .Returns(5);
			//mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m.Name == "DerivedMethod"), It.Is<object[]>(p => p.Length == 1 && (int)p[0] == 1)))
			//    .Returns(5);

			var invoker = mockInvoker.Object;
			var proxy = ProxyBuilder.Create<IGenericDerived>(invoker.Invoke);
			Assert.AreEqual(5, proxy.MethodOfT(1));
			//Assert.AreEqual(5, proxy.BaseMethod(1));
			//Assert.AreEqual(5, proxy.DerivedMethod(1));
		}

		public enum TestEnum
		{
			One,
			Two
		}

		public interface IEnum<T>
		{
			T ReturnEnum(T arg1);
		}

		[Test]
		public void EnumMethodInvocationTest()
		{
			var mockInvoker = new Mock<IInvoker>();
			mockInvoker.Setup(mock => mock.Invoke(It.Is<MethodInfo>(m => m.Name == "ReturnEnum"), It.Is<object[]>(p => p.Length == 1 && (TestEnum)p[0] == TestEnum.One)))
				.Returns(TestEnum.Two);

			var invoker = mockInvoker.Object;
			var proxy = ProxyBuilder.Create<IEnum<TestEnum>>(invoker.Invoke);
			Assert.AreEqual(TestEnum.Two, proxy.ReturnEnum(TestEnum.One));
		}
	}
}
