using System.Collections.Generic;
using System.Linq;

namespace JellyField.Core
{
    public class ClearWave
    {
        public readonly Dictionary<int, HashSet<JellyColor>> Removed = new Dictionary<int, HashSet<JellyColor>>();
        public readonly int[] Counts = new int[6];
        public int Total => Counts.Sum();

        public void Add(int index, JellyColor color)
        {
            if (!Removed.TryGetValue(index, out var set))
                Removed[index] = set = new HashSet<JellyColor>();
            if (set.Add(color))
                Counts[(int)color]++;
        }
    }
}
