using System.Diagnostics.CodeAnalysis;
using LethalCompany.Doom.Unity;
using ManagedDoom;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalCompany.Doom;

[HarmonyPatch(typeof(Terminal))]
internal static class TerminalPatches
{
    private static readonly Lazy<string> SoundFontPath = new(GetSoundFontPath);
    private static readonly Vector2 TerminalImageSize = new(428, 500);
    private static readonly Vector3 TerminalImageRotation = new(0, 0, 90);
    private static readonly Vector2 TerminalImageTranslation = new(11.65f, -48);
    private static readonly Lazy<string> WadPath = new(GetWadPath);

    [MemberNotNullWhen(true, nameof(cpu), nameof(doom), nameof(terminalImageSize))]
    private static bool isRunning => cpu is not null && doom is not null && terminalImageSize.HasValue;
    private static Coroutine? cpu;
    private static UnityDoom? doom;
    private static Vector2? terminalImageSize;

    private static void DestroyDoom(Terminal terminal)
    {
        if (!isRunning) return;

        terminal.StopCoroutine(cpu);
        cpu = null;

        doom.Dispose();
        doom = null;

        terminal.terminalImage.rectTransform.anchoredPosition -= TerminalImageTranslation;
        terminal.terminalImage.rectTransform.Rotate(TerminalImageRotation);

        terminal.terminalImage.rectTransform.sizeDelta = terminalImageSize.Value;
        terminalImageSize = null;

        terminal.screenText.enabled = true;
        terminal.screenText.text += "EXIT (0)\n\n";

        terminal.terminalImageMask.enabled = true;
        terminal.topRightText.enabled = true;

        GC.Collect();
        DoomPlugin.Value.Logger.LogInfo("TerminalPatches: destroyed doom instance");
    }

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

    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void OnPostAwake(Terminal __instance)
    {
        __instance.terminalNodes.allKeywords = [
            .. __instance.terminalNodes.allKeywords,
            DoomKeyword()];

        var node = __instance.terminalNodes.allKeywords.First(keyword => keyword.word is "other");
        node.specialKeywordResult.displayText = node.specialKeywordResult.displayText.TrimEnd()
            + "\n\n>DOOM\nBoss makes a dollar, I make a dime. That's why I play DOOM on company time.\n\n";

        static TerminalKeyword DoomKeyword()
        {
            var node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;
            node.displayText = @$"{DoomPluginInfo.Name} v{DoomPluginInfo.Version}
https://github.com/cryptoc1/lc-doom
 
CREDITS:
• id Software
  https://www.idsoftware.com

• {ApplicationInfo.Title}
  https://github.com/sinshu/managed-doom

• DoomInUnityInspector
  https://github.com/xabblll/DoomInUnityInspector

LOADING .... ";

            node.persistentImage = true;
            node.terminalEvent = "doom";

            var keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            keyword.isVerb = false;
            keyword.specialKeywordResult = node;
            keyword.word = "doom";

            return keyword;
        }
    }

    [HarmonyPatch(nameof(Terminal.QuitTerminal))]
    [HarmonyPostfix]
    public static void OnPostQuitTerminal(Terminal __instance) => DestroyDoom(__instance);

    [HarmonyPatch(nameof(Terminal.LoadTerminalImage))]
    [HarmonyPostfix]
    public static void OnPostLoadTerminalImage(Terminal __instance, TerminalNode node)
    {
        if (node.terminalEvent is "doom" && doom is not null)
        {
            __instance.screenText.text += "LOADED!\n\n";
            __instance.terminalImage.texture = doom.Texture;

            // NOTE: override behavior: allow doom while in pre-game lobby
            if (StartOfRound.Instance.inShipPhase is true) __instance.displayingPersistentImage = doom.Texture;

            cpu = __instance.StartCoroutine(
                RenderDoom(__instance));

            __instance.terminalImage.enabled = true;
            __instance.topRightText.enabled = false;
            __instance.screenText.enabled = false;
        }
    }

    [HarmonyPatch(nameof(Terminal.LoadNewNode))]
    [HarmonyPrefix]
    public static void OnPreLoadNewNode(Terminal __instance, TerminalNode node)
    {
        if (node.terminalEvent is "doom" && doom is null)
        {
            terminalImageSize = __instance.terminalImage.rectTransform.sizeDelta;
            __instance.terminalImage.rectTransform.sizeDelta = TerminalImageSize;
            __instance.terminalImage.rectTransform.Rotate(-TerminalImageRotation);
            __instance.terminalImage.rectTransform.anchoredPosition += TerminalImageTranslation;
            __instance.terminalImageMask.enabled = false;

            doom = new(
                new(["iwad", WadPath.Value]),
                (Mathf.RoundToInt(TerminalImageSize.x), Mathf.RoundToInt(TerminalImageSize.y)),
                SoundFontPath.Value);
        }
    }

    [HarmonyPatch("PressESC")]
    [HarmonyPrefix]
    public static bool OnPrePressESC()
    {
        // NOTE: prevent original `PressESC` from being called while doom is running
        return !isRunning;
    }

    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    public static void OnPreUpdate()
    {
        if (isRunning) doom.Input(Mouse.current, Keyboard.current);
    }

    private static IEnumerator RenderDoom(Terminal terminal)
    {
        while (doom is not null)
        {
            if (!doom.Render())
            {
                DestroyDoom(terminal);
                yield break;
            }

            yield return new WaitForEndOfFrame();
        }
    }
}
