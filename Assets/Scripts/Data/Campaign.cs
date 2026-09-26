using System;
using System.Linq;
using JellyField.Core;
using UnityEngine;

namespace JellyField.Data
{
    [CreateAssetMenu(fileName = "Campaign", menuName = "Jelly Field/Campaign")]
    public class Campaign : ScriptableObject
    {
        public LevelAsset[] levels = new LevelAsset[0];
        public LevelDefinition[] Create()
        {
            if (levels == null || levels.Length == 0 || levels.Any(level => level == null))
                throw new InvalidOperationException("Campaign cần ít nhất một Level và không được có phần tử trống.");
            return levels.Select(level => level.ToDefinition()).ToArray();
        }
    }
}
