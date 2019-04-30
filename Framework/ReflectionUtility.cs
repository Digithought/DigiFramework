using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework
{
    public static class ReflectionUtility
    {
		/// <summary> Exceptions from dynamic invoke calls are wrapped in a TargetInvocationException, this unravels them. </summary>
		public static void UnravelTargetException(Action call)
        {
            try
            {
                call();
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

		/// <summary> Exceptions from dynamic invoke calls are wrapped in a TargetInvocationException, this unravels them. </summary>
		public static T UnravelTargetException<T>(Func<T> call)
        {
            try
            {
                return call();
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }
    }
}
