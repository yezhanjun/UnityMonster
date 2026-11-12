using System;
using UnityEngine;
using AvatarPortability.Expressions;

namespace AvatarPortability.ExpressionClips
{
    /// <summary>Samples exported facial-only clips without changing the body's Animator.</summary>
    [ExecuteAlways, DefaultExecutionOrder(20000), DisallowMultipleComponent]
    public sealed class WendyExpressionClipPlayer : MonoBehaviour
    {
        public WendyExpressionClipLibrary library;
        [Min(0)] public float transitionDuration = .25f;
        private sealed class Session
        {
            public WendyExpressionClipLibrary library;
            public SkinnedMeshRenderer renderer;
            public Mesh mesh;
            public int[] indices;
            public float[] baseline;
            public string rendererPath;
            public string[] managedNames, pathSegments;
            public GameObject samplingRoot;
            public SkinnedMeshRenderer samplingFace;
        }
        private Session session;
        private ExpressionClipEntry current;
        private int index = -1;
        private bool playing, paused;
        private double elapsed;
        private float sampleTime;
        private bool transitioning, fadingToBaseline;
        private float transitionElapsed, activeTransitionDuration;
        private float[] transitionSource;
        private string status = "尚未选择表情动画。";

        public bool IsPlaying => playing || (transitioning && !paused);
        public bool IsTransitioning => transitioning;
        public int CurrentIndex => index;
        public float NormalizedTime => current != null && current.clip && current.clip.length > 0 ? Mathf.Clamp01(sampleTime / current.clip.length) : 0;
        public bool HasBaseline => session != null && session.renderer;
        public string StatusMessage => status;

        public bool Play(int requestedIndex, out string message)
        {
            if (!Prepare(requestedIndex, out ExpressionClipEntry entry, out SkinnedMeshRenderer face, out int[] mapped, out message)) return false;
            // The older static preview must release its captured face before we capture our own baseline.
            var staticPreview = GetComponent<AvatarPortability.Expressions.WendyExpressionPlayer>();
            if (staticPreview) staticPreview.Restore(out _);
            bool same = session != null && session.library == library && session.renderer == face && session.mesh == face.sharedMesh && LayoutUnchanged(session) && SameIndices(session.indices, mapped);
            if (!same && session != null) WriteBaseline(session);
            Session candidate = same ? session : new Session { library = library, renderer = face, mesh = face.sharedMesh, indices = mapped, baseline = Read(face, mapped), rendererPath = library.rendererPath, managedNames = (string[])library.managedBlendshapes.Clone(), pathSegments = string.IsNullOrEmpty(library.rendererPath) ? Array.Empty<string>() : library.rendererPath.Split('/') };
            float[] previous = Read(face, mapped);
            try { EnsureSampler(candidate); Sample(candidate, entry.clip, 0, false); }
            catch (Exception exception)
            {
                Write(face, mapped, previous);
                if (!same) ReleaseSampler(candidate);
                if (!same && current != null && current.clip && Compatible(session)) Sample(session, current.clip, sampleTime);
                return Fail("表情动画采样失败：" + exception.Message, out message);
            }
            if (!same) ReleaseSampler(session);
            session = candidate;
            ClearTransition();
            current = new ExpressionClipEntry { clip = entry.clip, originalName = entry.originalName, category = entry.category, sourceClipPath = entry.sourceClipPath, utility = entry.utility, timeVarying = entry.timeVarying, loop = entry.loop };
            index = requestedIndex; elapsed = 0; sampleTime = 0; paused = false;
            playing = entry.timeVarying && entry.clip.length > 0;
            return Succeed((playing ? "正在播放：" : "保持静态表情：") + WendyExpressionChinese.Name(entry.originalName), out message);
        }

        public bool CrossFade(int requestedIndex, float duration, out string message)
        {
            if (!ValidDuration(duration)) return Fail("过渡时长必须是大于或等于零的有限数值。", out message);
            if (duration == 0) return Play(requestedIndex, out message);
            if (!Prepare(requestedIndex, out _, out SkinnedMeshRenderer face, out int[] mapped, out message)) return false;
            float[] displayed = Read(face, mapped);
            if (!Play(requestedIndex, out message)) return false;
            transitionSource = displayed;
            transitionElapsed = 0; activeTransitionDuration = duration;
            transitioning = true; fadingToBaseline = false;
            Write(session.renderer, session.indices, displayed);
            return Succeed("正在平滑切换：" + WendyExpressionChinese.Name(current.originalName), out message);
        }
        public bool CrossFadeNeutral(float duration, out string message)
        { return CrossFade(library ? library.neutralIndex : -1, duration, out message); }
        public bool CrossFade(int requestedIndex, out string message) { return CrossFade(requestedIndex, transitionDuration, out message); }
        public bool CrossFadeNeutral(out string message) { return CrossFadeNeutral(transitionDuration, out message); }
        public bool FadeToBaseline(out string message) { return FadeToBaseline(transitionDuration, out message); }
        public bool FadeToBaseline(float duration, out string message)
        {
            if (!ValidDuration(duration)) return Fail("过渡时长必须是大于或等于零的有限数值。", out message);
            if (duration == 0) return RestoreBaseline(out message);
            if (!Compatible(session)) return Fail("没有可恢复的有效面部状态记录。", out message);
            if (!PlaybackLayoutValid()) { RestoreBaseline(out _); return Fail("面部绑定配置已改变，已立即恢复原始状态。", out message); }
            transitionSource = Read(session.renderer, session.indices);
            transitionElapsed = 0; activeTransitionDuration = duration;
            transitioning = true; fadingToBaseline = true; playing = false; paused = false;
            return Succeed("正在平滑恢复播放前的面部状态。", out message);
        }

        public bool Seek(float normalized, out string message)
        {
            if (float.IsNaN(normalized) || float.IsInfinity(normalized) || normalized < 0 || normalized > 1) return Fail("播放进度必须是 0 到 1 之间的有限数值。", out message);
            if (current == null || !current.clip || !Compatible(session)) return Fail("当前没有可调整进度的有效表情动画。", out message);
            if (!PlaybackLayoutValid()) { RestoreBaseline(out _); return Fail("面部绑定配置已改变，已恢复播放前的面部状态。", out message); }
            float next = normalized * current.clip.length;
            float[] previous = Read(session.renderer, session.indices);
            try { Sample(session, current.clip, next, false); }
            catch (Exception exception) { Write(session.renderer, session.indices, previous); return Fail("调整表情进度时采样失败：" + exception.Message, out message); }
            elapsed = next; sampleTime = next;
            ClearTransition();
            if (!current.loop && next >= current.clip.length) playing = false;
            return Succeed("已调整：" + WendyExpressionChinese.Name(current.originalName) + "，进度 " + normalized.ToString("P0"), out message);
        }

        public void Pause()
        {
            if (current == null) { status = "当前没有可暂停的表情动画。"; return; }
            playing = false; paused = true; status = "已暂停：" + WendyExpressionChinese.Name(current.originalName);
        }
        public void Resume()
        {
            if (current == null || !current.clip || !Compatible(session)) { status = "当前没有可继续播放的有效表情动画。"; return; }
            if (!PlaybackLayoutValid()) { RestoreBaseline(out _); status = "面部绑定配置已改变，已恢复播放前的面部状态。"; return; }
            if (transitioning) { paused = false; playing = !fadingToBaseline && current.timeVarying && current.clip.length > 0 && (current.loop || sampleTime < current.clip.length); status = "继续平滑过渡。"; return; }
            if (!current.timeVarying || current.clip.length <= 0) { status = "保持静态表情：" + WendyExpressionChinese.Name(current.originalName); return; }
            if (!current.loop && sampleTime >= current.clip.length) { status = "表情动画已结束；请重新播放，或先调整进度再继续。"; return; }
            paused = false; playing = true; status = "正在播放：" + WendyExpressionChinese.Name(current.originalName);
        }
        public bool PlayNeutral(out string message)
        { return Play(library ? library.neutralIndex : -1, out message); }

        public bool RestoreBaseline(out string message)
        {
            bool restored = WriteBaseline(session);
            ReleaseSampler(session);
            ClearTransition();
            session = null; current = null; index = -1; playing = false; paused = false; elapsed = 0; sampleTime = 0;
            return restored ? Succeed("已恢复播放前的面部状态。", out message) : Fail("没有可恢复的有效面部状态记录。", out message);
        }

        /// <summary>Deterministic unscaled-time stepping, also available to an Editor preview driver.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if ((!playing && !transitioning) || paused || current == null) return;
            if (!Compatible(session) || !current.clip || !PlaybackLayoutValid())
            { RestoreBaseline(out _); status = "面部目标已改变或丢失，表情播放已停止并释放。"; return; }
            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0)
            { status = "已忽略无效的非缩放时间增量。"; return; }
            if (unscaledDeltaTime == 0) return;
            if (transitioning) transitionElapsed = Mathf.Min(transitionElapsed + unscaledDeltaTime, activeTransitionDuration);
            if (fadingToBaseline)
            {
                float blend = TransitionBlend();
                for (int i = 0; i < session.indices.Length; i++) session.renderer.SetBlendShapeWeight(session.indices[i], Mathf.LerpUnclamped(transitionSource[i], session.baseline[i], blend));
                if (transitionElapsed >= activeTransitionDuration) RestoreBaseline(out _);
                return;
            }
            double duration = current.clip.length;
            double nextElapsed = elapsed + (playing ? unscaledDeltaTime : 0);
            float next = sampleTime;
            bool finished = false;
            if (playing && duration > 0 && current.loop)
            {
                if (current.clip.wrapMode == WrapMode.PingPong)
                { double phase = nextElapsed % (duration * 2); next = (float)(phase <= duration ? phase : duration * 2 - phase); }
                else next = (float)(nextElapsed % duration);
            }
            else if (playing && duration > 0) { next = (float)Math.Min(nextElapsed, duration); finished = nextElapsed >= duration; }
            try { Sample(session, current.clip, next); }
            catch (Exception exception) { playing = false; ClearTransition(); status = "表情采样已停止：" + exception.Message; return; }
            elapsed = current.loop && duration > 0 ? nextElapsed % (duration * 2) : next; sampleTime = next;
            if (transitioning && transitionElapsed >= activeTransitionDuration)
            {
                ClearTransition();
                status = (playing ? "正在播放：" : "保持表情：") + WendyExpressionChinese.Name(current.originalName);
            }
            if (finished) { playing = false; status = "播放完成：" + WendyExpressionChinese.Name(current.originalName) + "，保持最后一帧。"; }
        }

        private bool Prepare(int requestedIndex, out ExpressionClipEntry entry, out SkinnedMeshRenderer face, out int[] mapped, out string message)
        {
            entry = null; face = null; mapped = null;
            if (!library || library.entries == null || requestedIndex < 0 || requestedIndex >= library.entries.Length || library.entries[requestedIndex] == null || !library.entries[requestedIndex].clip)
                return Fail("请选择有效的已导出表情动画。", out message);
            entry = library.entries[requestedIndex];
            if (float.IsNaN(entry.clip.length) || float.IsInfinity(entry.clip.length) || entry.clip.length < 0) return Fail("表情动画时长无效。", out message);
            if (entry.clip.events.Length != 0) return Fail("不支持包含动画事件的表情动画。", out message);
            Transform node = string.IsNullOrEmpty(library.rendererPath) ? transform : Resolve(transform, library.rendererPath);
            face = node ? node.GetComponent<SkinnedMeshRenderer>() : null;
            if (!face || !face.sharedMesh) return Fail("无法按配置路径找到面部蒙皮渲染器及其网格。", out message);
            if (library.managedBlendshapes == null || library.managedBlendshapes.Length == 0) return Fail("受管面部形态列表为空。", out message);
            mapped = new int[library.managedBlendshapes.Length];
            for (int i = 0; i < mapped.Length; i++)
            {
                mapped[i] = face.sharedMesh.GetBlendShapeIndex(library.managedBlendshapes[i]);
                if (mapped[i] < 0) return Fail("面部网格缺少形态：" + library.managedBlendshapes[i], out message);
            }
            message = string.Empty; return true;
        }

        private void LateUpdate() { if (Application.isPlaying && (playing || transitioning)) Tick(Time.unscaledDeltaTime); }
        private void OnDisable() { RestoreBaseline(out _); }
        private void OnDestroy() { RestoreBaseline(out _); }
        private bool Succeed(string value, out string message) { status = message = value; return true; }
        private bool Fail(string value, out string message) { status = message = value; return false; }
        private static bool Compatible(Session value) { return value != null && value.renderer && value.renderer.sharedMesh == value.mesh; }
        private bool PlaybackLayoutValid()
        { return session != null && session.library == library && LayoutUnchanged(session) && Resolve(transform, session.pathSegments) == session.renderer.transform; }
        private static bool LayoutUnchanged(Session value)
        {
            if (value == null || !value.library || value.library.rendererPath != value.rendererPath || value.library.managedBlendshapes == null || value.library.managedBlendshapes.Length != value.managedNames.Length) return false;
            for (int i = 0; i < value.managedNames.Length; i++) if (value.library.managedBlendshapes[i] != value.managedNames[i]) return false;
            return true;
        }
        private static bool SameIndices(int[] left, int[] right)
        { if (left.Length != right.Length) return false; for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false; return true; }
        private static float[] Read(SkinnedMeshRenderer face, int[] mapped)
        { var result = new float[mapped.Length]; for (int i = 0; i < mapped.Length; i++) result[i] = face.GetBlendShapeWeight(mapped[i]); return result; }
        private static void Write(SkinnedMeshRenderer face, int[] mapped, float[] values)
        { if (!face) return; for (int i = 0; i < mapped.Length; i++) face.SetBlendShapeWeight(mapped[i], values[i]); }
        private static bool WriteBaseline(Session value)
        { if (!Compatible(value)) return false; Write(value.renderer, value.indices, value.baseline); return true; }
        // Sampling directly on a Humanoid actor can cause Unity to write its Avatar's
        // default body pose even when the clip contains only renderer curves.
        // This renderer-only hierarchy has no Animator, Avatar, bones or behaviours.
        private static void EnsureSampler(Session value)
        {
            if (value.samplingRoot && value.samplingFace) return;
            value.samplingRoot = new GameObject("Wendy Facial Clip Sampler") { hideFlags = HideFlags.HideAndDontSave };
            Transform node = value.samplingRoot.transform;
            foreach (string segment in value.pathSegments)
            {
                var child = new GameObject(segment) { hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(node, false); node = child.transform;
            }
            value.samplingFace = node.gameObject.AddComponent<SkinnedMeshRenderer>();
            value.samplingFace.enabled = false;
            value.samplingFace.sharedMesh = value.mesh;
        }
        private void Sample(Session value, AnimationClip clip, float time, bool blendTransition = true)
        {
            EnsureSampler(value);
            clip.SampleAnimation(value.samplingRoot, time);
            float blend = blendTransition && transitioning ? TransitionBlend() : 1;
            for (int i = 0; i < value.indices.Length; i++)
            {
                float target = value.samplingFace.GetBlendShapeWeight(value.indices[i]);
                value.renderer.SetBlendShapeWeight(value.indices[i], blendTransition && transitioning ? Mathf.LerpUnclamped(transitionSource[i], target, blend) : target);
            }
        }
        private static bool ValidDuration(float duration) { return !float.IsNaN(duration) && !float.IsInfinity(duration) && duration >= 0; }
        private float TransitionBlend() { float t = Mathf.Clamp01(transitionElapsed / activeTransitionDuration); return t * t * (3 - 2 * t); }
        private void ClearTransition() { transitioning = false; fadingToBaseline = false; transitionElapsed = 0; activeTransitionDuration = 0; transitionSource = null; }
        private static void ReleaseSampler(Session value)
        {
            if (value == null) return;
            if (value.samplingRoot)
            {
                if (Application.isPlaying) Destroy(value.samplingRoot);
                else DestroyImmediate(value.samplingRoot);
            }
            value.samplingRoot = null; value.samplingFace = null;
        }
        private static Transform Resolve(Transform root, string path)
        { return Resolve(root, path.Split('/')); }
        private static Transform Resolve(Transform root, string[] segments)
        {
            Transform current = root;
            foreach (string name in segments)
            {
                Transform match = null;
                for (int i = 0; i < current.childCount; i++) if (current.GetChild(i).name == name) { if (match) return null; match = current.GetChild(i); }
                if (!match) return null; current = match;
            }
            return current;
        }
    }
}
