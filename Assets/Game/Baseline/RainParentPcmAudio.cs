using System;
using System.IO;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public static class RainParentPcmAudio
    {
        // Caller owns the resulting clip and must Destroy it. No static audio/resource cache.
        // Preserve float overshoots produced by the original Vorbis decode, avoiding PCM16 import wrapping.
        public static AudioClip Create(TextAsset source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            byte[] bytes = source.bytes;
            if (bytes.Length < 16 || BitConverter.ToInt32(bytes, 0) != 0x31435052)
                throw new InvalidDataException("Invalid Parent float PCM header.");
            int channels = BitConverter.ToInt32(bytes, 4);
            int frequency = BitConverter.ToInt32(bytes, 8);
            int frames = BitConverter.ToInt32(bytes, 12);
            if (channels < 1 || channels > 8 || frequency < 8000 || frequency > 192000 || frames <= 0
                || (long)frames * channels * 4 != bytes.Length - 16)
                throw new InvalidDataException("Invalid Parent float PCM dimensions.");
            var samples = new float[frames * channels];
            Buffer.BlockCopy(bytes, 16, samples, 0, bytes.Length - 16);
            for (int i = 0; i < samples.Length; i++)
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    throw new InvalidDataException("Non-finite Parent PCM sample.");
            var clip = AudioClip.Create(source.name, frames, channels, frequency, false);
            if (clip.SetData(samples, 0)) return clip;
            UnityEngine.Object.Destroy(clip);
            throw new InvalidDataException("Parent float PCM upload failed.");
        }
    }
}
