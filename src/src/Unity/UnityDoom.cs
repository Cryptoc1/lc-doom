using System.Runtime.ExceptionServices;
using BepInEx.Configuration;
using ManagedDoom;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalCompany.Doom.Unity;

/* Based on DoomInUnityInspector/Unity/UnityDoom.cs, and ManagedDoom/src/Silk/SilkDoom.cs */

internal sealed class UnityDoom : IDisposable
{
    private readonly DoomConfigBinder binder;
    private readonly Config config;
    private readonly GameContent content;

    private readonly ManagedDoom.Doom? doom;
    private readonly int fpsScale;
    private int frameCount;

    private UnitySound? sound;
    private UnityUserInput? userInput;
    private UnityVideo? video;

    public Texture2D? Texture => video?.Texture;

    public UnityDoom(DoomConfigBinder binder)
    {
        try
        {
            var args = new CommandLineArgs(["iwad", binder.Wad.Value]);
            this.binder = binder;

            config = binder.GetConfig();
            content = new(args);
            doom = new(
                args,
                config,
                content,
                video = new(config, content),
                sound = !args.nosound.Present && !args.nosfx.Present ? new(config, content) : default!,
                default!,
                userInput = new(config));

            frameCount = -1;
            fpsScale = args.timedemo.Present ? 1 : config.video_fpsscale;
        }
        catch (Exception exception)
        {
            Dispose();
            ExceptionDispatchInfo.Throw(exception);
        }
    }

    public void Dispose()
    {
        if (sound is not null)
        {
            sound.Dispose();
            sound = null;
        }

        if (video is not null)
        {
            video.Dispose();
            video = null;
        }

        if (userInput is not null)
        {
            userInput.Dispose();
            userInput = null;
        }

        binder.Save(config);
    }

    public void Input(Mouse mouse, Keyboard keyboard)
    {
        userInput!.MoveMouse(mouse.delta.value);
        foreach (var (control, button) in UnityUserInput.MouseButtons(Mouse.current))
        {
            if (control.wasPressedThisFrame) PressButton(button);
            if (control.wasReleasedThisFrame) ReleaseButton(button);
        }

        foreach (var (keyCode, key) in UnityUserInput.Keys)
        {
            if (keyboard[keyCode].wasPressedThisFrame) PressKey(key);
            if (keyboard[keyCode].wasReleasedThisFrame) ReleaseKey(key);
        }

        void PressButton(DoomMouseButton button)
        {
            if (button is not DoomMouseButton.Unknown && !userInput!.PressedButtons.Contains(button)) userInput!.PressedButtons.Add(button);
            doom!.PostEvent(new(EventType.Mouse, DoomKey.Unknown));
        }

        void PressKey(DoomKey key)
        {
            if (key is not DoomKey.Unknown && !userInput!.PressedKeys.Contains(key)) userInput.PressedKeys.Add(key);
            doom!.PostEvent(new(EventType.KeyDown, key));
        }

        void ReleaseButton(DoomMouseButton button)
        {
            userInput!.PressedButtons.Remove(button);
            doom!.PostEvent(new(EventType.Mouse, DoomKey.Unknown));
        }
        void ReleaseKey(DoomKey key)
        {
            userInput!.PressedKeys.Remove(key);
            doom!.PostEvent(new(EventType.KeyUp, key));
        }
    }

    public bool Render()
    {
        if ((frameCount += 1) % fpsScale is 0)
        {
            var result = doom!.Update();
            if (result is UpdateResult.Completed)
            {
                return false;
            }

            video!.Render(
                doom!,
                Fixed.FromInt(frameCount % fpsScale + 1) / fpsScale);
        }

        return true;
    }
}


public sealed class DoomConfigBinder(ConfigFile file, (int width, int height) size)
{
    private static readonly Lazy<string> DefaultWadPath = new(GetWadPath);
    private static readonly Lazy<string> DefaultSoundFontPath = new(GetSoundFontPath);

    public ConfigEntry<bool> AlwaysRun { get; } = file.Bind("General", "Always Run", true);
    public ConfigEntry<string?> Wad { get; } = file.Bind<string?>("General", "WAD", DefaultWadPath.Value, "An absolute path to the WAD file to be loaded.");

    public ConfigEntry<string> KeyBackward { get; } = file.Bind("Keybinds", "Backward", "s,down");
    public ConfigEntry<string> KeyForward { get; } = file.Bind("Keybinds", "Forward", "w,up");
    public ConfigEntry<string> KeyFire { get; } = file.Bind("Keybinds", "Fire", "mouse1,f,lcontrol,rcontrol");
    public ConfigEntry<string> KeyRun { get; } = file.Bind("Keybinds", "Run", "lshift,rshift");
    public ConfigEntry<string> KeyStrafe { get; } = file.Bind("Keybinds", "Strafe", "lalt,ralt");
    public ConfigEntry<string> KeyStrafeLeft { get; } = file.Bind("Keybinds", "Strafe Left", "a");
    public ConfigEntry<string> KeyStrafeRight { get; } = file.Bind("Keybinds", "Strafe Right", "d");
    public ConfigEntry<string> KeyTurnLeft { get; } = file.Bind("Keybinds", "Turn Left", "left");
    public ConfigEntry<string> KeyTurnRight { get; } = file.Bind("Keybinds", "Turn Right", "right");
    public ConfigEntry<string> KeyUse { get; } = file.Bind("Keybinds", "Use", "space,mouse2");

    public ConfigEntry<bool> MouseDisableYAxis { get; } = file.Bind("Mouse", "Disable Y-Axis", false);
    public ConfigEntry<int> MouseSensitivity { get; } = file.Bind("Mouse", "Sensitivity", 8);

    // public ConfigEntry<bool> MusicEffect { get; } = file.Bind("Music", "Effects", true, "Whether music effects are enabled.");
    // public ConfigEntry<string?> MusicSoundFont { get; } = file.Bind<string?>("Music", "Sound Font", DefaultSoundFontPath.Value, "An absolute path to the SF2 file to be loaded.");
    // public ConfigEntry<int> MusicVolume { get; } = file.Bind("Music", "Volume", 8, "The volume of game music.");

    public ConfigEntry<bool> SfxRandomPitch { get; } = file.Bind("Sfx", "Random Pitch", true);
    public ConfigEntry<int> SfxVolume { get; } = file.Bind("Sfx", "Volume", 8, "The volume of sound effects.");

    public ConfigEntry<bool> VideoDisplayMessage { get; } = file.Bind("Video", "Display Message", true);
    public ConfigEntry<int> VideoFpsScale { get; } = file.Bind("Video", "FPS Scale", 2);
    public ConfigEntry<int> VideoGammaCorrection { get; } = file.Bind("Video", "Gamma Correction", 2);
    public ConfigEntry<bool> VideoHighResolution { get; } = file.Bind("Video", "High Resolution", true);
    public ConfigEntry<int> VideoScreenSize { get; } = file.Bind("Video", "Screen Size", 7);

    public Config GetConfig() => new()
    {
        // audio_musiceffect = MusicEffect.Value,
        // audio_musicvolume = MusicVolume.Value,
        audio_randompitch = SfxRandomPitch.Value,
        // audio_soundfont = MusicSoundFont.Value,
        audio_soundvolume = SfxVolume.Value,

        game_alwaysrun = AlwaysRun.Value,

        key_backward = KeyBinding.Parse(KeyBackward.Value),
        key_fire = KeyBinding.Parse(KeyFire.Value),
        key_forward = KeyBinding.Parse(KeyForward.Value),
        key_run = KeyBinding.Parse(KeyRun.Value),
        key_strafe = KeyBinding.Parse(KeyStrafe.Value),
        key_strafeleft = KeyBinding.Parse(KeyStrafeLeft.Value),
        key_straferight = KeyBinding.Parse(KeyStrafeRight.Value),
        key_turnleft = KeyBinding.Parse(KeyTurnLeft.Value),
        key_turnright = KeyBinding.Parse(KeyTurnRight.Value),
        key_use = KeyBinding.Parse(KeyUse.Value),

        mouse_disableyaxis = MouseDisableYAxis.Value,
        mouse_sensitivity = MouseSensitivity.Value,

        video_displaymessage = VideoDisplayMessage.Value,
        video_fpsscale = VideoFpsScale.Value,
        video_fullscreen = false,
        video_gamescreensize = VideoScreenSize.Value,
        video_gammacorrection = VideoGammaCorrection.Value,
        video_highresolution = VideoHighResolution.Value,
        video_screenheight = size.height,
        video_screenwidth = size.width,
    };

    private static string GetSoundFontPath()
    {
        var location = Path.GetDirectoryName(
            typeof(DoomPlugin).Assembly.Location);

        return Path.Combine(location, "RLNDGM.SF2");
    }

    private static string GetWadPath()
    {
        var location = Path.GetDirectoryName(
            typeof(DoomPlugin).Assembly.Location);

        return Path.Combine(location, "DOOM1.WAD");
    }

    public void Save(Config config)
    {
        AlwaysRun.Value = config.game_alwaysrun;

        KeyBackward.Value = config.key_backward.ToString();
        KeyForward.Value = config.key_forward.ToString();
        KeyFire.Value = config.key_fire.ToString();
        KeyRun.Value = config.key_run.ToString();
        KeyStrafe.Value = config.key_strafe.ToString();
        KeyStrafeLeft.Value = config.key_strafeleft.ToString();
        KeyStrafeRight.Value = config.key_straferight.ToString();
        KeyTurnLeft.Value = config.key_turnleft.ToString();
        KeyTurnRight.Value = config.key_turnright.ToString();
        KeyUse.Value = config.key_use.ToString();

        MouseDisableYAxis.Value = config.mouse_disableyaxis;
        MouseSensitivity.Value = config.mouse_sensitivity;

        // MusicEffect.Value = config.audio_musiceffect;
        // MusicSoundFont.Value = config.audio_soundfont;
        // MusicVolume.Value = config.audio_musicvolume;

        SfxRandomPitch.Value = config.audio_randompitch;
        SfxVolume.Value = config.audio_soundvolume;

        VideoDisplayMessage.Value = config.video_displaymessage;
        VideoFpsScale.Value = config.video_fpsscale;
        VideoGammaCorrection.Value = config.video_gammacorrection;
        VideoHighResolution.Value = config.video_highresolution;
        VideoScreenSize.Value = config.video_gamescreensize;

        file.Save();
    }
}
