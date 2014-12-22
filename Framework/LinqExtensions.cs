using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework
{
    public static class LinqExtensions
    {
        /// <summary> Flattens a list of nested items. </summary>
        public static IEnumerable<TResult> Expand<TSource, TResult>(this IEnumerable<TSource> items, Func<TSource, IEnumerable<TResult>> children)
        {
            foreach (var item in items)
                foreach (var child in children(item))
                    yield return child;
        }

        /// <summary> Removes the specified items by the given predicate. </summary>
        public static void Remove<T>(this ICollection<T> items, Func<T, bool> predicate)
        {
            foreach (var toRemove in items.Where(predicate).ToList())
                items.Remove(toRemove);
        }

		/// <summary> Returns the subset of items that are distinct by the given key returning function. </summary>
		public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> getKey)
		{
			var observed = new HashSet<TKey>();
			foreach (var entry in source)
			{
				if (observed.Add(getKey(entry)))
				{
					yield return entry;
				}
			}
		}

		/// <summary> Invokes the given method for each item in the enumeration. </summary>
		public static void Each<T>(this IEnumerable<T> items, Action<T> forEach)
		{
			foreach (var i in items)
				forEach(i);
		}
	}
}
