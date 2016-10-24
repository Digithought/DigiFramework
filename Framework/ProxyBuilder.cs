using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Digithought.Framework
{
	// TODO: Support generic methods (besides those parameterized by the interface)

	/// <summary> Given an interface, generates a proxy class which forwards all invocations to a single <c>IInvoker</c>. </summary>
	/// <remarks> Inspired by NAct. </remarks>
	/// <example> Given an interface like this:
	///	 <code>
	///		 public interface IHello
	///		 {
	///			 int SayHi(string message);
	///		 }
	///	 </code>
	///	 
	///	 Create&lt;IHello&gt;(invoker) generates a class of this nature and returns an instance:
	///	 <code>
	///		 public class Hello : IHello
	///		 {
	///			 private InvokerHandler invoker;
	///			 
	///			 public Hello(InvokeHandler invoker)
	///			 {
	///				 this.invoker = invoker;
	///			 }
	///			 
	///			 public int SayHi(string message)
	///			 {
	///				 return (int)invoker(methodof(SayHi), new object[] { message });
	///			 }
	///		 }
	///	 </code>
	/// </example>
	public static class ProxyBuilder
	{
		private const string AssemblyName = "DigithoughtDynamic";
		private const string ModuleName = "dynamic.dll";
		
		private static readonly MethodInfo _methodGetMethodFromHandle = 
			typeof(System.Reflection.MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(System.RuntimeMethodHandle), typeof(System.RuntimeTypeHandle) });
		private static readonly ConstructorInfo _objectConstructor = typeof(object).GetConstructor(new Type[0]);
		private static readonly MethodInfo _invokeHandlerInvoke = typeof(InvokeHandler).GetMethod("Invoke");
		private static readonly AssemblyBuilder _assembly = 
			AppDomain.CurrentDomain.DefineDynamicAssembly
			(
				new AssemblyName(AssemblyName),
				#if (DEBUG_PROXYBUILDER)
				AssemblyBuilderAccess.RunAndSave
				#else 
				AssemblyBuilderAccess.RunAndCollect
				#endif
			);
		private static readonly ModuleBuilder _module;
		private static readonly object _moduleLock = new object();
		private static int _classIndex = 1;

		static ProxyBuilder()
		{
			_module = _assembly.DefineDynamicModule(ModuleName, ModuleName);
		}

		/// <summary> Given an interface type (<c>T</c>), returns a proxy class which implements that interface and routes all calls to the given <c>invoker</c>. </summary>
		/// <typeparam name="T"> Proxy interface. </typeparam>
		/// <param name="invoker"> Instance of <c>IInvoker</c> to which all calls will be routed. </param>
		/// <param name="options"> Optional configuration parameters. </param>
		/// <returns> Proxied implementation of interface <c>T</c>. </returns>
		public static T Create<T>(InvokeHandler invoker, ProxyOptions options = null)
		{
			if (!typeof(T).IsInterface)
			{
				throw new InvalidOperationException("Cannot create a proxy for non-interface type " + typeof(T).Name);
			}

			if (typeof(T).IsNotPublic)
			{
				throw new InvalidOperationException("Cannot create a proxy for non-public interface " + typeof(T).Name);
			}

			if (options == null)
				options = new ProxyOptions();

			var builder = GetTypeBuilder<T>(typeof(T).ToString(), options.Parent);

			var invokerField = builder.DefineField("invoker", typeof(InvokeHandler), FieldAttributes.Private | FieldAttributes.InitOnly);

			foreach (var method in 
				GetPublicMethods(typeof(T))
					.Concat(options?.AdditionalInterfaces?.SelectMany(ai => GetPublicMethods(ai)) ?? new MethodInfo[0]
				)
			)
			{
				BuildMethodInvoker(builder, invokerField, method);
			}

			BuildConstructor(builder, invokerField);

			// Implement the interface, including properties and events
			builder.AddInterfaceImplementation(typeof(T));
			if (options.AdditionalInterfaces != null)
			{
				foreach (var i in options.AdditionalInterfaces)
				{
					builder.AddInterfaceImplementation(i);
				}
			}

			var type = builder.CreateType();

			#if (DEBUG_PROXYBUILDER)
			module.CreateGlobalFunctions();
			_assembly.Save("debug.dll");
			#endif

			return (T)Activator.CreateInstance(type, new object[] { invoker });
		}

		private static void BuildConstructor(TypeBuilder type, FieldBuilder invokerField)
		{
			var builder = type.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { typeof(InvokeHandler) });

			ILGenerator generator = builder.GetILGenerator();

			// base()
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Call, _objectConstructor);

			// this.invoker = invoker 
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Stfld, invokerField);

			generator.Emit(OpCodes.Ret);
		}

		private static void BuildMethodInvoker(TypeBuilder type, FieldBuilder invokerField, MethodInfo method)
		{
			var builder = type.DefineMethod(method.Name, method.Attributes & ~MethodAttributes.Abstract, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());

			ILGenerator generator = builder.GetILGenerator();
			var parameters = method.GetParameters();

			// Name the parameters (useful metadata)
			parameters.Each(p => builder.DefineParameter(p.Position + 1, ParameterAttributes.None, p.Name));

			// Push invoker instance from the invoker field
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldfld, invokerField);

			// Push method info
			generator.Emit(OpCodes.Ldtoken, method);
			generator.Emit(OpCodes.Ldtoken, method.DeclaringType);
			generator.Emit(OpCodes.Call, _methodGetMethodFromHandle);
			generator.Emit(OpCodes.Castclass, typeof(MethodInfo));

			// Push parameter array
			generator.Emit(OpCodes.Ldc_I4, parameters.Length);
			generator.Emit(OpCodes.Newarr, typeof(object));
			for (int i = 0; i < parameters.Length; i++)
			{
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Ldc_I4, i);

				generator.Emit(OpCodes.Ldarg, i + 1);
				if (parameters[i].ParameterType.IsValueType)
				{
					generator.Emit(OpCodes.Box, parameters[i].ParameterType);
				}

				generator.Emit(OpCodes.Stelem_Ref);
			}

			// Make the call
			generator.Emit(OpCodes.Call, _invokeHandlerInvoke);

			if (builder.ReturnType == typeof(void))
			{
				// Junk the result if void
				generator.Emit(OpCodes.Pop);
			}
			else
			{
				// Unbox result if needed
				if (builder.ReturnType.IsValueType)
				{
					generator.Emit(OpCodes.Unbox_Any, builder.ReturnType);
				}
			}

			generator.Emit(OpCodes.Ret);
		}

		/// <summary> Given a type, returns all public methods and all public methods of all implemented interfaces. </summary>
		private static IEnumerable<MethodInfo> GetPublicMethods(Type type)
		{
			return type.GetInterfaces().SelectMany(i => i.GetMethods())
				.Concat(type.GetMethods());
		}

		/// <summary> Generates a new, uniquely named type builder for the proxy. </summary>
		private static TypeBuilder GetTypeBuilder<T>(string name, Type parent)
		{
			lock (_moduleLock)
			{
				_classIndex++;
				return _module.DefineType("Proxy_" + name + "_" + _classIndex, TypeAttributes.Class | TypeAttributes.Public, parent);
			}
		}
	}

	public delegate object InvokeHandler(MethodInfo method, params object[] parameters);
}
