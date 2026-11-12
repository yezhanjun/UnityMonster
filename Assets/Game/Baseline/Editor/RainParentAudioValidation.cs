using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using MTPSKIT.Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Source-media readiness only. Wwise event gain/random/material/attenuation require separate integration.
    [InitializeOnLoad]
    public static class RainParentAudioValidation
    {
        const string Active = "RainParentAudio.Active";
        const string Deadline = "RainParentAudio.Deadline";
        const string Folder = "Assets/RainSource/ParentAudio/";
        const string Source = "Docs/玩法参考设计/雨中冒险2/原版依据/Parent脚步音频/CompleteEventMedia/Decoded/解码验证.json";
        static readonly List<string> playbackPaths = new List<string>();
        static AudioSource source;
        static AudioClip ownedClip;
        static RainParentAudioProbe probe;
        static GameObject owner;
        static int phase, index;
        static double startedDsp;
        static float startedReal, oldVolume;
        static bool oldPause, everAdvanced;
        static AudioListener[] oldListeners;

        static RainParentAudioValidation()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            try
            {
                ValidateImportedSources();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, Folder + "ParentAudioValidation.unity");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "ParentAudioValidation.unity");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 100);
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Fail(error); }
        }

        static void ValidateImportedSources()
        {
            var manifest = JObject.Parse(File.ReadAllText(Source));
            var checks = new JArray();
            long samples = 0;
            float worstError = 0;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var item in manifest["clips"])
            {
                string path = Folder + (string)item["file"];
                Require(Hash(path) == (string)item["sha256"], "Decoded WAV changed: " + path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Require(importer, "WAV AudioImporter missing: " + path);
                importer.forceToMono = false;
                var serializedImporter = new SerializedObject(importer);
                var normalize = serializedImporter.FindProperty("m_Normalize") ?? serializedImporter.FindProperty("normalize");
                Require(normalize != null, "Audio importer normalize field unavailable.");
                normalize.boolValue = false;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.loadInBackground = false;
                importer.ClearSampleSettingOverride("Standalone");
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.quality = 1;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                var clip = RainParentPcmAudio.Create(AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace(".wav", ".bytes")));
                Require(clip && clip.channels == (int)item["channels"] && clip.frequency == (int)item["sampleRate"]
                    && clip.samples == (int)item["frames"], "Unity changed source rate/channels/frame count: " + path);
                Require(clip.LoadAudioData() && clip.loadState == AudioDataLoadState.Loaded, "Decoded audio not synchronously loaded.");
                var actual = new float[clip.samples * clip.channels];
                Require(clip.GetData(actual, 0), "Unity PCM readback failed.");
                var expected = ReadWaveSamples(path);
                Require(actual.Length == expected.Length, "Unity PCM sample count differs.");
                float maxError = 0, peak = 0;
                double energy = 0;
                for (int i = 0; i < actual.Length; i++)
                {
                    Require(!float.IsNaN(actual[i]) && !float.IsInfinity(actual[i]), "Non-finite Unity PCM.");
                    maxError = Math.Max(maxError, Math.Abs(actual[i] - expected[i]));
                    peak = Math.Max(peak, Math.Abs(actual[i]));
                    energy += actual[i] * actual[i];
                }
                Require(maxError == 0 && peak > .00001f,
                    "Float PCM upload changed samples or became silent: path=" + path + " maxError=" + maxError
                    + " peak=" + peak + " sourcePeak=" + expected.Max(v => Math.Abs(v)));
                samples += actual.Length;
                worstError = Math.Max(worstError, maxError);
                checks.Add(new JObject { ["path"] = path, ["sha256"] = Hash(path), ["channels"] = clip.channels,
                    ["frequency"] = clip.frequency, ["frames"] = clip.samples, ["sampleCount"] = actual.Length,
                    ["maxAbsoluteError"] = maxError, ["peak"] = peak, ["rms"] = Math.Sqrt(energy / actual.Length) });
                UnityEngine.Object.DestroyImmediate(clip);
            }
            Require(checks.Count == 64, "Incomplete footstep audio import set.");
            File.WriteAllText("ParentAudio-UnityImport.json", new JObject {
                ["scope"] = "Original decoded media import only; no Wwise event playback fidelity",
                ["clips"] = checks, ["totalInterleavedSamples"] = samples, ["worstAbsoluteError"] = worstError
            }.ToString());
            Debug.Log("RAIN_PARENT_AUDIO_IMPORT_CHECKED clips=64 interleaved_samples=" + samples + " worst_error=" + worstError);
        }

        static float[] ReadWaveSamples(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            Require(new string(reader.ReadChars(4)) == "RIFF", "Not a RIFF source.");
            reader.ReadUInt32();
            Require(new string(reader.ReadChars(4)) == "WAVE", "Not a WAV source.");
            bool floatFormat = false;
            while (stream.Position + 8 <= stream.Length)
            {
                string chunk = new string(reader.ReadChars(4));
                uint size = reader.ReadUInt32();
                long end = stream.Position + size;
                if (chunk == "fmt ")
                {
                    ushort format = reader.ReadUInt16();
                    reader.ReadUInt16(); reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadUInt16();
                    ushort bits = reader.ReadUInt16();
                    if (format == 65534)
                    {
                        reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32(); format = reader.ReadUInt16();
                    }
                    floatFormat = format == 3 && bits == 32;
                }
                else if (chunk == "data")
                {
                    Require(floatFormat && size % 4 == 0, "Expected IEEE float32 source PCM.");
                    var samples = new float[size / 4];
                    for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadSingle();
                    return samples;
                }
                stream.Position = end + size % 2;
            }
            throw new InvalidDataException("Missing source PCM.");
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0)) throw new TimeoutException("Parent Audio validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase == 0)
                {
                    oldVolume = AudioListener.volume; oldPause = AudioListener.pause;
                    oldListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
                    foreach (var listener in oldListeners) listener.enabled = false;
                    AudioListener.volume = .05f; AudioListener.pause = false;
                    owner = new GameObject("Parent decoded source audio playback");
                    owner.AddComponent<AudioListener>();
                    source = owner.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
                    probe = owner.AddComponent<RainParentAudioProbe>();
                    var imported = JObject.Parse(File.ReadAllText("ParentAudio-UnityImport.json"))["clips"];
                    playbackPaths.Add((string)imported.First(c => (int)c["channels"] == 1)["path"]);
                    playbackPaths.Add(Folder + "170724810.wav");
                    playbackPaths.Add(Folder + "726553620.wav");
                    BeginPlayback();
                    phase = 1;
                }
                else if (phase == 1)
                {
                    everAdvanced |= source.timeSamples > 0;
                    if (Time.realtimeSinceStartup - startedReal > 5)
                        throw new TimeoutException("Real audio callback missing or playback stalled; no mock acceptance.");
                    if (AudioSettings.dspTime - startedDsp < source.clip.length + .1 || source.isPlaying) return;
                    Require(everAdvanced && probe.blocks > 0 && probe.peak > .000001f,
                        "AudioSource did not advance and produce non-silent real DSP callbacks.");
                    Debug.Log("RAIN_PARENT_AUDIO_SOURCE_PLAY_CHECKED path=" + playbackPaths[index] + " blocks=" + probe.blocks
                        + " output_channels=" + probe.channels + " peak=" + probe.peak + " dsp_delta=" + (AudioSettings.dspTime - startedDsp));
                    if (++index < playbackPaths.Count) BeginPlayback();
                    else
                    {
                        source.Stop();
                        UnityEngine.Object.Destroy(ownedClip);
                        UnityEngine.Object.Destroy(owner);
                        phase = 2;
                    }
                }
                else if (phase == 2)
                {
                    Require(!owner && !source && !probe && !ownedClip, "Audio owner, generated clip or callback component survived destruction.");
                    Debug.Log("RAIN_PARENT_AUDIO_PLAY_PASSED imported_clips=64 actual_source_formats=3 owner_cleanup=True wwise_event_fidelity_pending=True");
                    Finish(0);
                }
            }
            catch (Exception error) { Fail(error); }
        }

        static void BeginPlayback()
        {
            if (ownedClip) UnityEngine.Object.Destroy(ownedClip);
            ownedClip = RainParentPcmAudio.Create(AssetDatabase.LoadAssetAtPath<TextAsset>(playbackPaths[index].Replace(".wav", ".bytes")));
            source.clip = ownedClip;
            Require(source.clip, "Playback source missing.");
            probe.ResetMetrics(); everAdvanced = false;
            startedReal = Time.realtimeSinceStartup; startedDsp = AudioSettings.dspTime;
            source.Play();
        }

        static string Hash(string path)
        {
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Fail(Exception error) { Debug.LogException(error); Debug.LogError("RAIN_PARENT_AUDIO_FAILED"); Finish(1); }
        static void Finish(int code)
        {
            if (ownedClip) UnityEngine.Object.DestroyImmediate(ownedClip);
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            if (oldListeners != null)
            {
                foreach (var listener in oldListeners) if (listener) listener.enabled = true;
                AudioListener.volume = oldVolume; AudioListener.pause = oldPause;
            }
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }

    public sealed class RainParentAudioProbe : MonoBehaviour
    {
        public volatile int blocks, channels;
        public volatile float peak;
        public void ResetMetrics() { Interlocked.Exchange(ref blocks, 0); peak = 0; channels = 0; }
        void OnAudioFilterRead(float[] data, int outputChannels)
        {
            float maximum = 0;
            for (int i = 0; i < data.Length; i++) maximum = Math.Max(maximum, Math.Abs(data[i]));
            if (maximum > peak) peak = maximum;
            channels = outputChannels;
            Interlocked.Increment(ref blocks);
        }
    }
}
