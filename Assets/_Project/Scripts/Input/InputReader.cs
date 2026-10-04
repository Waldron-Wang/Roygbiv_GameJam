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
    ///   Jump    Z / Space / A(south)
    ///   Attack  X / X(west)           (hold + release = Blaze Strike)
    ///   Shoot   Left Click / C / Y(north)   (Light Shot; aims at the mouse unless fired from a gamepad)
    ///   Dash    Left Shift / RB
    ///   Pause   Esc / Start
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputReader : MonoBehaviour
    {
        public PlayerIntent Intent { get; private set; }

        readonly List<IInputModifier> modifiers = new();
        int gameplayBlockers;

        InputAction move, jump, attack, shoot, dash, confirm, pause;

        /// <summary>False while dialogue, pause, cutscenes etc. hold a block.</summary>
        public bool GameplayEnabled => gameplayBlockers == 0;

        /// <summary>Each Block must be paired with exactly one Unblock.</summary>
        public void BlockGameplay() => gameplayBlockers++;
        public void UnblockGameplay() => gameplayBlockers = Mathf.Max(0, gameplayBlockers - 1);

        public void AddModifier(IInputModifier m) { if (!modifiers.Contains(m)) modifiers.Add(m); }
        public void RemoveModifier(IInputModifier m) => modifiers.Remove(m);
        public void ClearModifiers() => modifiers.Clear();

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

            jump = Button("Jump", "<Keyboard>/z", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            attack = Button("Attack", "<Keyboard>/x", "<Gamepad>/buttonWest");
            shoot = Button("Shoot", "<Mouse>/leftButton", "<Keyboard>/c", "<Gamepad>/buttonNorth");
            dash = Button("Dash", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
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
                confirmPressed = confirm.WasPressedThisFrame(),
                pausePressed = pause.WasPressedThisFrame(),
            };
            ReadMouseAim(ref i);

            if (!GameplayEnabled) i.ClearGameplay();
            foreach (var m in modifiers) i = m.Modify(i);
            Intent = i;
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
            yield return dash; yield return confirm; yield return pause;
        }
    }
}
