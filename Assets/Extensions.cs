using System;
using System.Collections.Generic;
using System.Linq;

public static class EnumerableExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        return source.Shuffle(new Random());
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
    {
        if (source == null) throw new ArgumentNullException("source");
        if (rng == null) throw new ArgumentNullException("rng");

        return source.ShuffleIterator(rng);
    }

    private static IEnumerable<T> ShuffleIterator<T>(
        this IEnumerable<T> source, Random rng)
    {
        var buffer = source.ToList();
        for (int i = 0; i < buffer.Count; i++)
        {
            int j = rng.Next(i, buffer.Count);
            yield return buffer[j];

            buffer[j] = buffer[i];
        }
    }
}

static class Extension
{
    public static void LeftShift<T>(this T[] arr, int shift)
    {
        shift %= arr.Length;
        var buffer = new T[shift];
        Array.Copy(arr, buffer, shift);
        Array.Copy(arr, shift, arr, 0, arr.Length - shift);
        Array.Copy(buffer, 0, arr, arr.Length - shift, shift);
    }
    
    public static void LeftShift<T>(this List<T> lst, int shift)
    {
        for (var i = 0; i < shift; i++)
        {
            var items = lst[lst.Count() - 1];
            lst.RemoveAt(lst.Count - 1);
            lst.Insert(0, items);
        }
    }
}
