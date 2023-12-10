using System.Runtime.ExceptionServices;
using ManagedDoom;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalCompany.Doom.Unity;

/* Based on DoomInUnityInspector/Unity/UnityDoom.cs, and ManagedDoom/src/Silk/SilkDoom.cs */

internal sealed class UnityDoom : IDisposable
{
    private readonly Config config;
    private readonly GameContent content;

    private readonly ManagedDoom.Doom? doom;
    private readonly int fpsScale;
    private int frameCount;

    private UnitySound? sound;
    private UnityUserInput? userInput;
    private UnityVideo? video;

    public Texture2D? Texture => video?.Texture;

    public UnityDoom(CommandLineArgs args, (int width, int height) size, string soundFontPath = "")
    {
        try
        {
            config = new()
            {
                audio_soundfont = soundFontPath,
                mouse_disableyaxis = true,
                video_screenwidth = size.width,
                video_screenheight = size.height
            };

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
