using System.Collections.Generic;
using UnityEngine;

namespace WrestleGame
{
    public class WrestlingAudio : MonoBehaviour
    {
        public static WrestlingAudio Instance { get; private set; }

        private AudioSource sfxSource;
        private AudioSource crowdSource;
        private AudioSource musicSource;

        private AudioClip bellClip;
        private AudioClip punchClip;
        private AudioClip heavyHitClip;
        private AudioClip slamClip;
        private AudioClip ropeClip;
        private AudioClip blockClip;
        private AudioClip reversalClip;
        private AudioClip finisherClip;
        private AudioClip refCountClip;
        private AudioClip crowdCheerClip;
        private AudioClip crowdGaspClip;
        private AudioClip victoryFanfareClip;
        private AudioClip uiClickClip;

        [Range(0f, 1f)] public float sfxVolume = 0.85f;
        [Range(0f, 1f)] public float crowdVolume = 0.45f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            crowdSource = gameObject.AddComponent<AudioSource>();
            crowdSource.playOnAwake = false;
            crowdSource.loop = true;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            GenerateAllAudioClips();
            StartAmbientCrowd();
        }

        private void GenerateAllAudioClips()
        {
            bellClip = CreateBellSound();
            punchClip = CreatePunchSound();
            heavyHitClip = CreateHeavyHitSound();
            slamClip = CreateSlamSound();
            ropeClip = CreateRopeSound();
            blockClip = CreateBlockSound();
            reversalClip = CreateReversalSound();
            finisherClip = CreateFinisherSound();
            refCountClip = CreateRefCountSound();
            crowdCheerClip = CreateCrowdCheerSound();
            crowdGaspClip = CreateCrowdGaspSound();
            victoryFanfareClip = CreateVictoryFanfare();
            uiClickClip = CreateUiClickSound();
        }

        private void StartAmbientCrowd()
        {
            if (crowdCheerClip != null && crowdSource != null)
            {
                crowdSource.clip = crowdCheerClip;
                crowdSource.volume = crowdVolume * 0.25f;
                crowdSource.Play();
            }
        }

        public void PlayBell() => PlayOneShot(bellClip, 1.0f);
        public void PlayPunch() => PlayOneShot(punchClip, 0.9f, Random.Range(0.9f, 1.15f));
        public void PlayHeavyHit() => PlayOneShot(heavyHitClip, 1.0f, Random.Range(0.85f, 1.1f));
        public void PlaySlam()
        {
            PlayOneShot(slamClip, 1.0f, Random.Range(0.9f, 1.05f));
            PlayCrowdCheer(0.7f);
        }
        public void PlayRopeBounce() => PlayOneShot(ropeClip, 0.95f, Random.Range(0.95f, 1.15f));
        public void PlayBlock() => PlayOneShot(blockClip, 0.8f, Random.Range(0.95f, 1.1f));
        public void PlayReversal()
        {
            PlayOneShot(reversalClip, 1.0f);
            PlayCrowdCheer(0.8f);
        }
        public void PlayFinisher() => PlayOneShot(finisherClip, 1.0f);
        public void PlayRefCount(int count) => PlayOneShot(refCountClip, 1.0f, 0.9f + (count * 0.1f));
        public void PlayCrowdCheer(float intensity = 1.0f)
        {
            if (crowdSource != null)
            {
                crowdSource.volume = Mathf.Clamp01(crowdVolume * intensity);
            }
            PlayOneShot(crowdCheerClip, 0.7f * intensity);
        }
        public void PlayCrowdGasp() => PlayOneShot(crowdGaspClip, 0.85f);
        public void PlayVictory()
        {
            PlayOneShot(victoryFanfareClip, 0.95f);
            PlayCrowdCheer(1.0f);
        }
        public void PlayUiClick() => PlayOneShot(uiClickClip, 0.6f);

        private void PlayOneShot(AudioClip clip, float volumeScale = 1.0f, float pitch = 1.0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        #region Sound Generators

        private AudioClip CreateBellSound()
        {
            int sampleRate = 44100;
            float duration = 1.6f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float freq1 = 1200f;
            float freq2 = 2400f;
            float freq3 = 3600f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 3.5f);
                float s = Mathf.Sin(2 * Mathf.PI * freq1 * t) * 0.5f +
                          Mathf.Sin(2 * Mathf.PI * freq2 * t) * 0.3f +
                          Mathf.Sin(2 * Mathf.PI * freq3 * t) * 0.15f;
                // Add a small metallic strike transient
                float strike = (t < 0.05f) ? (Random.value * 2f - 1f) * Mathf.Exp(-t * 80f) * 0.4f : 0f;
                samples[i] = Mathf.Clamp((s + strike) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("BellSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreatePunchSound()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 24f);
                float noise = (Random.value * 2f - 1f) * 0.6f;
                float bass = Mathf.Sin(2 * Mathf.PI * 90f * Mathf.Exp(-t * 15f) * t) * 0.7f;
                samples[i] = Mathf.Clamp((noise + bass) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("PunchSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateHeavyHitSound()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                float noise = (Random.value * 2f - 1f) * 0.8f;
                float thud = Mathf.Sin(2 * Mathf.PI * 65f * Mathf.Exp(-t * 10f) * t) * 0.9f;
                samples[i] = Mathf.Clamp((noise + thud) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("HeavyHitSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateSlamSound()
        {
            int sampleRate = 44100;
            float duration = 0.7f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float sub = Mathf.Sin(2 * Mathf.PI * 45f * Mathf.Exp(-t * 5f) * t) * 1.0f;
                float canvas = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f) * 0.7f;
                samples[i] = Mathf.Clamp((sub + canvas) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SlamSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateRopeSound()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9f);
                float freq = 160f + Mathf.Sin(2 * Mathf.PI * 25f * t) * 40f;
                float twang = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.7f;
                float snap = (t < 0.03f) ? (Random.value * 2f - 1f) * 0.6f : 0f;
                samples[i] = Mathf.Clamp((twang + snap) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("RopeSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateBlockSound()
        {
            int sampleRate = 44100;
            float duration = 0.2f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 30f);
                float tone = Mathf.Sin(2 * Mathf.PI * 380f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 720f * t) * 0.3f;
                float click = (Random.value * 2f - 1f) * 0.4f;
                samples[i] = Mathf.Clamp((tone + click) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("BlockSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateReversalSound()
        {
            int sampleRate = 44100;
            float duration = 0.4f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                float whoosh = (Random.value * 2f - 1f) * Mathf.Sin(2 * Mathf.PI * (200f + t * 600f) * t) * 0.6f;
                float tone = Mathf.Sin(2 * Mathf.PI * (300f + t * 400f) * t) * 0.4f;
                samples[i] = Mathf.Clamp((whoosh + tone) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ReversalSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateFinisherSound()
        {
            int sampleRate = 44100;
            float duration = 0.8f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = (t < 0.6f) ? (t / 0.6f) : (1f - (t - 0.6f) / 0.2f);
                float freq = 200f + (t / duration) * 800f;
                float chime = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.3f;
                samples[i] = Mathf.Clamp(chime * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("FinisherSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateRefCountSound()
        {
            int sampleRate = 44100;
            float duration = 0.3f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 20f);
                float slap = (Random.value * 2f - 1f) * 0.7f + Mathf.Sin(2 * Mathf.PI * 180f * t) * 0.6f;
                samples[i] = Mathf.Clamp(slap * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("RefCountSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCrowdCheerSound()
        {
            int sampleRate = 44100;
            float duration = 2.0f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float smooth = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                float rawNoise = Random.value * 2f - 1f;
                // Low-pass filter noise for crowd roar texture
                smooth = smooth * 0.92f + rawNoise * 0.08f;
                float roar = smooth * 3.5f;
                samples[i] = Mathf.Clamp(roar * env * 0.7f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CrowdCheerSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCrowdGaspSound()
        {
            int sampleRate = 44100;
            float duration = 0.9f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float smooth = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 4f);
                float rawNoise = Random.value * 2f - 1f;
                smooth = smooth * 0.88f + rawNoise * 0.12f;
                samples[i] = Mathf.Clamp(smooth * 2.8f * env * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CrowdGaspSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateVictoryFanfare()
        {
            int sampleRate = 44100;
            float duration = 2.5f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float[] notes = new float[] { 261.63f, 329.63f, 392.00f, 523.25f, 659.25f, 783.99f };

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.35f));
                float freq = notes[noteIndex];
                float noteT = t % 0.35f;
                float env = Mathf.Exp(-noteT * 6f);
                float tone = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.25f;
                samples[i] = Mathf.Clamp(tone * env * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("VictoryFanfare", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateUiClickSound()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 50f);
                float beep = Mathf.Sin(2 * Mathf.PI * 880f * t) * 0.6f;
                samples[i] = Mathf.Clamp(beep * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("UiClickSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
