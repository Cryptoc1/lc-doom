using ManagedDoom;
using ManagedDoom.UserInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LethalCompany.Doom.Unity;

/* Based on DoomInUnityInspector/Unity/UnityUserInput.cs */

internal sealed class UnityUserInput : IUserInput, IDisposable
{
    private readonly Config config;
    private readonly bool[] weaponKeys;

    private Vector2 mouseDelta = new(0, 0);
    private Vector2 mousePosition = new(0, 0);
    private Vector2 mousePreviousPosition = new(0, 0);

    private int turnHeld;

    public int MaxMouseSensitivity => 15;
    public int MouseSensitivity { get => config.mouse_sensitivity; set => config.mouse_sensitivity = value; }

    public List<DoomMouseButton> PressedButtons { get; } = [];
    public List<DoomKey> PressedKeys { get; } = [];

    public UnityUserInput(Config config)
    {
        try
        {
            this.config = config;
            weaponKeys = new bool[7];
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void BuildTicCmd(TicCmd cmd)
    {
        var keyForward = IsPressed(config.key_forward);
        var keyBackward = IsPressed(config.key_backward);
        var keyStrafeLeft = IsPressed(config.key_strafeleft);
        var keyStrafeRight = IsPressed(config.key_straferight);
        var keyTurnLeft = IsPressed(config.key_turnleft);
        var keyTurnRight = IsPressed(config.key_turnright);
        var keyFire = IsPressed(config.key_fire);
        var keyUse = IsPressed(config.key_use);
        var keyRun = IsPressed(config.key_run);
        var keyStrafe = IsPressed(config.key_strafe);

        weaponKeys[0] = PressedKeys.Contains(DoomKey.Num1);
        weaponKeys[1] = PressedKeys.Contains(DoomKey.Num2);
        weaponKeys[2] = PressedKeys.Contains(DoomKey.Num3);
        weaponKeys[3] = PressedKeys.Contains(DoomKey.Num4);
        weaponKeys[4] = PressedKeys.Contains(DoomKey.Num5);
        weaponKeys[5] = PressedKeys.Contains(DoomKey.Num6);
        weaponKeys[6] = PressedKeys.Contains(DoomKey.Num7);

        cmd.Clear();

        var strafe = keyStrafe;
        var speed = keyRun ? 1 : 0;
        var forward = 0;
        var side = 0;

        if (config.game_alwaysrun) speed = 1 - speed;

        turnHeld = keyTurnLeft || keyTurnRight ? turnHeld + 1 : turnHeld = 0;
        int turnSpeed = turnHeld < PlayerBehavior.SlowTurnTics ? 2 : speed;

        if (strafe)
        {
            if (keyTurnRight) side += PlayerBehavior.SideMove[speed];
            if (keyTurnLeft) side -= PlayerBehavior.SideMove[speed];
        }
        else
        {
            if (keyTurnRight) cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
            if (keyTurnLeft) cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
        }

        if (keyForward) forward += PlayerBehavior.ForwardMove[speed];
        if (keyBackward) forward -= PlayerBehavior.ForwardMove[speed];

        if (keyStrafeLeft) side -= PlayerBehavior.SideMove[speed];
        if (keyStrafeRight) side += PlayerBehavior.SideMove[speed];

        if (keyFire) cmd.Buttons |= TicCmdButtons.Attack;
        if (keyUse) cmd.Buttons |= TicCmdButtons.Use;

        // Check weapon keys.
        for (var i = 0; i < weaponKeys.Length; i++)
        {
            if (weaponKeys[i])
            {
                cmd.Buttons |= TicCmdButtons.Change;
                cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                break;
            }
        }

        var ms = 0.5F * config.mouse_sensitivity;
        var mx = (int)MathF.Round(ms * mouseDelta.x);
        var my = (int)MathF.Round(ms * -mouseDelta.y);
        forward += my;

        if (strafe) side += mx * 2;
        else cmd.AngleTurn -= (short)(mx * 0x16);

        if (forward > PlayerBehavior.MaxMove) forward = PlayerBehavior.MaxMove;
        else if (forward < -PlayerBehavior.MaxMove) forward = -PlayerBehavior.MaxMove;

        if (side > PlayerBehavior.MaxMove) side = PlayerBehavior.MaxMove;
        else if (side < -PlayerBehavior.MaxMove) side = -PlayerBehavior.MaxMove;

        cmd.ForwardMove += (sbyte)forward;
        cmd.SideMove += (sbyte)side;
    }

    public void Dispose()
    {
        PressedButtons.Clear();
        PressedKeys.Clear();
    }

    public void GrabMouse()
    {
    }

    private bool IsPressed(KeyBinding binding)
    {
        foreach (var key in binding.Keys)
        {
            if (PressedKeys.Contains(key)) return true;
        }

        foreach (var button in binding.MouseButtons)
        {
            if (PressedButtons.Contains(button)) return true;
        }

        return false;
    }

    public void MoveMouse(Vector2 delta)
    {
        mousePreviousPosition.x = mousePosition.x;
        mousePreviousPosition.y = mousePosition.y;

        mousePosition += delta;
        mouseDelta = mousePosition - mousePreviousPosition;

        if (config.mouse_disableyaxis) mouseDelta.y = 0;
    }

    public void ReleaseMouse()
    {
    }

    public void Reset()
    {
    }

    public static readonly IEnumerable<(Key InputKey, DoomKey Key)> Keys = [
        (Key.Space, DoomKey.Space),
        (Key.Comma, DoomKey.Comma),
        (Key.Minus, DoomKey.Subtract),
        (Key.Period, DoomKey.Period),
        (Key.Slash, DoomKey.Slash),
        (Key.Digit0, DoomKey.Num0),
        (Key.Digit1, DoomKey.Num1),
        (Key.Digit2, DoomKey.Num2),
        (Key.Digit3, DoomKey.Num3),
        (Key.Digit4, DoomKey.Num4),
        (Key.Digit5, DoomKey.Num5),
        (Key.Digit6, DoomKey.Num6),
        (Key.Digit7, DoomKey.Num7),
        (Key.Digit8, DoomKey.Num8),
        (Key.Digit9, DoomKey.Num9),
        (Key.Semicolon, DoomKey.Semicolon),
        (Key.NumpadEquals, DoomKey.Equal),
        (Key.A, DoomKey.A),
        (Key.B, DoomKey.B),
        (Key.C, DoomKey.C),
        (Key.D, DoomKey.D),
        (Key.E, DoomKey.E),
        (Key.F, DoomKey.F),
        (Key.G, DoomKey.G),
        (Key.H, DoomKey.H),
        (Key.I, DoomKey.I),
        (Key.J, DoomKey.J),
        (Key.K, DoomKey.K),
        (Key.L, DoomKey.L),
        (Key.M, DoomKey.M),
        (Key.N, DoomKey.N),
        (Key.O, DoomKey.O),
        (Key.P, DoomKey.P),
        (Key.Q, DoomKey.Q),
        (Key.R, DoomKey.R),
        (Key.S, DoomKey.S),
        (Key.T, DoomKey.T),
        (Key.U, DoomKey.U),
        (Key.V, DoomKey.V),
        (Key.W, DoomKey.W),
        (Key.X, DoomKey.X),
        (Key.Y, DoomKey.Y),
        (Key.Z, DoomKey.Z),
        (Key.LeftBracket, DoomKey.LBracket),
        (Key.Backslash, DoomKey.Backslash),
        (Key.RightBracket, DoomKey.RBracket),
        (Key.Escape, DoomKey.Escape),
        (Key.Enter, DoomKey.Enter),
        (Key.Tab, DoomKey.Tab),
        (Key.Backspace, DoomKey.Backspace),
        (Key.Insert, DoomKey.Insert),
        (Key.Delete, DoomKey.Delete),
        (Key.RightArrow, DoomKey.Right),
        (Key.LeftArrow, DoomKey.Left),
        (Key.DownArrow, DoomKey.Down),
        (Key.UpArrow, DoomKey.Up),
        (Key.PageUp, DoomKey.PageUp),
        (Key.PageDown, DoomKey.PageDown),
        (Key.Home, DoomKey.Home),
        (Key.End, DoomKey.End),
        (Key.Pause, DoomKey.Pause),
        (Key.F1, DoomKey.F1),
        (Key.F2, DoomKey.F2),
        (Key.F3, DoomKey.F3),
        (Key.F4, DoomKey.F4),
        (Key.F5, DoomKey.F5),
        (Key.F6, DoomKey.F6),
        (Key.F7, DoomKey.F7),
        (Key.F8, DoomKey.F8),
        (Key.F9, DoomKey.F9),
        (Key.F10, DoomKey.F10),
        (Key.F11, DoomKey.F11),
        (Key.F12, DoomKey.F12),
        (Key.Numpad0, DoomKey.Numpad0),
        (Key.Numpad1, DoomKey.Numpad1),
        (Key.Numpad2, DoomKey.Numpad2),
        (Key.Numpad3, DoomKey.Numpad3),
        (Key.Numpad4, DoomKey.Numpad4),
        (Key.Numpad5, DoomKey.Numpad5),
        (Key.Numpad6, DoomKey.Numpad6),
        (Key.Numpad7, DoomKey.Numpad7),
        (Key.Numpad8, DoomKey.Numpad8),
        (Key.Numpad9, DoomKey.Numpad9),
        (Key.NumpadDivide, DoomKey.Divide),
        (Key.NumpadMultiply, DoomKey.Multiply),
        (Key.NumpadMinus, DoomKey.Subtract),
        (Key.NumpadPlus, DoomKey.Add),
        (Key.NumpadEnter, DoomKey.Enter),
        (Key.LeftShift, DoomKey.LShift),
        (Key.LeftCtrl, DoomKey.LControl),
        (Key.LeftAlt, DoomKey.LAlt),
        (Key.RightShift, DoomKey.RShift),
        (Key.RightCtrl, DoomKey.RControl),
        (Key.RightAlt, DoomKey.RAlt),
        (Key.ContextMenu, DoomKey.Menu)];

    public static IEnumerable<(ButtonControl Control, DoomMouseButton Button)> MouseButtons(Mouse mouse) => [
        (mouse.leftButton, DoomMouseButton.Mouse1),
        (mouse.rightButton, DoomMouseButton.Mouse2),
        (mouse.middleButton, DoomMouseButton.Mouse3),
        (mouse.backButton, DoomMouseButton.Mouse4),
        (mouse.forwardButton,DoomMouseButton.Mouse5)];
}
