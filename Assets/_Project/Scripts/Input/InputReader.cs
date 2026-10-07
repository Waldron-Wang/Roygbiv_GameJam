using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roygbiv
{
    /// <summary>
    /// Reads devices once per frame and publishes a PlayerIntent (Game.Input.Intent).
    /// Runs before every other script so the intent is fresh when gameplay reads it.
    ///
    /// Default bindings (change them here, nowhere else):
    ///   Move    WASD / Arrows / left stick
    ///   Jump    Space / A(south)
    ///   Attack  Left click / X(west)           (hold + release = Blaze Strike)
    ///   Shoot   Right Click / C / Y(north)   (Light Shot; aims at the mouse unless fired from a gamepad)
    ///   Dash    Left Shift / RB             (hold Down too = Down Dash)
    ///   Serenity Q / LB
    ///   Confirm Z / X / Enter / A(south)    (dialogue, cards, menus)
    ///   Pause   Esc / Start
    /// Menus read Navigate (the move keys, raw) and confirmPressed / pausePressed, which stay live while gameplay is blocked.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputReader : MonoBehaviour
    {
        public PlayerIntent Intent { get; private set; }

        /// <summary>
        /// The move keys / stick as they are, for menus (pause, main menu, hub): never cleared by a gameplay block and
        /// never scrambled by a curse. Gameplay reads Intent.move instead.
        /// </summary>
        public Vector2 Navigate { get; private set; }

        readonly List<IInputModifier> modifiers = new();
        readonly List<Func<Vector2, bool>> pointerBlockers = new();
        int gameplayBlockers;
        bool pointerCaptured;

        InputAction move, jump, attack, shoot, dash, serenity, confirm, pause;

        /// <summary>False while dialogue, pause, cutscenes etc. hold a block.</summary>
        public bool GameplayEnabled => gameplayBlockers == 0;

        /// <summary>Each Block must be paired with exactly one Unblock.</summary>
        public void BlockGameplay() => gameplayBlockers++;
        public void UnblockGameplay() => gameplayBlockers = Mathf.Max(0, gameplayBlockers - 1);

        public void AddModifier(IInputModifier m) { if (!modifiers.Contains(m)) modifiers.Add(m); }
        public void RemoveModifier(IInputModifier m) => modifiers.Remove(m);
        public void ClearModifiers() => modifiers.Clear();

        /// <summary>
        /// On-screen UI clicked during gameplay (the Tip button) registers a hit test here, in screen pixels with the
        /// origin bottom-left. A mouse press that starts over it never reaches gameplay: attack and shoot stay masked
        /// from that press until the button is let go, so clicking the UI doesn't also swing, charge or fire.
        /// (IMGUI sees the click only after gameplay's Update, too late to cancel it there.)
        /// </summary>
        public void AddPointerBlocker(Func<Vector2, bool> isOver) { if (!pointerBlockers.Contains(isOver)) pointerBlockers.Add(isOver); }
        public void RemovePointerBlocker(Func<Vector2, bool> isOver) => pointerBlockers.Remove(isOver);

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            attack = Button("Attack", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            shoot = Button("Shoot", "<Mouse>/rightButton", "<Keyboard>/c", "<Gamepad>/buttonNorth");
            dash = Button("Dash", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
            serenity = Button("Serenity", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            confirm = Button("Confirm", "<Keyboard>/enter", "<Keyboard>/z", "<Keyboard>/x", "<Gamepad>/buttonSouth");
            pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");

            foreach (var a in All()) a.Enable();
        }

        void OnDestroy()
        {
            foreach (var a in All()) a.Dispose();
        }

        void Update()
        {
            var i = new PlayerIntent
            {
                move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f),
                jumpPressed = jump.WasPressedThisFrame(),
                jumpHeld = jump.IsPressed(),
                attackPressed = attack.WasPressedThisFrame(),
                attackHeld = attack.IsPressed(),
                attackReleased = attack.WasReleasedThisFrame(),
                shootPressed = shoot.WasPressedThisFrame(),
                dashPressed = dash.WasPressedThisFrame(),
                serenityPressed = serenity.WasPressedThisFrame(),
                confirmPressed = confirm.WasPressedThisFrame(),
                pausePressed = pause.WasPressedThisFrame(),
            };
            Navigate = i.move;
            ReadMouseAim(ref i);
            MaskPointerOverUI(ref i);

            if (!GameplayEnabled) i.ClearGameplay();
            foreach (var m in modifiers) i = m.Modify(i);
            Intent = i;
        }

        void MaskPointerOverUI(ref PlayerIntent i)
        {
            var mouse = Mouse.current;
            if (mouse == null) { pointerCaptured = false; return; }

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                var at = mouse.position.ReadValue();
                foreach (var isOver in pointerBlockers)
                    if (isOver(at)) { pointerCaptured = true; break; }
            }
            if (!pointerCaptured) return;

            i.attackPressed = i.attackHeld = i.attackReleased = false;
            i.shootPressed = false;
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed) pointerCaptured = false; // released: masked this frame too
        }

        void ReadMouseAim(ref PlayerIntent i)
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null || shoot.activeControl?.device is Gamepad) return;

            var screen = mouse.position.ReadValue();
            i.aimPoint = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            i.hasAimPoint = true;
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }

        IEnumerable<InputAction> All()
        {
            yield return move; yield return jump; yield return attack; yield return shoot;
            yield return dash; yield return serenity; yield return confirm; yield return pause;
        }
    }
}
