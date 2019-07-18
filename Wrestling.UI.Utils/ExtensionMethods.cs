using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Wrestling.UI.Utils
{
    public static class ExtensionMethods
    {
        public static void Shuffle<T>(this IList<T> list, Random rnd)
        {
            for (var i = 0; i < list.Count; i++)
                list.Swap(i, rnd.Next(i, list.Count));
        }

        public static void Swap<T>(this IList<T> list, int i, int j)
        {
            Contract.Requires(list != null);
            Contract.Requires(i >= 0 && i < list.Count);
            Contract.Requires(j >= 0 && j < list.Count);

            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}