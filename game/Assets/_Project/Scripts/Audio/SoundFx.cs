using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.Audio
{
    /// <summary>Every sound effect in the game. Appended to, never renumbered.</summary>
    public enum Sfx
    {
        Pop,      // a unit picked up
        Drop,     // a unit put down into a hopper or onto a shelf
        Coin,     // a sale
        Tick,     // money going into a purchase
        Unlock,   // a purchase completing
        Trash,    // a unit into the bin
        Fix,      // a repair task finishing
        Whoosh,   // travelling between locations
        Happy,    // a customer leaves pleased
        Grumble,  // a customer leaves angry
        Click,    // a menu button
    }

    /// <summary>
    /// The game's sound effects, drawn in code the way <c>IconFactory</c> draws its icons: no
    /// audio files, no licences, nothing to import. There is deliberately no music.
    ///
    /// Each effect is a few sine partials under a soft envelope, built once on first use and
    /// kept (an <c>AudioClip.Create</c> + <c>SetData</c> clip that is not streamed, which is the
    /// route that is safe in a WebGL build). Every clip starts and ends at zero, has its mean
    /// removed and is normalised to a modest peak, so none of them can click, thump or clip.
    ///
    /// One <see cref="AudioSource"/> on the HUD object plays everything. Pitch is therefore
    /// baked into the clips rather than set on the source: <c>PlayOneShot</c> shares the
    /// source's pitch, so changing it would bend sounds that were already playing. The pitch
    /// variants are made lazily, so a sound costs nothing until it is first heard at that pitch.
    ///
    /// Repeats are what make this tricky - a stack of eight transfers fires every 0.18 s - so:
    /// <list type="bullet">
    /// <item>each sound has a minimum gap, and calls inside it are dropped;</item>
    /// <item>"ripple" sounds climb a pentatonic scale while they keep repeating and start again
    /// from the bottom after a pause, so a stack sounds like a run of notes, not a buzz;</item>
    /// <item>the others get a small random pitch so repeats are never an identical machine-gun.</item>
    /// </list>
    ///
    /// Sounds from the world (<see cref="PlayAt"/>) are heard only near the player. Stations are
    /// shared with hired workers, who keep working in another location or off screen, and the
    /// player should not hear a farm they are not standing in.
    ///
    /// Muting is not handled here. The pause panel's SOUND toggle sets
    /// <see cref="AudioListener.volume"/>, which silences everything; the play calls also skip
    /// the work when that is zero.
    ///
    /// To change how something sounds, edit its case in <c>Render</c>. To add a sound, append
    /// to <see cref="Sfx"/>, give it a row in <c>Spec</c> and a case in <c>Render</c>. The
    /// editor menu Tycoon > Export Sound Clips writes every clip to a WAV to inspect.
    /// </summary>
    public static class SoundFx
    {
        public const int SampleRate = 22050;

        /// <summary>Master level of the source. Clips are already normalised below 0.6.</summary>
        private const float MasterVolume = 0.8f;

        /// <summary>Beyond this (flat distance) a world sound is not played at all.</summary>
        private const float HearingRange = 20f;

        /// <summary>Inside this a world sound is at full volume.</summary>
        private const float FullVolumeRange = 9f;

        /// <summary>A pause longer than this ends a run of repeats.</summary>
        private const float RippleWindow = 0.5f;

        // Major pentatonic, in semitones: any run of these sounds consonant.
        private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14, 16 };

        private static readonly float[] Jitter = { 0.96f, 1f, 1.04f };

        private struct Settings
        {
            public float peak;      // normalised peak of the clip
            public float minGap;    // seconds between plays
            public int rippleSteps; // 0 = random jitter instead of a rising run
        }

        private static Settings Spec(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Pop:     return new Settings { peak = 0.42f, minGap = 0.06f, rippleSteps = 8 };
                case Sfx.Drop:    return new Settings { peak = 0.45f, minGap = 0.07f, rippleSteps = 8 };
                case Sfx.Coin:    return new Settings { peak = 0.38f, minGap = 0.07f, rippleSteps = 8 };
                case Sfx.Tick:    return new Settings { peak = 0.30f, minGap = 0.07f, rippleSteps = 5 };
                case Sfx.Unlock:  return new Settings { peak = 0.50f, minGap = 0.30f, rippleSteps = 0 };
                case Sfx.Trash:   return new Settings { peak = 0.38f, minGap = 0.07f, rippleSteps = 8 };
                case Sfx.Fix:     return new Settings { peak = 0.40f, minGap = 0.12f, rippleSteps = 0 };
                case Sfx.Whoosh:  return new Settings { peak = 0.34f, minGap = 0.50f, rippleSteps = 0 };
                case Sfx.Happy:   return new Settings { peak = 0.32f, minGap = 0.18f, rippleSteps = 0 };
                case Sfx.Grumble: return new Settings { peak = 0.30f, minGap = 0.30f, rippleSteps = 0 };
                default:          return new Settings { peak = 0.28f, minGap = 0.05f, rippleSteps = 0 };
            }
        }

        private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        private static readonly Dictionary<Sfx, float> LastPlayed = new Dictionary<Sfx, float>();
        private static readonly Dictionary<Sfx, int> RunLength = new Dictionary<Sfx, int>();
        private static readonly Dictionary<Sfx, int> LastJitter = new Dictionary<Sfx, int>();

        private static AudioSource _source;
        private static Transform _listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // "Enter play mode without domain reload" keeps statics alive; the clips and the
            // source belong to the previous session.
            Clips.Clear();
            LastPlayed.Clear();
            RunLength.Clear();
            LastJitter.Clear();
            _source = null;
            _listener = null;
        }

        // ================================================================ playing

        /// <summary>Plays a sound that is not in the world: a menu button.</summary>
        public static void Play(Sfx sfx) => PlayScaled(sfx, 1f);

        /// <summary>
        /// Plays a sound that happened somewhere in the world. Quieter with distance from the
        /// player and silent beyond <see cref="HearingRange"/>.
        /// </summary>
        public static void PlayAt(Sfx sfx, Vector3 position)
        {
            float volume = 1f;

            var listener = Listener();
            if (listener != null)
            {
                Vector3 d = position - listener.position;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance >= HearingRange) return;
                if (distance > FullVolumeRange)
                    volume = 1f - (distance - FullVolumeRange) / (HearingRange - FullVolumeRange);
            }

            PlayScaled(sfx, volume);
        }

        /// <summary>Master switch for every sound effect.</summary>
        public static bool Enabled = true;

        private static void PlayScaled(Sfx sfx, float volume)
        {
            if (!Enabled) return;
            if (volume <= 0.01f) return;
            if (AudioListener.volume <= 0f) return; // muted: do not even build the clip

            var spec = Spec(sfx);
            float now = Time.unscaledTime;

            bool played = LastPlayed.TryGetValue(sfx, out float last);
            if (played && now - last < spec.minGap) return;

            var source = Source();
            if (source == null) return;

            int variant;
            if (spec.rippleSteps > 0)
            {
                int run = 0;
                if (played && now - last < RippleWindow && RunLength.TryGetValue(sfx, out int previousRun))
                    run = Mathf.Min(previousRun + 1, spec.rippleSteps - 1);
                RunLength[sfx] = run;
                variant = run;
            }
            else
            {
                // A random pitch, but never the same one twice running.
                LastJitter.TryGetValue(sfx, out int previous);
                variant = Random.Range(0, Jitter.Length);
                if (variant == previous) variant = (variant + 1) % Jitter.Length;
                LastJitter[sfx] = variant;
            }

            LastPlayed[sfx] = now;

            var clip = GetClip(sfx, variant, spec);
            if (clip != null) source.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// The recorded clip a sound plays, from Resources/Sfx (trimmed there by the
        /// Tycoon/Sounds/Trim To Game Clips tool). Sounds without one stay silent: the
        /// synthesised versions were rejected by ear and are kept only for the export tool.
        /// </summary>
        private static string RecordedName(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Pop:   return "Sfx/pickup";   // anything into the player's arms
                case Sfx.Drop:                         // anything out of them: feeding,
                case Sfx.Coin:                         // selling,
                case Sfx.Trash: return "Sfx/drop";     // and the bin
                default:        return null;
            }
        }

        private static AudioClip GetClip(Sfx sfx, int variant, Settings spec)
        {
            // Each step of a rising run is its own pre-rendered clip (name_0 .. name_7): WebGL
            // cannot read clip data back, so pitch cannot be resampled at runtime.
            int key = (int)sfx * 100 + variant;
            if (Clips.TryGetValue(key, out var clip) && clip != null) return clip;

            string recorded = RecordedName(sfx);
            if (recorded == null) return null;
            clip = Resources.Load<AudioClip>($"{recorded}_{variant}");
            if (clip == null) clip = Resources.Load<AudioClip>(recorded);
            if (clip != null) Clips[key] = clip;
            return clip;
        }

        /// <summary>The pitch ratio of a variant: a step up the scale, or a little jitter.</summary>
        public static float PitchFor(Sfx sfx, int variant)
        {
            if (Spec(sfx).rippleSteps > 0)
                return Mathf.Pow(2f, Scale[Mathf.Clamp(variant, 0, Scale.Length - 1)] / 12f);
            return Jitter[Mathf.Clamp(variant, 0, Jitter.Length - 1)];
        }

        /// <summary>How many pitch variants a sound has (what the editor export walks through).</summary>
        public static int VariantCount(Sfx sfx)
        {
            int steps = Spec(sfx).rippleSteps;
            return steps > 0 ? steps : Jitter.Length;
        }

        private static AudioSource Source()
        {
            if (_source != null) return _source;

            // The HUD object lives for the whole session, so the source does too.
            GameObject host = Tycoon.UI.HudRoot.Instance != null ? Tycoon.UI.HudRoot.Instance.gameObject : null;
            if (host == null)
            {
                host = new GameObject("~SoundFx");
                Object.DontDestroyOnLoad(host);
            }

            _source = host.GetComponent<AudioSource>();
            if (_source == null) _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = MasterVolume;
            _source.pitch = 1f;
            return _source;
        }

        private static Transform Listener()
        {
            if (_listener == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _listener = player.transform;
            }
            return _listener;
        }

        // ================================================================ the sounds

        /// <summary>
        /// Builds the samples of one sound at a pitch ratio (1 = as written). Free of Unity audio
        /// objects so the editor can write it to a WAV file for inspection.
        /// </summary>
        public static float[] Synthesize(Sfx sfx, float pitch)
        {
            float[] samples = Render(sfx, pitch);
            Finish(samples, Spec(sfx).peak);
            return samples;
        }

        private static readonly (float ratio, float amp)[] Soft = { (1f, 1f), (2f, 0.15f) };
        private static readonly (float ratio, float amp)[] Round = { (1f, 1f), (2f, 0.25f), (3f, 0.06f) };
        private static readonly (float ratio, float amp)[] Bell = { (1f, 1f), (2f, 0.35f), (3.01f, 0.12f), (5.4f, 0.05f) };
        private static readonly (float ratio, float amp)[] Tink = { (1f, 1f), (2.76f, 0.30f), (5.4f, 0.08f) };
        private static readonly (float ratio, float amp)[] Buzz = { (1f, 1f), (2f, 0.30f), (3f, 0.15f) };

        private static float[] Render(Sfx sfx, float p)
        {
            switch (sfx)
            {
                case Sfx.Pop:
                {
                    // A bubble: a quick upward chirp that dies away.
                    var b = Buffer(0.12f);
                    Tone(b, 0f, 0.12f, 380f * p, 760f * p, 0.07f, 0.004f, 0.035f, Soft);
                    return b;
                }
                case Sfx.Drop:
                {
                    // A soft landing: a round thump falling in pitch.
                    var b = Buffer(0.18f);
                    Tone(b, 0f, 0.18f, 300f * p, 150f * p, 0.06f, 0.004f, 0.05f, Round);
                    return b;
                }
                case Sfx.Coin:
                {
                    // Two bright bell notes, a fourth apart.
                    var b = Buffer(0.4f);
                    Tone(b, 0f, 0.32f, 988f * p, 988f * p, 0.001f, 0.003f, 0.09f, Bell);
                    Tone(b, 0.07f, 0.32f, 1319f * p, 1319f * p, 0.001f, 0.003f, 0.11f, Bell);
                    return b;
                }
                case Sfx.Tick:
                {
                    // The smallest, driest sound: a little dot of a note.
                    var b = Buffer(0.07f);
                    Tone(b, 0f, 0.07f, 1000f * p, 880f * p, 0.04f, 0.002f, 0.016f, Soft);
                    return b;
                }
                case Sfx.Unlock:
                {
                    // A little four-note fanfare, C major, ending on the high C.
                    var b = Buffer(0.8f);
                    float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(b, i * 0.085f, i == 3 ? 0.46f : 0.26f, notes[i] * p, notes[i] * p,
                            0.001f, 0.004f, i == 3 ? 0.16f : 0.09f, Bell);
                    return b;
                }
                case Sfx.Trash:
                {
                    // A rustle and a soft thud, as a bin swallows something.
                    var b = Buffer(0.14f);
                    Noise(b, 0f, 0.13f, 2200f * p, 500f * p, 0.005f, 0.035f, 11);
                    Tone(b, 0f, 0.14f, 220f * p, 110f * p, 0.04f, 0.004f, 0.04f, Round, 0.8f);
                    return b;
                }
                case Sfx.Fix:
                {
                    // Tink, tink, ting: a tool on metal, with the last tap brighter.
                    var b = Buffer(0.5f);
                    Tone(b, 0f, 0.14f, 1568f * p, 1568f * p, 0.001f, 0.002f, 0.04f, Tink);
                    Tone(b, 0.1f, 0.14f, 1568f * p, 1568f * p, 0.001f, 0.002f, 0.04f, Tink);
                    Tone(b, 0.2f, 0.3f, 2093f * p, 2093f * p, 0.001f, 0.002f, 0.09f, Tink);
                    return b;
                }
                case Sfx.Whoosh:
                {
                    // Air passing: noise that opens up and closes again.
                    var b = Buffer(0.55f);
                    Whoosh(b, 0.55f, 250f * p, 2600f * p);
                    return b;
                }
                case Sfx.Happy:
                {
                    // Two warm notes going up.
                    var b = Buffer(0.4f);
                    Tone(b, 0f, 0.22f, 698.46f * p, 698.46f * p, 0.001f, 0.006f, 0.08f, Soft);
                    Tone(b, 0.1f, 0.3f, 880f * p, 880f * p, 0.001f, 0.006f, 0.12f, Soft);
                    return b;
                }
                case Sfx.Grumble:
                {
                    // A low, wobbly "bwomp bwomp", going down.
                    var b = Buffer(0.5f);
                    Tone(b, 0f, 0.26f, 260f * p, 200f * p, 0.001f, 0.02f, 0.14f, Buzz, 1f, 14f, 0.03f);
                    Tone(b, 0.15f, 0.3f, 233f * p, 150f * p, 0.001f, 0.02f, 0.16f, Buzz, 1f, 14f, 0.03f);
                    return b;
                }
                default:
                {
                    // Click: a short rounded tap.
                    var b = Buffer(0.06f);
                    Tone(b, 0f, 0.06f, 900f * p, 620f * p, 0.03f, 0.002f, 0.012f, Soft);
                    return b;
                }
            }
        }

        // ================================================================ building blocks

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        /// <summary>
        /// Adds one note. The pitch glides from <paramref name="f0"/> to <paramref name="f1"/>
        /// over <paramref name="glide"/> seconds, then holds. The envelope is a raised-cosine
        /// attack, an exponential decay and a short cosine release, so the note begins and ends
        /// at exactly zero.
        /// </summary>
        private static void Tone(float[] buffer, float start, float length, float f0, float f1,
            float glide, float attack, float decay, (float ratio, float amp)[] partials,
            float gain = 1f, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), buffer.Length - first);
            if (count <= 0) return;

            float release = Mathf.Min(0.02f, length * 0.4f);
            double phase = 0d;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                float f = f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, t / glide));
                if (vibratoHz > 0f) f *= 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t);

                phase += f / SampleRate;

                float env = Mathf.Exp(-t / decay);
                if (t < attack) env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / attack);
                float untilEnd = length - t;
                if (untilEnd < release) env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * untilEnd / release);

                float v = 0f;
                for (int k = 0; k < partials.Length; k++)
                    v += partials[k].amp * Mathf.Sin(2f * Mathf.PI * (float)((phase * partials[k].ratio) % 1d));

                buffer[first + i] += v * env * gain;
            }
        }

        /// <summary>
        /// Adds a burst of low-passed noise, the cutoff gliding from <paramref name="c0"/> to
        /// <paramref name="c1"/>. Seeded, so the same call always gives the same samples.
        /// </summary>
        private static void Noise(float[] buffer, float start, float length, float c0, float c1,
            float attack, float decay, int seed)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), buffer.Length - first);
            if (count <= 0) return;

            var rng = new System.Random(seed);
            float release = Mathf.Min(0.02f, length * 0.4f);
            float lp = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float cutoff = Mathf.Lerp(c0, c1, t / length);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

                float white = (float)(rng.NextDouble() * 2d - 1d);
                lp += a * (white - lp);

                float env = Mathf.Exp(-t / decay);
                if (t < attack) env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / attack);
                float untilEnd = length - t;
                if (untilEnd < release) env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * untilEnd / release);

                buffer[first + i] += lp * env * 1.6f;
            }
        }

        /// <summary>A band of noise whose cutoff sweeps up then back down, under a smooth swell.</summary>
        private static void Whoosh(float[] buffer, float length, float low, float high)
        {
            var rng = new System.Random(7);
            float lp = 0f, floor = 0f;
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), buffer.Length);

            for (int i = 0; i < count; i++)
            {
                float u = i / (float)count;
                float swell = Mathf.Sin(Mathf.PI * u);
                float cutoff = Mathf.Lerp(low, high, swell);

                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
                float white = (float)(rng.NextDouble() * 2d - 1d);
                lp += a * (white - lp);

                // Take the rumble out so it reads as air, not as a bass note.
                floor += 0.03f * (lp - floor);

                buffer[i] += (lp - floor) * swell * swell * 1.5f;
            }
        }

        /// <summary>
        /// Makes a rendered sound safe to play: mean removed (no DC thump), ends faded to zero
        /// (no click), peak set to the target (no clipping, consistent loudness).
        /// </summary>
        private static void Finish(float[] samples, float peak)
        {
            if (samples.Length == 0) return;

            double sum = 0d;
            for (int i = 0; i < samples.Length; i++) sum += samples[i];
            float mean = (float)(sum / samples.Length);
            for (int i = 0; i < samples.Length; i++) samples[i] -= mean;

            int fadeIn = Mathf.Min(samples.Length / 2, Mathf.RoundToInt(0.003f * SampleRate));
            for (int i = 0; i < fadeIn; i++)
                samples[i] *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / fadeIn);

            int fadeOut = Mathf.Min(samples.Length / 2, Mathf.RoundToInt(0.012f * SampleRate));
            for (int i = 0; i < fadeOut; i++)
                samples[samples.Length - 1 - i] *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / fadeOut);

            float max = 0f;
            for (int i = 0; i < samples.Length; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
            if (max <= 1e-6f) return;

            float scale = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
        }
    }
}
