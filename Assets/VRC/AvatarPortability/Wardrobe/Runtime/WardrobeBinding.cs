using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    public static class WardrobeBinding
    {
        public static bool TryBind(WardrobeItem item, GameObject clothing, Transform avatar,
            out string report)
        {
            var errors = new List<string>();
            Transform root = clothing.transform;
            Transform sourceRig = Resolve(root, item.sourceSkeletonPath);
            Transform targetRig = Resolve(avatar, item.targetSkeletonPath);
            if (!sourceRig || !targetRig)
            {
                report = "Source or target skeleton path does not exist.";
                return false;
            }
            if (item.bindSkinDirectlyToAvatar && !ValidateDirectSkinSetup(item, clothing, out report)) return false;
            var preserved = new HashSet<Transform>();
            if (item.preservedSourcePaths != null)
                foreach (string path in item.preservedSourcePaths)
                {
                    Transform bone = Resolve(root, path);
                    if (!bone) errors.Add("Preserved path missing: " + path);
                    else foreach (Transform child in bone.GetComponentsInChildren<Transform>(true)) preserved.Add(child);
                }
            MonoBehaviour[] behaviours = clothing.GetComponentsInChildren<MonoBehaviour>(true);
            Transform[] candidates = sourceRig.GetComponentsInChildren<Transform>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                var owner = behaviour as AvatarPortability.IBoneSimulationOwnership;
                if (owner == null) continue;
                foreach (Transform bone in candidates)
                    if (owner.OwnsBone(bone))
                        foreach (Transform child in bone.GetComponentsInChildren<Transform>(true)) preserved.Add(child);
            }
            var mapping = new Dictionary<Transform, Transform>();
            var explicitSources = new HashSet<Transform>();
            if (item.overrides != null)
                foreach (WardrobeItem.BoneOverride entry in item.overrides)
                {
                    Transform source = Resolve(root, entry.sourcePath);
                    Transform target = Resolve(avatar, entry.targetPath);
                    if (!source || !target || !IsUnder(source, sourceRig)) errors.Add("Invalid override: " + entry.sourcePath + " -> " + entry.targetPath);
                    else if (!explicitSources.Add(source)) errors.Add("Duplicate override: " + entry.sourcePath);
                    else if (preserved.Contains(source)) errors.Add("Override conflicts with preserved bone: " + entry.sourcePath);
                    else mapping[source] = target;
                }
            Animator sourceAnimator = clothing.GetComponentInChildren<Animator>(true);
            Animator targetAnimator = avatar.GetComponentInChildren<Animator>(true);
            if (item.useHumanoidMapping && sourceAnimator && targetAnimator && sourceAnimator.isHuman && targetAnimator.isHuman)
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    Transform source = sourceAnimator.GetBoneTransform((HumanBodyBones)i);
                    Transform target = targetAnimator.GetBoneTransform((HumanBodyBones)i);
                    if (source && target && IsUnder(source, sourceRig) && IsUnder(target, targetRig) && !preserved.Contains(source) && !mapping.ContainsKey(source)) mapping.Add(source, target);
                }
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (item.allowUniqueNameFallback)
                foreach (Transform target in targetRig.GetComponentsInChildren<Transform>(true))
                {
                    if (byName.ContainsKey(target.name)) byName[target.name] = null;
                    else byName.Add(target.name, target);
                }
            var sourceNames = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Transform source in candidates)
            {
                if (!sourceNames.ContainsKey(source.name)) sourceNames[source.name] = 0;
                sourceNames[source.name]++;
            }
            foreach (Transform source in candidates)
            {
                if (preserved.Contains(source) || mapping.ContainsKey(source)) continue;
                string relativePath = RelativePath(source, sourceRig);
                if (Resolve(sourceRig, relativePath) != source)
                { errors.Add("Ambiguous source bone path: " + relativePath); continue; }
                Transform target = Resolve(targetRig, relativePath);
                if (!target && item.allowUniqueNameFallback && sourceNames[source.name] == 1) byName.TryGetValue(source.name, out target);
                if (target) mapping.Add(source, target);
            }
            if (item.requiredSourcePaths != null)
                foreach (string path in item.requiredSourcePaths)
                {
                    Transform source = Resolve(root, path);
                    if (!source || !mapping.ContainsKey(source)) errors.Add("Required bone not mapped: " + path);
                }
            if (mapping.Count == 0) errors.Add("No bones mapped.");
            if (errors.Count != 0)
            {
                report = string.Join("\n", errors);
                return false;
            }
            if (item.bindSkinDirectlyToAvatar)
                return TryBindDirectSkin(clothing, avatar, targetRig, mapping, out report);
            // Preserve the instantiated hierarchy and every serialized internal reference.
            // Only the pose driver is added; skinning and physics keep their original bones.
            var sources = new List<Transform>(mapping.Count);
            var targets = new List<Transform>(mapping.Count);
            foreach (Transform source in candidates)
                if (mapping.TryGetValue(source, out Transform target)) { sources.Add(source); targets.Add(target); }
            foreach (Animator animator in clothing.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            WardrobePoseFollower follower = clothing.GetComponent<WardrobePoseFollower>();
            if (!follower) follower = clothing.AddComponent<WardrobePoseFollower>();
            Quaternion[] authored = null;
            if (item.restRotationOverrides != null && item.restRotationOverrides.Length > 0)
            {
                if (!item.preserveRestRotationOffsets) { report = "Authored rotations require preserveRestRotationOffsets"; return false; }
                authored = new Quaternion[sources.Count];
                for (int i = 0; i < sources.Count; i++)
                {
                    string path = RelativePath(sources[i], root), targetPath = RelativePath(targets[i], avatar);
                    int found = -1;
                    for (int j = 0; j < item.restRotationOverrides.Length; j++) if (item.restRotationOverrides[j].sourcePath == path)
                    { if (found >= 0) { report = "Duplicate authored rotation: " + path; return false; } found = j; }
                    if (found < 0 || item.restRotationOverrides[found].targetPath != targetPath) { report = "Missing/mismatched authored rotation: " + path; return false; }
                    Quaternion q = item.restRotationOverrides[found].rotation;
                    float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
                    if (float.IsNaN(norm) || float.IsInfinity(norm) || Mathf.Abs(norm-1f)>.001f) { report = "Invalid authored quaternion: " + path; return false; }
                    authored[i] = q;
                }
            }
            follower.Initialize(sources.ToArray(), targets.ToArray(), item.preserveRestRotationOffsets, authored);
            report = "Mapped " + mapping.Count + " bones; preserved " + preserved.Count + ". Clothing hierarchy retained.";
            return true;
        }

        private static bool ValidateDirectSkinSetup(WardrobeItem item, GameObject clothing, out string report)
        {
            if (item.preservedSourcePaths != null && item.preservedSourcePaths.Length > 0)
            { report = "Direct skin binding does not permit preserved clothing bone branches."; return false; }
            if (clothing.GetComponentInChildren<WardrobePoseFollower>(true))
            { report = "Direct skin binding does not permit an existing clothing pose follower."; return false; }
            if (clothing.GetComponentInChildren<Cloth>(true) || clothing.GetComponentInChildren<Rigidbody>(true) || clothing.GetComponentInChildren<Joint>(true))
            { report = "Direct skin binding requires passive clothing without independent Cloth/Rigidbody/Joint physics."; return false; }
            foreach (MonoBehaviour behaviour in clothing.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour) { report = "Direct skin clothing contains a missing script."; return false; }
                string type = behaviour.GetType().Name;
                if (behaviour is AvatarPortability.IBoneSimulationOwnership || type == "DynamicBone" || type == "VRCPhysBone")
                { report = "Direct skin binding does not permit independent clothing bone physics: " + type; return false; }
            }
            report = string.Empty;
            return true;
        }

        private static bool TryBindDirectSkin(GameObject clothing, Transform avatar, Transform targetRig,
            Dictionary<Transform, Transform> mapping, out string report)
        {
            SkinnedMeshRenderer[] skins = clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (avatar == clothing.transform || avatar.IsChildOf(clothing.transform) || targetRig.IsChildOf(clothing.transform))
            { report = "Direct skin target must belong to a separate avatar skeleton."; return false; }
            if (skins.Length == 0) { report = "Direct skin clothing has no SkinnedMeshRenderer."; return false; }
            var mappedBones = new Transform[skins.Length][];
            var mappedRoots = new Transform[skins.Length];
            // Validate every renderer before assigning any bone array. Mesh and bindposes are immutable here.
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (!skin.sharedMesh) { report = "Direct skin mesh missing: " + skin.name; return false; }
                Matrix4x4 relative = avatar.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                for (int cell = 0; cell < 16; cell++)
                {
                    float value = relative[cell];
                    if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value - Matrix4x4.identity[cell]) > .0001f)
                    { report = "Direct skin renderer must share avatar root coordinates: " + skin.name; return false; }
                }
                Transform[] original = skin.bones;
                Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
                if (original == null || original.Length == 0 || original.Length != bindposes.Length)
                { report = "Direct skin bone/bindpose counts do not match: " + skin.name; return false; }
                mappedBones[i] = new Transform[original.Length];
                for (int bone = 0; bone < original.Length; bone++)
                {
                    if (!original[bone] || !mapping.TryGetValue(original[bone], out Transform target) || !target || !IsUnder(target, targetRig))
                    { report = "Direct skin bone not mapped to avatar: " + skin.name + " [" + bone + "]"; return false; }
                    for (int cell = 0; cell < 16; cell++)
                        if (float.IsNaN(bindposes[bone][cell]) || float.IsInfinity(bindposes[bone][cell]))
                        { report = "Direct skin bindpose contains a nonfinite value: " + skin.name; return false; }
                    mappedBones[i][bone] = target;
                }
                if (!skin.rootBone || !mapping.TryGetValue(skin.rootBone, out Transform root) || !root || !IsUnder(root, targetRig))
                { report = "Direct skin rootBone not mapped to avatar: " + skin.name; return false; }
                mappedRoots[i] = root;
            }
            for (int i = 0; i < skins.Length; i++)
            { skins[i].bones = mappedBones[i]; skins[i].rootBone = mappedRoots[i]; }
            foreach (Animator animator in clothing.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            report = "Direct skin binding succeeded: " + skins.Length + " renderers use avatar bones; mesh/bindposes and clothing hierarchy retained; no follower added.";
            return true;
        }

        public static Transform Resolve(Transform root, string path)
        {
            if (!root) return null;
            if (string.IsNullOrEmpty(path)) return root;
            // Transform.Find silently chooses the first duplicate sibling. Reject ambiguity.
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform match = null;
                for (int i = 0; i < current.childCount; i++)
                    if (current.GetChild(i).name == segment)
                    {
                        if (match) return null;
                        match = current.GetChild(i);
                    }
                if (!match) return null;
                current = match;
            }
            return current;
        }

        private static bool IsUnder(Transform bone, Transform root) { return bone == root || bone.IsChildOf(root); }

        private static string RelativePath(Transform bone, Transform root)
        {
            if (bone == root) return string.Empty;
            string path = bone.name;
            while (bone.parent != root) { bone = bone.parent; path = bone.name + "/" + path; }
            return path;
        }
    }
}
