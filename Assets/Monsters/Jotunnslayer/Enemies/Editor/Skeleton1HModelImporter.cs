using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class Skeleton1HModelImporter : AssetPostprocessor
    {
        const string ModelPath = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/Skeleton1H/skeleton_1h_sur.fbx";

        const string BowModelPath = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/SkeletonBowRegular/skeleton_bow_VarRegular.fbx";

        const string SphinxFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/SphinxSkeleton/";
        const string MilipedeFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/Milipede/";
        const string HorrorJotunFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/HorrorJotun/";
        const string HorrorAlfFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/HorrorAlf/";
        const string BatLootFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/BatLoot/";
        const string HybSubBossPath = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/HybSubBossEasy/hyb_sub_boss_sur_VarEasy.fbx";
        const string HybBossFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/HybBossEasy/";
        const string DriderFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimDrider/";
        const string StoneJotunnFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimStoneJotunn/";
        const string ElfRangedFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimElfRanged/";
        const string ElfMeleeFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimElfMelee/";
        const string SentinelFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimSentinels/";
        const string AlfheimSpiderFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimSpiders/";
        const string AlfheimFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/AlfheimBoss/";
        const string GhostFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/Ghost/";
        const string GhostSkullFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/GhostSkull/";
        const string BossFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/NiflheimBossEasy/";
        const string GuardianFolder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/SkeletonGuardian/";

        // Bone names from the original skeleton_1h_sur Avatar.
        static readonly (string human, string bone)[] BoneMap =
        {
            ("Hips", "hips"),
            ("Spine", "spine"),
            ("Chest", "chest"),
            ("UpperChest", "upper_chest"),
            ("Neck", "neck"),
            ("Head", "head"),
            ("LeftShoulder", "left_shoulder"),
            ("RightShoulder", "right_shoulder"),
            ("LeftUpperArm", "left_upper_arm"),
            ("RightUpperArm", "right_upper_arm"),
            ("LeftLowerArm", "left_lower_arm"),
            ("RightLowerArm", "right_lower_arm"),
            ("LeftHand", "left_hand"),
            ("RightHand", "right_hand"),
            ("LeftUpperLeg", "left_upper_leg"),
            ("RightUpperLeg", "right_upper_leg"),
            ("LeftLowerLeg", "left_lower_leg"),
            ("RightLowerLeg", "right_lower_leg"),
            ("LeftFoot", "left_foot"),
            ("RightFoot", "right_foot"),
            ("LeftToes", "left_toes"),
            ("RightToes", "right_toes"),
            ("Left Thumb Proximal", "left_thumb_proximal"),
            ("Left Index Proximal", "left_index_proximal"),
            ("Left Index Intermediate", "left_index_intermediate"),
            ("Right Index Proximal", "right_index_proximal"),
            ("Right Thumb Proximal", "right_thumb_proximal")
        };

        void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/Skeleton1H/skeleton_part1_nm.png" &&
                assetPath != "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/SpiderRegular/spiderling_nm.png" &&
                assetPath != GuardianFolder + "skeleton_part2_nm.png" &&
                assetPath != BossFolder + "skeleton_goat_normal.png" &&
                assetPath != GhostFolder + "ghost_nm.png" &&
                assetPath != GhostSkullFolder + "ghost_skull_nm.png" &&
                assetPath != HorrorJotunFolder + "horror_jotunheim_nm.png" &&
                assetPath != HorrorAlfFolder + "horror_expl_alf_nm.png" &&
                assetPath != BatLootFolder + "bat_loot_nm.png" &&
                assetPath != DriderFolder + "elf_drider_nm.png" &&
                assetPath != DriderFolder + "elf_drider_insane_nm.png" &&
                assetPath != StoneJotunnFolder + "jotun_sentinel_nm.png" &&
                assetPath != StoneJotunnFolder + "jotunn_stone_insane_nm.png" &&
                assetPath != ElfRangedFolder + "elven_wraith_ranged_nm.png" &&
                assetPath != ElfMeleeFolder + "elven_wraith_nm.png" &&
                assetPath != ElfMeleeFolder + "elven_wraith_insane_nm.png" &&
                assetPath != SentinelFolder + "gargoyle_nm.png" &&
                assetPath != AlfheimSpiderFolder + "spider_alf_nm.png" &&
                assetPath != AlfheimSpiderFolder + "spider_alf_veteran_nm.png" &&
                assetPath != AlfheimFolder + "jotunn_spider_nm.png" &&
                assetPath != AlfheimFolder + "jotunn_spider_bag_nm.png" && assetPath != SphinxFolder + "sphinx_nm.png" &&
                assetPath != HybBossFolder + "sphinx_boss_nm.png" &&
                assetPath != SphinxFolder + "sphinx_miniboss_nm.png")
                return;

            ((TextureImporter)assetImporter).textureType = TextureImporterType.NormalMap;
        }

        void OnPreprocessModel()
        {
            if (assetPath == "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/SpiderRegular/spider_sur_VarRegular.fbx" ||
                assetPath == BossFolder + "skeleton_goat_sur_boss_VarEasy.fbx" ||
                assetPath == GhostFolder + "ghost_sur.fbx" ||
                assetPath == GhostSkullFolder + "ghost_skull_sur.fbx" ||
                assetPath == DriderFolder + "elf_spider_sur.fbx" ||
                assetPath == DriderFolder + "elf_spider_02_sur.fbx" ||
                assetPath == StoneJotunnFolder + "jotunn_stone_sur.fbx" ||
                assetPath == StoneJotunnFolder + "jotunn_stone_insane_sur.fbx" ||
                assetPath == ElfRangedFolder + "elf_ranged_sur.fbx" ||
                assetPath == ElfMeleeFolder + "elf_melee_sur.fbx" ||
                assetPath == ElfMeleeFolder + "elf_melee_insane_sur.fbx" ||
                assetPath == SentinelFolder + "sentinel_alf_sur.fbx" ||
                assetPath == SentinelFolder + "sentinel_veteran_alf_sur.fbx" ||
                assetPath == AlfheimSpiderFolder + "spider_alf_sur.fbx" ||
                assetPath == AlfheimSpiderFolder + "spider_alf_veteran_sur.fbx" ||
                assetPath == AlfheimFolder + "jotunn_spider_cs_mid.fbx" ||
                assetPath == AlfheimFolder + "jotunn_alfheim_boss_sur.fbx" ||
                assetPath == AlfheimFolder + "jotunn_alfheim_boss_phase2_sur.fbx" ||
                assetPath == MilipedeFolder + "milipede_sur.fbx" ||
                assetPath == HorrorJotunFolder + "horror_expl_jotun_sur.fbx" ||
                assetPath == HorrorAlfFolder + "horror_expl_alf_sur.fbx" ||
                assetPath == BatLootFolder + "bat_loot_sur.fbx")
            {
                var generic = (ModelImporter)assetImporter;
                generic.animationType = ModelImporterAnimationType.Generic;
                generic.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }

            if (assetPath != ModelPath && assetPath != BowModelPath &&
                assetPath != GuardianFolder + "skeleton_guardian_sur.fbx" && assetPath != HybSubBossPath &&
                assetPath != HybBossFolder + "hyb_boss_sur_VarEasy.fbx" &&
                assetPath != SphinxFolder + "skeleton_1h_sphinx_sur.fbx" &&
                assetPath != SphinxFolder + "skeleton_bow_sphinx.fbx" &&
                assetPath != SphinxFolder + "skeleton_guardian_sphinx_sur.fbx" &&
                assetPath != SphinxFolder + "skeleton_mb_sphinx_sur.fbx") return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            var description = importer.humanDescription;
            description.human = new HumanBone[BoneMap.Length];
            for (int index = 0; index < BoneMap.Length; index++)
            {
                var (human, bone) = BoneMap[index];
                description.human[index] = new HumanBone
                {
                    humanName = human,
                    boneName = bone,
                    limit = new HumanLimit { useDefaultValues = true }
                };
            }

            importer.humanDescription = description;
        }
    }
}
