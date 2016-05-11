using System;
using System.Collections.Generic;
using System.Linq;

namespace Digithought.Framework
{
	public static class LinqExtensions
	{
		/// <summary> Flattens a list of nested items. </summary>
        [Obsolete("There is an equivalent method in the standard library: Enumerable.SelectMany")]
		public static IEnumerable<TResult> Expand<TSource, TResult>(this IEnumerable<TSource> items, Func<TSource, IEnumerable<TResult>> children)
		{
			foreach (var item in items)
				foreach (var child in children(item))
					yield return child;
		}

		/// <summary> Flattens a list of nested items, returning in addition, the indexes of the outer list. </summary>
		public static IEnumerable<KeyValuePair<int, TResult>> ExpandWithIndex<TSource, TResult>(this IEnumerable<TSource> items, Func<TSource, IEnumerable<TResult>> children)
		{
			var i = 0;
			foreach (var item in items)
			{
				foreach (var child in children(item))
					yield return new KeyValuePair<int, TResult>(i, child);
				++i;
			}
		}

		/// <summary> Flattens a list of nested items, returning in addition, the relative indexes of the outer and inner lists. </summary>
		public static IEnumerable<KeyValuePair<int, KeyValuePair<int, TResult>>> ExpandWithIndexes<TSource, TResult>(this IEnumerable<TSource> items, Func<TSource, IEnumerable<TResult>> children)
		{
			var i = 0;
			foreach (var item in items)
			{
				var j = 0;
				foreach (var child in children(item))
				{
					yield return new KeyValuePair<int, KeyValuePair<int, TResult>>(i, new KeyValuePair<int, TResult>(j, child));
					++j;
				}
				++i;
			}
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

		/// <summary> Invokes the given method for each item in the enumeration, including the enumeration index. </summary>
		public static void EachWithIndex<T>(this IEnumerable<T> items, Action<int, T> forEach)
		{
			var i = 0;
			foreach (var item in items)
			{
				forEach(i, item);
				++i;
			}
		}

        public static IEnumerable<TElement> Sequence<TElement>(TElement first, Func<TElement, TElement> getNext)
            where TElement : class
        {
            var current = first;
            while (current != null)
            {
                yield return current;
                current = getNext(current);
            }
        }
	}
}
