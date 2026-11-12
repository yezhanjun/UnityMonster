using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class CatalogValidation
    {
        public static void Run()
        {
            var catalog = SkillCatalog.Load();
            var byId = new Dictionary<string, SkillCatalog.Node>();
            foreach (var node in catalog.skills)
                byId.Add(node.id, node);

            var occupied = new HashSet<(string, int, int)>();
            foreach (var node in catalog.skills)
            {
                if (node.gridX < 1 || node.gridY < 1 || !occupied.Add((node.group, node.gridX, node.gridY)))
                    throw new InvalidOperationException($"Invalid grid position: {node.id}");
                RequireTexture(node.icon);
            }

            foreach (var owner in catalog.owners)
                RequireTexture(owner.portrait);

            var completed = new HashSet<string>();
            var visiting = new HashSet<string>();
            foreach (var node in catalog.skills)
                Visit(node, byId, visiting, completed);

            foreach (var node in catalog.skills)
            {
                var growthIds = new HashSet<int>();
                foreach (var level in node.growth)
                {
                    if (!growthIds.Add(level.csvId) || level.effects.Length == 0)
                        throw new InvalidOperationException($"Invalid growth entry {node.id}: {level.csvId}");
                }
            }

            RequireEffect(byId["active_seer_E"], 4403, "Missiles 9 >> 13");
            RequireEffect(byId["active_seer_E_evolve"], 4404, "Secondary Damage 12");
            RequireEffect(byId["active_thor_D"], 2032, "Interval 0.85 >> 0.7 s");
            RequireEffect(byId["active_thor_D_evolve"], 2034, "Damage 35");

            var progress = new SkillProgress(catalog, null, 6);
            const string baseSkill = "active_seer_E";
            const string evolution = "active_seer_E_evolve";
            if (progress.TryLearn(evolution) || !progress.TryLearn(baseSkill) || progress.TryLearn(baseSkill) ||
                !progress.TryLearn(evolution) || progress.Points != 0)
                throw new InvalidOperationException("Unlock, prerequisite, or point accounting failed.");

            progress.Reset();
            if (progress.Points != 6 || progress.IsLearned(baseSkill) || progress.IsLearned(evolution))
                throw new InvalidOperationException("Reset did not refund and clear the purchased nodes.");

            ValidatePointPurchase(catalog, baseSkill);
            Debug.Log($"JOTUNNSLAYER_CATALOG_OK {catalog.skills.Length} skills, {catalog.awards.Length} trophies");
        }

        private static void ValidatePointPurchase(SkillCatalog catalog, string baseSkill)
        {
            string key = "jotunnslayer.validation." + Guid.NewGuid().ToString("N");
            try
            {
                PlayerPrefs.SetString(key, "{\"points\":6,\"learned\":[]}");
                var progress = new SkillProgress(catalog, key, 10, 25);
                if (progress.MaxBuyablePoints() != 8 || progress.QuotePoints(7) != 21 ||
                    progress.QuotePoints(8) != 25 || progress.TryBuyPoints(9) ||
                    progress.Points != 6 || progress.Gold != 25 || !progress.TryBuyPoints(7))
                    throw new InvalidOperationException("Batch price, affordable maximum, or atomic purchase check failed.");

                progress = new SkillProgress(catalog, key, 10, 25);
                if (progress.Points != 13 || progress.Gold != 4 || progress.NextPointPrice != 4 ||
                    !progress.TryBuyPoints(1) || progress.TryBuyPoints(1))
                    throw new InvalidOperationException("Point price, save reload, or insufficient gold check failed.");

                if (!progress.TryLearn(baseSkill))
                    throw new InvalidOperationException("Could not spend the purchased skill points.");

                progress.Reset();
                if (progress.Points != 14 || progress.Gold != 0 || progress.NextPointPrice != 4)
                    throw new InvalidOperationException("Skill reset changed purchase history or gold.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void RequireTexture(string key)
        {
            if (string.IsNullOrEmpty(key) || !Resources.Load<Texture2D>("Jotunnslayer/Icons/" + key))
                throw new InvalidOperationException($"Missing skill tree texture: {key}");
        }

        private static void RequireEffect(SkillCatalog.Node node, int csvId, string effect)
        {
            var level = Array.Find(node.growth, entry => entry.csvId == csvId);
            if (level == null || Array.IndexOf(level.effects, effect) < 0)
                throw new InvalidOperationException($"Missing evidence {node.id}: {csvId} / {effect}");
        }

        private static void Visit(SkillCatalog.Node node, Dictionary<string, SkillCatalog.Node> byId,
            HashSet<string> visiting, HashSet<string> completed)
        {
            if (completed.Contains(node.id))
                return;

            if (node.cost < 0 || node.souls < 0 || !visiting.Add(node.id))
                throw new InvalidOperationException($"Invalid skill tree node {node.id}");

            foreach (string prerequisite in node.conditions)
                Visit(byId[prerequisite], byId, visiting, completed);

            visiting.Remove(node.id);
            completed.Add(node.id);
        }
    }
}


