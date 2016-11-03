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
        public static void UnravelTargetException(Action call)
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

        public static T UnravelTargetException<T>(Func<T> call)
        {
            // Exceptions from dynamic invoke calls are wrapped in a TargetInvocationException, we must unravel them
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
