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

		public static T Aggregate<T>(this IList<T> list, Func<T, T, T> apply, Func<IList<T>, T> initialize = null, Func<IList<T>, T, T> finalize = null, Func<T> empty = null)
		{
			if (list == null || list.Count == 0)
				if (empty != null)
					return empty();
				else
					throw new ArgumentException("Cannot aggregate over an empty list.");

			var value = initialize == null ? list[0] : initialize(list);

			for (var i = 1; i < list.Count; ++i)
				value = apply(value, list[i]);

			return finalize == null ? value : finalize(list, value);
		}

		public static R Aggregate<T, R>(this IList<T> list, Func<IList<T>, R> initialize, Func<R, T, R> apply, Func<IList<T>, R, R> finalize = null, Func<R> empty = null)
		{
			if (list == null || list.Count == 0)
				if (empty != null)
					return empty();
				else
					throw new ArgumentException("Cannot aggregate over an empty list.");

			var value = initialize(list);

			for (var i = 1; i < list.Count; ++i)
				value = apply(value, list[i]);

			return finalize == null ? value : finalize(list, value);
		}

		public static IList<IList<T>> Group<T>(this IList<T> items, Func<T, T, bool> compare)
		{
			var result = new List<IList<T>>();
			var group = Enumerable.Range(0, items.Count).Select(r => -1).ToArray();    // contains result indexes - all initialized to -1
			for (var i = 0; i < items.Count; i++)
			{
				for (var j = i + 1; j < items.Count; j++)
					if (compare(items[i], items[j]))
						if (group[i] == -1)
						{
							if (group[j] == -1)
							{
								result.Add(new List<T> { items[i], items[j] });
								group[i] = group[j] = result.Count - 1;
							}
							else
							{
								result[group[j]].Add(items[i]);
								group[i] = group[j];
							}
						}
						else
						{
							if (group[j] == -1)
							{
								result[group[i]].Add(items[j]);
								group[j] = group[i];
							}
							else if (group[i] != group[j])
							{
								var a = Math.Min(group[i], group[j]);
								var b = Math.Max(group[i], group[j]);
								result[b].Each(r => result[a].Add(r));
								result.RemoveAt(b);
								for (var x = 0; x < group.Length; x++)
									if (group[x] == b)
										group[x] = a;
									else if (group[x] > b)
										group[x]--;
							}
						}
				if (group[i] == -1)
					result.Add(new List<T> { items[i] });
			}
			return result;
		}

		public static int IndexOfMax<T>(this IEnumerable<T> items, Func<T, double> eval)
		{
			var bestIndex = -1;
			double bestValue = double.MinValue;
			var i = 0;
			foreach (var item in items)
			{
				var value = eval(item);
				if (value > bestValue)
				{
					bestIndex = i;
					bestValue = value;
				}
				++i;
			}
			return bestIndex;
		}

		public static T ValueOfMax<T>(this IEnumerable<T> items, Func<T, double> eval, T initialItem = default(T))
		{
			var bestItem = initialItem;
			double bestValue = double.MinValue;
			foreach (var item in items)
			{
				var value = eval(item);
				if (value > bestValue)
				{
					bestItem = item;
					bestValue = value;
				}
			}
			return bestItem;
		}
	}
}
