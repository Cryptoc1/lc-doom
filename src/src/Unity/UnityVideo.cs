using ManagedDoom;
using ManagedDoom.Video;
using UnityEngine;
using DoomRenderer = ManagedDoom.Video.Renderer;

namespace LethalCompany.Doom.Unity;

/* Based on DoomInUnityInspector/Unity/UnityVideo.cs, and ManagedDoom/src/Silk/SilkVideo.cs */

internal sealed class UnityVideo : IVideo, IDisposable
{
    private byte[] buffer;
    private readonly DoomRenderer renderer;

    public Texture2D Texture { get; private set; }

    public bool DisplayMessage { get => renderer.DisplayMessage; set => renderer.DisplayMessage = value; }
    public int GammaCorrectionLevel { get => renderer.GammaCorrectionLevel; set => renderer.GammaCorrectionLevel = value; }
    public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;
    public int MaxWindowSize => renderer.MaxWindowSize;
    public int WindowSize { get => renderer.WindowSize; set => renderer.WindowSize = value; }
    public int WipeBandCount => renderer.WipeBandCount;
    public int WipeHeight => renderer.WipeHeight;

    public UnityVideo(Config config, GameContent content)
    {
        try
        {
            var (width, height) = config.video_highresolution ? (400, 640) : (200, 320);
            renderer = new DoomRenderer(config, content);

            buffer = new byte[4 * width * height];
            Texture = new(width, height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                name = "LCDoomScreen"
            };
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        buffer = null!;
        if (Texture is not null)
        {
            UnityEngine.Object.DestroyImmediate(Texture, true);
            Texture = null!;
        }
    }

    public bool HasFocus() => true;

    public void InitializeWipe() => renderer.InitializeWipe();

    public void Render(ManagedDoom.Doom doom, Fixed frame)
    {
        renderer.Render(doom, buffer, frame);

        Texture.SetPixelData(buffer, 0, 0);
        Texture.Apply(false, false);
    }
}
