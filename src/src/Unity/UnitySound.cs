using ManagedDoom;
using ManagedDoom.Audio;
using UnityEngine;

namespace LethalCompany.Doom.Unity;

/* Based on DoomInUnityInspector/Unity/UnitySound.cs */

internal sealed class UnitySound : ISound, IDisposable
{
    private static readonly int ChannelCount = 8;

    private static readonly float FastDecay = (float)Math.Pow(0.5, 1.0 / (35 / 5));
    private static readonly float SlowDecay = (float)Math.Pow(0.5, 1.0 / 35);

    private static readonly float ClipDist = 1200;
    private static readonly float CloseDist = 160;
    private static readonly float Attenuator = ClipDist - CloseDist;

    public int MaxVolume => 15;
    public int Volume
    {
        get => config.audio_soundvolume;
        set
        {
            config.audio_soundvolume = value;
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
        }
    }

    private readonly float[] amplitudes;
    private readonly Config config;
    private readonly ChannelInfo[] infos;
    private readonly DoomRandom? random;

    private AudioClip[] buffers;
    private AudioSource[] channels;

    private AudioSource uiChannel;
    private Sfx uiReserved;

    private DateTime lastUpdate;
    private Mobj listener = default!;
    private float masterVolumeDecay;

    public UnitySound(Config config, GameContent content)
    {
        try
        {
            this.config = config;
            config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolume);

            buffers = new AudioClip[DoomInfo.SfxNames.Length];
            amplitudes = new float[DoomInfo.SfxNames.Length];

            if (config.audio_randompitch) random = new();

            for (var i = 0; i < DoomInfo.SfxNames.Length; i++)
            {
                var name = $"DS{DoomInfo.SfxNames[i]}".ToUpperInvariant();
                if (content.Wad.GetLumpNumber(name) is -1) continue;

                var samples = GetSamples(content.Wad, name, out var sampleRate, out var sampleCount);
                if (samples?.Length is not null or 0)
                {
                    var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
                    clip.SetData(samples, 0);

                    buffers[i] = clip;
                    amplitudes[i] = GetAmplitude(samples, sampleRate, samples.Length);
                }
            }

            channels = CreateChannels(ChannelCount);
            infos = new ChannelInfo[ChannelCount];
            for (var i = 0; i < ChannelCount; i++) infos[i] = new();

            lastUpdate = DateTime.MinValue;
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;

            uiChannel = DoomAudioSource.Create("UI");
            uiReserved = Sfx.NONE;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private AudioSource[] CreateChannels(int channelCount)
    {
        var channels = new AudioSource[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            var channel = DoomAudioSource.Create($"SFX_{i}");
            channel.spatialBlend = 0f;
            channel.playOnAwake = false;

            channels[i] = channel;
        }

        return channels;
    }

    public void Dispose()
    {
        if (channels is not null)
        {
            for (var i = 0; i < channels.Length; i++)
            {
                if (channels[i] is not null)
                {
                    channels[i].Stop();
                    UnityEngine.Object.DestroyImmediate(channels[i].gameObject, true);
                    channels[i] = null!;
                }
            }

            channels = null!;
        }

        if (buffers is not null)
        {
            for (var i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] is not null)
                {
                    UnityEngine.Object.DestroyImmediate(buffers[i], true);
                    buffers[i] = null!;
                }
            }

            buffers = null!;
        }

        if (uiChannel is not null)
        {
            UnityEngine.Object.DestroyImmediate(uiChannel.gameObject, true);
            uiChannel = null!;
        }
    }

    private static float[]? GetSamples(Wad wad, string name, out int sampleRate, out int sampleCount)
    {
        var data = wad.ReadLump(name);
        if (data.Length < 8)
        {
            sampleRate = -1;
            sampleCount = -1;
            return null;
        }

        sampleRate = BitConverter.ToUInt16(data, 2);
        sampleCount = BitConverter.ToInt32(data, 4);

        var offset = 8;
        if (ContainsDmxPadding(data))
        {
            offset += 16;
            sampleCount -= 32;
        }

        if (sampleCount > 0)
        {
            var samples = new float[sampleCount];
            for (int i = offset; i < sampleCount; i++)
            {
                samples[i] = (float)data[i] / 127 - 1f;
            }

            return samples;
        }

        return [];
    }

    /// <summary> Check if the data contains pad bytes; if the first and last 16 samples are the same, the data should contain pad bytes. </summary>
    /// <see cref="https://doomwiki.org/wiki/Sound"/>
    private static bool ContainsDmxPadding(byte[] data)
    {
        var sampleCount = BitConverter.ToInt32(data, 4);
        if (sampleCount < 32) return false;

        var first = data[8];
        for (var i = 1; i < 16; i++)
        {
            if (data[8 + i] != first) return false;
        }

        var last = data[8 + sampleCount - 1];
        for (var i = 1; i < 16; i++)
        {
            if (data[8 + sampleCount - i - 1] != last) return false;
        }

        return true;
    }

    private static float GetAmplitude(float[] samples, int sampleRate, int sampleCount)
    {
        var max = 0f;
        if (sampleCount > 0)
        {
            var count = Math.Min(sampleRate / 5, sampleCount);
            for (var t = 0; t < count; t++)
            {
                var a = samples[t] - 0.5f;

                if (a < 0f) a = -a;
                if (a > max) max = a;
            }
        }

        return max;
    }

    public void SetListener(Mobj listener) => this.listener = listener;

    public void Update()
    {
        var now = DateTime.Now;
        if ((now - lastUpdate).TotalSeconds < 0.01)
        {
            // Don't update so frequently (for timedemo).
            return;
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            var channel = channels[i];

            if (info.Playing is not Sfx.NONE)
            {
                info.Priority *= info.Type is SfxType.Diffuse ? SlowDecay : FastDecay;
                UpdateAudioSource(channel, info);
            }

            if (info.Reserved is not Sfx.NONE)
            {
                if (info.Playing is not Sfx.NONE) channel.Stop();

                channel.clip = buffers[(int)info.Reserved];
                UpdateAudioSource(channel, info);

                channel.pitch = GetPitch(info.Type, info.Reserved);
                channel.PlayOneShot(channel.clip);

                info.Playing = info.Reserved;
                info.Reserved = Sfx.NONE;
            }
        }

        if (uiReserved is not Sfx.NONE)
        {
            if (uiChannel.isPlaying) uiChannel.Stop();

            uiChannel.volume = masterVolumeDecay;
            uiChannel.clip = buffers[(int)uiReserved];
            uiChannel.Play();

            uiReserved = Sfx.NONE;
        }

        lastUpdate = now;
    }

    public void StartSound(Sfx sfx)
    {
        if (buffers[(int)sfx] is null) return;
        uiReserved = sfx;
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type) => StartSound(mobj, sfx, type, 100);

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
    {
        if (buffers[(int)sfx] is null) return;

        var x = (mobj.X - listener.X).ToFloat();
        var y = (mobj.Y - listener.Y).ToFloat();
        var dist = MathF.Sqrt(x * x + y * y);

        float priority = type is SfxType.Diffuse ? volume
            : amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume;

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj && info.Type == type)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Volume = volume;
                return;
            }
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Reserved is Sfx.NONE && info.Playing is Sfx.NONE)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
                return;
            }
        }

        var minPriority = float.MaxValue;
        var minChannel = -1;
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Priority < minPriority)
            {
                minPriority = info.Priority;
                minChannel = i;
            }
        }

        // if (minChannel >= 0 && priority >= minPriority)
        if (priority >= minPriority)
        {
            var info = infos[minChannel];

            info.Reserved = sfx;
            info.Priority = priority;
            info.Source = mobj;
            info.Type = type;
            info.Volume = volume;
        }
    }

    public void StopSound(Mobj mobj)
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj)
            {
                info.LastX = info.Source.X;
                info.LastY = info.Source.Y;
                info.Source = null;
                info.Volume /= 5;
            }
        }
    }

    public void Reset()
    {
        random?.Clear();
        for (var i = 0; i < infos.Length; i++)
        {
            channels[i].Stop();
            infos[i].Clear();
        }

        listener = null!;
    }

    public void Pause()
    {
    }

    public void Resume()
    {
    }

    private void UpdateAudioSource(AudioSource sound, ChannelInfo info)
    {
        if (info.Type is SfxType.Diffuse)
        {
            sound.panStereo = 0f;
            sound.volume = 0.01f * masterVolumeDecay * info.Volume;
            return;
        }

        Fixed sourceX, sourceY;
        if (info.Source is null)
        {
            sourceX = info.LastX;
            sourceY = info.LastY;
        }
        else
        {
            sourceX = info.Source.X;
            sourceY = info.Source.Y;
        }

        var x = (sourceX - listener.X).ToFloat();
        var y = (sourceY - listener.Y).ToFloat();

        if (Math.Abs(x) < 16 && Math.Abs(y) < 16)
        {
            sound.panStereo = 0f;
            sound.volume = 0.01f * masterVolumeDecay * info.Volume;
            return;
        }

        var dist = MathF.Sqrt(x * x + y * y);
        var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();
        angle = angle * Mathf.Rad2Deg / 180f;
        angle = Mathf.Clamp(angle, -0.2f, 0.2f);

        sound.panStereo = angle;
        sound.volume = 0.01f * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume;
    }

    private float GetDistanceDecay(float dist)
    {
        if (dist < CloseDist) return 1f;
        return Math.Max((ClipDist - dist) / Attenuator, 0f);
    }

    private float GetPitch(SfxType type, Sfx sfx)
    {
        if (random is not null)
        {
            if (type is SfxType.Voice) return 1.0F + 0.075F * (random.Next() - 128) / 128;
            return sfx switch
            {
                Sfx.ITEMUP or Sfx.TINK or Sfx.RADIO => 1.0f,
                _ => 1.0F + 0.025F * (random.Next() - 128) / 128,
            };
        }

        return 1.0F;
    }

    private sealed class ChannelInfo
    {
        public Sfx Reserved;
        public Sfx Playing;
        public float Priority;

        public Mobj? Source;
        public SfxType Type;
        public int Volume;
        public Fixed LastX;
        public Fixed LastY;

        public void Clear()
        {
            Reserved = Sfx.NONE;
            Playing = Sfx.NONE;
            Priority = 0;
            Source = null;
            Type = 0;
            Volume = 0;
            LastX = Fixed.Zero;
            LastY = Fixed.Zero;
        }
    }
}
