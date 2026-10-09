using System;
using System.IO;
using System.Text;
using Tycoon.Audio;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Writes every generated sound effect to a WAV file and reports its levels.
    ///
    /// The sounds are synthesised in code (<see cref="SoundFx"/>), so the only way to inspect
    /// one without playing the game is to render it out. This writes each sound at each of its
    /// pitch variants, then prints peak, DC offset, RMS, length and the first and last samples
    /// (which must be zero, or the sound clicks). Headless, with an output folder:
    ///
    ///   Unity.exe -quit -batchmode -nographics -projectPath game
    ///     -executeMethod Tycoon.EditorTools.SoundExport.Run -soundOut C:/some/folder
    ///
    /// With no <c>-soundOut</c> it writes to <c>../sound-clips</c> beside the project.
    /// </summary>
    public static class SoundExport
    {
        [MenuItem("Tycoon/Export Sound Clips")]
        public static void Export() => Write(DefaultFolder());

        public static void Run()
        {
            string folder = DefaultFolder();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-soundOut") folder = args[i + 1];

            Write(folder);
            Debug.Log("SOUNDS_OK");
        }

        private static string DefaultFolder() => Path.GetFullPath(Path.Combine(Application.dataPath, "../../sound-clips"));

        private static void Write(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("[Sounds] name  length  peak  DC  RMS  first  last\n");

            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                int variants = SoundFx.VariantCount(sfx);
                for (int v = 0; v < variants; v++)
                {
                    float[] samples = SoundFx.Synthesize(sfx, SoundFx.PitchFor(sfx, v));
                    string name = $"{sfx}_{v}";
                    WriteWav(Path.Combine(folder, name + ".wav"), samples, SoundFx.SampleRate);

                    double sum = 0d, squares = 0d;
                    float peak = 0f;
                    foreach (float s in samples)
                    {
                        sum += s;
                        squares += s * s;
                        peak = Mathf.Max(peak, Mathf.Abs(s));
                    }

                    report.AppendLine(
                        $"[Sounds] {name,-10} {samples.Length * 1000f / SoundFx.SampleRate,6:0}ms " +
                        $"peak {peak:0.000}  dc {sum / samples.Length:0.00000}  " +
                        $"rms {Math.Sqrt(squares / samples.Length):0.000}  " +
                        $"first {samples[0]:0.0000}  last {samples[samples.Length - 1]:0.0000}" +
                        (peak >= 0.99f ? "  CLIPPING" : ""));
                }
            }

            Debug.Log(report.ToString());
            Debug.Log($"[Sounds] wrote to {folder}");
        }

        /// <summary>16-bit mono PCM. Hand-written so there is nothing to import or reference.</summary>
        private static void WriteWav(string path, float[] samples, int rate)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(rate);
                w.Write(rate * 2);       // byte rate
                w.Write((short)2);       // block align
                w.Write((short)16);      // bits
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }
    }
}
