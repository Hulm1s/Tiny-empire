using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Cuts the recorded sound effects down to the short, instantly repeatable clips the game
    /// plays. A transfer fires every 0.18 s, so a clip much longer than that just piles up on
    /// itself. The source MP3s stay untouched in Audio/Source; the trimmed WAVs go to
    /// Resources/Sfx, which is where SoundFx loads them from at runtime.
    /// </summary>
    public static class SoundTrim
    {
        private const string SourceFolder = "Assets/_Project/Audio/Source/";
        private const string OutputFolder = "Assets/_Project/Resources/Sfx/";

        private struct Cut
        {
            public string Source;
            public string Output;
            public float MaxSeconds;    // kept after the onset
            public float FadeSeconds;   // fade-out at the end of what is kept
            public float Peak;          // normalised peak, so the two sit at one loudness
        }

        private static readonly Cut[] Cuts =
        {
            new Cut { Source = "pickup_source.mp3", Output = "pickup", MaxSeconds = 0.12f, FadeSeconds = 0.03f, Peak = 0.7f },
            new Cut { Source = "drop_source.mp3",   Output = "drop",   MaxSeconds = 0.12f, FadeSeconds = 0.03f, Peak = 0.7f },
        };

        /// <summary>Prints each source's length and loudness envelope, to choose the cut.</summary>
        [MenuItem("Tycoon/Sounds/Analyze Sources")]
        public static void Analyze()
        {
            foreach (var cut in Cuts)
            {
                var samples = Load(cut.Source, out int channels, out int rate);
                if (samples == null) continue;
                int frames = samples.Length / channels;
                var sb = new StringBuilder();
                sb.Append($"[SoundTrim] {cut.Source}: {frames / (float)rate:0.000}s, {channels}ch, {rate}Hz, " +
                          $"peak {Peak(samples):0.000}, onset {Onset(samples, channels, rate):0.000}s\n  rms/20ms:");
                int window = rate / 50;
                for (int f = 0; f < frames; f += window)
                {
                    double sum = 0; int n = 0;
                    for (int i = f; i < Mathf.Min(frames, f + window); i++, n++)
                    {
                        float m = Mono(samples, channels, i);
                        sum += m * m;
                    }
                    sb.Append($" {Mathf.Sqrt((float)(sum / Mathf.Max(1, n))):0.00}");
                }
                Debug.Log(sb.ToString());
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Tycoon/Sounds/Trim To Game Clips")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (var cut in Cuts)
            {
                var samples = Load(cut.Source, out int channels, out int rate);
                if (samples == null) continue;

                int start = Mathf.RoundToInt(Onset(samples, channels, rate) * rate);
                start = Mathf.Max(0, start - rate / 500);                // 2 ms of lead-in
                int frames = samples.Length / channels;
                int length = Mathf.Min(frames - start, Mathf.RoundToInt(cut.MaxSeconds * rate));
                int fade = Mathf.Min(length, Mathf.RoundToInt(cut.FadeSeconds * rate));
                int fadeIn = Mathf.Min(length, rate / 1000);             // 1 ms, no click at the cut

                var mono = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float gain = 1f;
                    if (i < fadeIn) gain *= i / (float)fadeIn;
                    if (i >= length - fade) gain *= (length - i) / (float)fade;
                    mono[i] = Mono(samples, channels, start + i) * gain;
                }

                float peak = Peak(mono);
                if (peak > 0f) for (int i = 0; i < mono.Length; i++) mono[i] *= cut.Peak / peak;

                string path = OutputFolder + cut.Output + ".wav";
                WriteWav(path, mono, rate);
                Debug.Log($"[SoundTrim] {path}: {length / (float)rate:0.000}s from {start / (float)rate:0.000}s");

                // The rising run: one clip per step, each a little higher, played in turn while
                // goods keep moving. Rendered here because WebGL cannot resample at runtime.
                for (int step = 0; step < GameLadder.Length; step++)
                    WriteWav($"{OutputFolder}{cut.Output}_{step}.wav", Pitched(mono, GameLadder[step]), rate);
            }
            AssetDatabase.Refresh();
            Debug.Log("TRIM_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static readonly string PreviewFolder =
            Path.Combine(Path.GetTempPath(), "claude", "D--iosGame", "b50262dc-8611-42da-8650-85247e0c766c", "scratchpad", "ripple");

        /// <summary>Semitone ladders auditioned for the "each one a bit higher" run.</summary>
        private static readonly int[] LadderA = { 0, 2, 4, 7, 9, 12, 12, 12 };   // pentatonic to an octave
        private static readonly int[] LadderB = { 0, 1, 2, 3, 4, 5, 6, 7 };      // gentle, up a fifth

        /// <summary>The ladder the game plays, chosen by ear from the previews: ladder B.</summary>
        public static readonly int[] GameLadder = LadderB;

        /// <summary>
        /// Writes what a run of eight transfers would sound like with each ladder, at the game's
        /// real 0.18 s transfer rate, so the effect can be judged before it goes in.
        /// </summary>
        [MenuItem("Tycoon/Sounds/Preview Rising Runs")]
        public static void PreviewRipple()
        {
            Directory.CreateDirectory(PreviewFolder);
            foreach (var name in new[] { "pickup", "drop" })
            {
                var clip = ReadWav(OutputFolder + name + ".wav", out int rate);
                if (clip == null) continue;
                WriteWav(Path.Combine(PreviewFolder, $"{name}_A_pentatonika.wav"), Run(clip, rate, LadderA, 0.18f), rate);
                WriteWav(Path.Combine(PreviewFolder, $"{name}_B_jemne.wav"), Run(clip, rate, LadderB, 0.18f), rate);
                WriteWav(Path.Combine(PreviewFolder, $"{name}_0_bez_zmeny.wav"), Run(clip, rate, new int[8], 0.18f), rate);
            }
            Debug.Log("[SoundTrim] previews -> " + PreviewFolder);
            Debug.Log("PREVIEW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static float[] Run(float[] clip, int rate, int[] ladder, float spacing)
        {
            int step = Mathf.RoundToInt(spacing * rate);
            var mix = new float[step * ladder.Length + clip.Length + rate / 4];
            for (int n = 0; n < ladder.Length; n++)
            {
                var v = Pitched(clip, ladder[n]);
                for (int i = 0; i < v.Length; i++) mix[n * step + i] += v[i];
            }
            return mix;
        }

        /// <summary>Raises a clip by some semitones by resampling (shorter and higher, like a faster tape).</summary>
        public static float[] Pitched(float[] clip, int semitones)
        {
            float ratio = Mathf.Pow(2f, semitones / 12f);
            int length = Mathf.Max(1, Mathf.FloorToInt((clip.Length - 1) / ratio));
            var outp = new float[length];
            for (int i = 0; i < length; i++)
            {
                float src = i * ratio;
                int a = Mathf.FloorToInt(src);
                int b = Mathf.Min(a + 1, clip.Length - 1);
                outp[i] = Mathf.Lerp(clip[a], clip[b], src - a);
            }
            return outp;
        }

        private static float[] ReadWav(string path, out int rate)
        {
            rate = 44100;
            if (!File.Exists(path)) { Debug.LogError($"[SoundTrim] missing {path} - run Trim first"); return null; }
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                r.ReadBytes(24);
                rate = r.ReadInt32();
                r.ReadBytes(12);   // byte rate, block align, bits, "data"
                int bytes = r.ReadInt32();
                var data = new float[bytes / 2];
                for (int i = 0; i < data.Length; i++) data[i] = r.ReadInt16() / 32768f;
                return data;
            }
        }

        private static float[] Load(string file, out int channels, out int rate)
        {
            channels = 1; rate = 44100;
            string path = SourceFolder + file;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                // GetData needs the whole clip decoded in memory.
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || importer.loadInBackground)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.loadInBackground = false;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { Debug.LogError($"[SoundTrim] missing {path}"); return null; }
            clip.LoadAudioData();
            channels = clip.channels;
            rate = clip.frequency;
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }

        private static float Mono(float[] s, int channels, int frame)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += s[frame * channels + c];
            return sum / channels;
        }

        private static float Peak(float[] s)
        {
            float p = 0f;
            foreach (var v in s) p = Mathf.Max(p, Mathf.Abs(v));
            return p;
        }

        /// <summary>Seconds until the sound first reaches a tenth of its peak.</summary>
        private static float Onset(float[] s, int channels, int rate)
        {
            float threshold = Peak(s) * 0.1f;
            int frames = s.Length / channels;
            for (int i = 0; i < frames; i++)
                if (Mathf.Abs(Mono(s, channels, i)) >= threshold) return i / (float)rate;
            return 0f;
        }

        private static void WriteWav(string path, float[] mono, int rate)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = mono.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var v in mono) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
            }
        }
    }
}
