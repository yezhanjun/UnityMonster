using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    [Serializable]
    public sealed class SkillCatalog
    {
        [Serializable]
        public sealed class Node
        {
            public string id, group, title, desc, icon;
            public int cost, souls, gridX, gridY;
            public string[] conditions;
            public Variant[] variants;
            public Growth[] growth;
        }

        [Serializable]
        public sealed class Variant
        {
            public string prefab, icon;
            public int csvId;
            public float cooldown;
        }

        [Serializable]
        public sealed class Growth
        {
            public int csvId;
            public string title;
            public string[] effects;
        }

        [Serializable]
        public sealed class Trophy
        {
            public string id, title, desc, condition, reward;
            public int count;
        }

        [Serializable]
        public sealed class Item
        {
            public string id, title, category, asset, prototype;
            public string description, skill, skillDescription, owner;
            public string[] effects;
            public bool equipment;
            public int characterMask;
        }

        [Serializable]
        public sealed class Owner
        {
            public string group, portrait, title, description;
            public Ability[] abilities;
        }

        [Serializable]
        public sealed class Ability
        {
            public string title, description;
        }

        public Owner[] owners;
        public Item[] items;
        public Node[] skills;
        public Trophy[] awards;

        public static SkillCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Jotunnslayer/Catalog");
            if (!asset)
                throw new InvalidOperationException("Missing Jotunnslayer skill catalog.");

            return JsonUtility.FromJson<SkillCatalog>(asset.text);
        }
    }

    public sealed class SkillProgress
    {
        [Serializable]
        private sealed class Save
        {
            public int points;
            public int gold;
            public int purchasedPoints;
            public bool goldInitialized;
            public List<string> learned = new List<string>();
        }

        private readonly Dictionary<string, SkillCatalog.Node> nodes;
        private readonly Save save;
        private readonly string saveKey;

        public int Points => save.points;
        public int Gold => save.gold;
        // Only learned-node mutations invalidate combat caches; gold pickups do not.
        internal uint LearnedRevision { get; private set; }
        private const int MaxPointPrice = 50;

        public int NextPointPrice => PointPrice(save.purchasedPoints);

        internal bool TryCollectGold(int amount = 1)
        {
            if (amount <= 0 || Gold == int.MaxValue)
                return false;

            save.gold = (int)Math.Min(int.MaxValue, (long)Gold + amount);
            Persist();
            return true;
        }

        public SkillProgress(SkillCatalog catalog, string saveKey, int startingPoints, int startingGold = 0)
        {
            nodes = new Dictionary<string, SkillCatalog.Node>(catalog.skills.Length, StringComparer.Ordinal);
            foreach (var node in catalog.skills)
                nodes.Add(node.id, node);

            this.saveKey = saveKey;
            save = string.IsNullOrEmpty(saveKey) ? null : JsonUtility.FromJson<Save>(PlayerPrefs.GetString(saveKey));
            save ??= new Save { points = startingPoints };
            if (!save.goldInitialized)
            {
                save.gold = startingGold;
                save.goldInitialized = true;
            }
        }

        private static int PointPrice(int purchased) =>
            Mathf.Min(MaxPointPrice, Mathf.FloorToInt(3 + purchased * 0.15f));

        public long QuotePoints(int count)
        {
            long total = 0;
            for (int i = 0; i < count; i++)
            {
                int price = PointPrice(save.purchasedPoints + i);
                if (price == MaxPointPrice)
                    return total + (long)(count - i) * MaxPointPrice;

                total += price;
            }

            return total;
        }

        public int MaxBuyablePoints()
        {
            int low = 0;
            int high = Math.Min(Gold / 3, Math.Min(int.MaxValue - Points, int.MaxValue - save.purchasedPoints));
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (QuotePoints(middle) <= Gold)
                    low = middle;
                else
                    high = middle - 1;
            }

            return low;
        }

        public bool TryBuyPoints(int count)
        {
            if (count <= 0 || count > int.MaxValue - Points || count > int.MaxValue - save.purchasedPoints)
                return false;

            long price = QuotePoints(count);
            if (Gold < price)
                return false;

            save.gold -= (int)price;
            save.points += count;
            save.purchasedPoints += count;
            Persist();
            return true;
        }

        public bool IsLearned(string id) => save.learned.Contains(id);

        internal string[] ExportLearnedNodes() => save.learned.ToArray();

        // Local progression is imported as a loadout, never as the host's save or currency.
        internal static bool TryImportLoadout(SkillCatalog catalog, string[] learned, out SkillProgress profile)
        {
            profile = null;
            if (learned == null || learned.Length > catalog.skills.Length) return false;
            int points = 0;
            foreach (string id in learned)
            {
                SkillCatalog.Node found = null;
                foreach (var node in catalog.skills)
                    if (node.id == id) { found = node; break; }
                if (found == null || found.cost < 0 || found.souls != 0) return false;
                points += found.cost;
            }
            var candidate = new SkillProgress(catalog, null, points);
            foreach (string id in learned)
                if (!candidate.TryLearn(id)) return false;
            profile = candidate;
            return true;
        }

        public bool CanLearn(string id)
        {
            if (!nodes.TryGetValue(id, out var node) || IsLearned(id) || node.cost > Points || node.souls > 0)
                return false;

            foreach (string prerequisite in node.conditions)
            {
                if (!IsLearned(prerequisite))
                    return false;
            }

            return true;
        }

        public bool TryLearn(string id)
        {
            if (!CanLearn(id))
                return false;

            save.points -= nodes[id].cost;
            save.learned.Add(id);
            unchecked { LearnedRevision++; }
            Persist();
            return true;
        }

        public void Reset()
        {
            foreach (string id in save.learned)
                save.points += nodes[id].cost;

            save.learned.Clear();
            unchecked { LearnedRevision++; }
            Persist();
        }

        private void Persist()
        {
            if (string.IsNullOrEmpty(saveKey))
                return;

            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }
    }
}




