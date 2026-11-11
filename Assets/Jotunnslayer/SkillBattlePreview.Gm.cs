using UnityEngine;
using MTPSKIT.Gameplay;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        // Reuse the live skill implementations; GM grants only a temporary test loadout.
        internal static readonly string[] GmGodSkillIds =
        {
            "active_thor_D",
            "active_thor_C_evolve",
            "active_thor_A_evolve",
            "active_thor_B_evolve",
            "active_thor_E_evolve",
            "active_freya_C_evolve",
            "active_freya_A_evolve",
            "active_freya_E_evolve",
            "active_freya_D_evolve",
            "active_freya_B_evolve",
            "active_loki_E_evolve",
            "active_loki_B_evolve",
            "active_loki_A_evolve",
            "active_loki_C_evolve",
            "active_loki_D_evolve",
            "active_skadi_A_evolve",
            "active_skadi_B_evolve",
            "active_skadi_D_evolve",
            "active_skadi_C_evolve",
            "active_skadi_E_evolve",
            "active_odin_A_evolve",
            "active_odin_B_evolve",
            "active_odin_C_evolve",
            "active_odin_D_evolve",
            "active_odin_E_evolve",
            "active_njord_C_evolve",
            "active_njord_D_evolve",
            "active_njord_B_evolve",
            "active_njord_A_evolve",
            "active_njord_E_evolve",
            "active_nidhogg_D_evolve",
            "active_nidhogg_E_evolve",
            "active_nidhogg_B_evolve",
            "active_nidhogg_A_evolve",
            "active_nidhogg_C_evolve",
        };

        internal bool StartGmGodSkill(Health owner, SkillProgress testProfile, string id, bool evolved)
        {
            ShowControls = false;
            ConfigureRewardDisplay(SkillCatalog.Load(), null, false);
            Bind(owner, testProfile, null);
            gmIndicators = true;
            SetRaidLevel(70);
            int ranks = evolved ? 4 : 1;
            for (int rank = 0; rank < ranks; rank++)
            {
                bool chosen = id switch
                {
                    "active_thor_D" => TryChoose(false),
                    "active_thor_C_evolve" => TryChooseThunder(),
                    "active_thor_A_evolve" => TryChooseLightning(),
                    "active_thor_B_evolve" => TryChooseHammer(),
                    "active_thor_E_evolve" => TryChooseOrb(),
                    "active_freya_C_evolve" => TryChooseAura(),
                    "active_freya_A_evolve" => TryChooseMagicSword(),
                    "active_freya_E_evolve" => TryChooseBlessing(),
                    "active_freya_D_evolve" => TryChooseDiscs(),
                    "active_freya_B_evolve" => TryChooseValkyrie(),
                    "active_loki_E_evolve" => TryChoosePlague(),
                    "active_loki_B_evolve" => TryChooseFireBarrier(),
                    "active_loki_A_evolve" => TryChooseSerpent(),
                    "active_loki_C_evolve" => TryChooseFirestar(),
                    "active_loki_D_evolve" => TryChooseVipers(),
                    "active_skadi_A_evolve" => TryChooseComet(),
                    "active_skadi_B_evolve" => TryChooseFrostRing(),
                    "active_skadi_D_evolve" => TryChooseFrostArrow(),
                    "active_skadi_C_evolve" => TryChooseSnowflake(),
                    "active_skadi_E_evolve" => TryChooseIceField(),
                    "active_odin_A_evolve" => TryChooseGungnir(),
                    "active_odin_B_evolve" => TryChooseRavens(),
                    "active_odin_C_evolve" => TryChooseRagnarok(),
                    "active_odin_D_evolve" => TryChooseBoulder(),
                    "active_odin_E_evolve" => TryChooseWarrior(),
                    "active_njord_C_evolve" => TryChooseVortex(),
                    "active_njord_D_evolve" => TryChooseShip(),
                    "active_njord_B_evolve" => TryChooseKraken(),
                    "active_njord_A_evolve" => TryChooseLeviathan(),
                    "active_njord_E_evolve" => TryChooseTidalBarrier(),
                    "active_nidhogg_D_evolve" => TryChooseWingStrike(),
                    "active_nidhogg_E_evolve" => TryChooseCalamity(),
                    "active_nidhogg_B_evolve" => TryChooseDiseaseRay(),
                    "active_nidhogg_A_evolve" => TryChooseRaiseDead(),
                    "active_nidhogg_C_evolve" => TryChooseTerror(),
                    _ => false
                };
                if (!chosen) return false;
            }
            automatic = true;
            return true;
        }

        internal void StopGmGodSkill()
        {
            automatic = false;
            enabled = false;
            Bind(null, progress, null);
        }
    }
}
