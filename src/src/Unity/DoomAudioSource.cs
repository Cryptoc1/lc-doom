using UnityEngine;

namespace LethalCompany.Doom.Unity;

internal static class DoomAudioSource
{
    public static AudioSource Create(string name)
    {
        var gameObject = new GameObject($"LCDoomAudio_{name}")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        var audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0;

        return audio;
    }
}
