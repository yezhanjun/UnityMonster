namespace MTPSKIT.Gameplay
{
    internal static class RainMonsterLogs
    {
        internal const string HermitCrabId = "Logs.HermitCrabBody.0";
        internal const string LunarExploderId = "Logs.LunarExploder.0";

        internal readonly struct Entry
        {
            internal readonly int Archetype;
            internal readonly string JournalId;
            internal readonly string UnlockableId;
            internal readonly float DropChance;

            internal Entry(int archetype, string journalId, string unlockableId, float dropChance = .01f)
            {
                Archetype = archetype;
                JournalId = journalId;
                UnlockableId = unlockableId;
                DropChance = dropChance;
            }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry(0, "BEETLE_BODY", "Logs.BeetleBody.0"),
            new Entry(1, "GOLEM_BODY", "Logs.GolemBody.0"),
            new Entry(2, "TITAN_BODY", "Logs.TitanBody.0", .03f),
            new Entry(RangeResearchEnemy.BeetleGuardKind, "BEETLEGUARD_BODY", "Logs.BeetleGuardBody.0"),
            new Entry(RangeResearchEnemy.LemurianKind, "LEMURIAN_BODY", "Logs.LemurianBody.0"),
            new Entry(RangeResearchEnemy.WispKind, "WISP_BODY", "Logs.WispBody.0"),
            new Entry(RangeResearchEnemy.JellyfishKind, "JELLYFISH_BODY", "Logs.JellyfishBody.0"),
            new Entry(RangeResearchEnemy.BruiserKind, "LEMURIANBRUISER_BODY", "Logs.LemurianBruiserBody.0"),
            new Entry(RangeResearchEnemy.QueenKind, "BEETLEQUEEN_BODY", "Logs.BeetleQueenBody.0", .03f),
            new Entry(RangeResearchEnemy.GreaterWispKind, "GREATERWISP_BODY", "Logs.GreaterWispBody.0"),
            new Entry(RangeResearchEnemy.ImpKind, "IMP_BODY", "Logs.ImpBody.0"),
            new Entry(RangeResearchEnemy.BisonKind, "BISON_BODY", "Logs.BisonBody.0"),
            new Entry(RangeResearchEnemy.VerminKind, "VERMIN_BODY", "Logs.VerminBody.0"),
            new Entry(RangeResearchEnemy.FlyingVerminKind, "FLYINGVERMIN_BODY", "Logs.FlyingVerminBody.0"),
            new Entry(RangeResearchEnemy.HermitCrabKind, "HERMIT_CRAB_BODY", HermitCrabId),
            new Entry(RangeResearchEnemy.AcidLarvaKind, "ACIDLARVA_BODY", "Logs.AcidLarva"),
            new Entry(RangeResearchEnemy.LunarExploderKind, "LUNAREXPLODER_BODY", LunarExploderId),
            new Entry(RangeResearchEnemy.ParentKind, "PARENT_BODY", "Logs.Parent.0")
        };

        internal static bool TryGet(int archetype, out Entry entry)
        {
            foreach (var candidate in Entries)
                if (candidate.Archetype == archetype)
                {
                    entry = candidate;
                    return true;
                }
            entry = default;
            return false;
        }

        internal static bool IsKnown(string unlockableId)
        {
            foreach (var entry in Entries)
                if (entry.UnlockableId == unlockableId) return true;
            return false;
        }

        internal static string ForJournal(string journalId)
        {
            foreach (var entry in Entries)
                if (entry.JournalId == journalId) return entry.UnlockableId;
            return null;
        }
    }
}
