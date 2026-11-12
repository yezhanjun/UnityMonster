using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    // Main-thread ownership. The controller is immutable after loading; Animator state is per actor.
    internal static class DraugControllerResources
    {
        [Serializable] sealed class Manifest { public string controllerAddress, controllerName; public ClipMap[] clips; }
        [Serializable] sealed class ClipMap { public string original, resource; }
        static AssetBundle bundle;
        static AnimatorOverrideController controller;
        static readonly Dictionary<Animator, Lease> owners = new Dictionary<Animator, Lease>();
        internal static int OwnerCount => owners.Count;
        internal static bool IsLoaded => bundle && controller;

        internal sealed class Lease : IDisposable
        {
            Animator animator;
            RuntimeAnimatorController previous;
            bool disposed;
            internal Lease(Animator target)
            {
                animator = target;
                previous = target.runtimeAnimatorController;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (animator && animator.runtimeAnimatorController == controller)
                    animator.runtimeAnimatorController = previous;
                owners.Remove(animator);
                animator = null;
                previous = null;
                if (owners.Count == 0) Unload();
            }
        }

        internal static Lease Acquire(Animator animator)
        {
            if (!animator) throw new ArgumentNullException(nameof(animator));
            if (owners.ContainsKey(animator)) throw new InvalidOperationException("Animator already owns a Draug controller lease");
            EnsureLoaded();
            var lease = new Lease(animator);
            try
            {
                animator.runtimeAnimatorController = controller;
                owners.Add(animator, lease);
                return lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        static void EnsureLoaded()
        {
            if (IsLoaded) return;
            if (owners.Count != 0) throw new InvalidOperationException("Draug controller invalidated while owned");
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new PlatformNotSupportedException("Draug compiled controller bundle targets Windows");
            try
            {
                var text = Resources.Load<TextAsset>("Jotunnslayer/DraugThrower/ControllerManifest");
                if (!text) throw new InvalidOperationException("Draug controller manifest missing");
                var manifest = JsonUtility.FromJson<Manifest>(text.text);
                bundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, "Jotunnslayer/Draug/source-controller.bundle"));
                if (!bundle) throw new InvalidOperationException("Draug controller bundle missing or already loaded outside its owner");
                var source = bundle.LoadAsset<RuntimeAnimatorController>(manifest.controllerAddress);
                if (!source || source.name != manifest.controllerName) throw new InvalidOperationException("Draug compiled controller missing");
                controller = new AnimatorOverrideController(source);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++)
                {
                    var pair = pairs[i];
                    ClipMap map = null;
                    for (int j = 0; j < manifest.clips.Length; j++)
                        if (manifest.clips[j].original == pair.Key.name) { map = manifest.clips[j]; break; }
                    if (map == null) throw new InvalidOperationException("Unmapped Draug controller clip: " + pair.Key.name);
                    if (string.IsNullOrEmpty(map.resource)) continue;
                    var clip = Resources.Load<AnimationClip>(map.resource);
                    if (!clip) throw new InvalidOperationException("Draug local clip missing: " + map.resource);
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, clip);
                }
                controller.ApplyOverrides(pairs);
            }
            catch { Unload(); throw; }
        }

        static void Unload()
        {
            if (controller)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(controller);
                else UnityEngine.Object.DestroyImmediate(controller);
            }
            controller = null;
            if (bundle) bundle.Unload(true);
            bundle = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode()
        {
            // Domain reload can be disabled; invalidate old leases so later disposal is harmless.
            var leases = new List<Lease>(owners.Values);
            foreach (var lease in leases) lease.Dispose();
            Unload();
        }
    }
}
