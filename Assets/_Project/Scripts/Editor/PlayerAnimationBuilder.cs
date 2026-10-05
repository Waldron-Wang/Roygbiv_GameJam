using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Player Animations.
    /// Turns the frame PNGs in Art/Player/{idle,run,attack,runAttack,jump,Hurt} (sorted by file name) into clips,
    /// builds the controller PlayerAnimator drives, and wires it onto Player.prefab.
    /// Safe to re-run after adding or replacing frames: clips and controller are rebuilt in place (same GUIDs).
    ///
    ///   Idle <-> Run            Speed
    ///   Any (grounded) -> Attack / RunAttack   Attack trigger, picked by Speed; plays once, then back to Idle / Run
    ///   Idle/Run/attacks -> Jump               !Grounded; Jump is scrubbed by JumpProgress, not played over time
    ///   Jump -> Land -> Idle    Grounded; Land is interrupted by running, jumping or attacking
    ///   Any -> Hurt             Hurt bool (held by PlayerAnimator after a hit); overrides everything, attacks included
    /// </summary>
    public static class PlayerAnimationBuilder
    {
        const string Dir = "Assets/_Project/Art/Player";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const float RunThreshold = 0.1f;

        // Jump art: 0 = take-off crouch (unused — the jump launches instantly), 1 = push-off, 2 = tuck/apex,
        // 3 = falling, 4 = landing crouch, 5 = recover.
        static readonly FrameRange JumpAirFrames = new(1, 3), JumpLandFrames = new(4, 2);

        static readonly EditorCurveBinding SpriteBinding =
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        readonly struct FrameRange
        {
            public readonly int start, count;
            public FrameRange(int start, int count) { this.start = start; this.count = count; }
        }

        [MenuItem("ROYGBIV/Build Player Animations")]
        public static void Build()
        {
            var idleFrames = LoadFrames("idle");
            var runFrames = LoadFrames("run");
            var attackFrames = LoadFrames("attack");
            var runAttackFrames = LoadFrames("runAttack");
            var jumpFrames = LoadFrames("jump");
            var hurtFrames = LoadFrames("Hurt");
            if (idleFrames == null || runFrames == null || attackFrames == null || runAttackFrames == null || jumpFrames == null || hurtFrames == null) return;
            if (jumpFrames.Length < JumpLandFrames.start + JumpLandFrames.count)
            {
                Debug.LogError($"[PlayerAnimationBuilder] Expected 6 jump frames, found {jumpFrames.Length}. Update JumpAirFrames / JumpLandFrames.");
                return;
            }

            var clips = new Clips
            {
                idle = BuildClip("Player_Idle", idleFrames, 8f, loop: true),
                run = BuildClip("Player_Run", runFrames, 12f, loop: true),
                attack = BuildClip("Player_Attack", attackFrames, 20f, loop: false),
                // Same leg cycle as Run, so same frame rate.
                runAttack = BuildClip("Player_runAttack", runAttackFrames, 12f, loop: false),
                jump = BuildClip("Player_Jump", Slice(jumpFrames, JumpAirFrames), 12f, loop: false),
                land = BuildClip("Player_Land", Slice(jumpFrames, JumpLandFrames), 12f, loop: false),
                // Held for as long as the Hurt bool is on, so it loops.
                hurt = BuildClip("Player_Hurt", hurtFrames, 12f, loop: true),
            };
            var controller = BuildController(clips);
            AssetDatabase.SaveAssets();

            if (WirePrefab(controller, idleFrames[0]))
                Debug.Log($"[PlayerAnimationBuilder] Built Idle/Run/Attack/RunAttack/Jump/Land/Hurt and wired {PlayerPrefab}.");
        }

        struct Clips { public AnimationClip idle, run, attack, runAttack, jump, land, hurt; }

        static Sprite[] LoadFrames(string subfolder)
        {
            string folder = $"{Dir}/{subfolder}";
            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s)
                .ToArray();
            if (frames.Length > 0) return frames;
            Debug.LogError($"[PlayerAnimationBuilder] No sprites in {folder}. Are the PNGs imported as Texture Type 'Sprite (2D and UI)'?");
            return null;
        }

        static Sprite[] Slice(Sprite[] frames, FrameRange r) => frames.Skip(r.start).Take(r.count).ToArray();

        static AnimationClip BuildClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = $"{Dir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, b, null);
            clip.frameRate = fps;

            // One key per frame, plus a repeat of the last frame so it gets its full duration.
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[Mathf.Min(i, frames.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController BuildController(Clips clips)
        {
            string path = Dir + "/Player.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (var p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("JumpProgress", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var s in sm.states) sm.RemoveState(s.state);

            var idle = AddState(sm, "Idle", clips.idle, 300, 0);
            var run = AddState(sm, "Run", clips.run, 300, 120);
            var attack = AddState(sm, "Attack", clips.attack, 600, 0);
            var runAttack = AddState(sm, "RunAttack", clips.runAttack, 600, 120);
            var jump = AddState(sm, "Jump", clips.jump, 0, 60);
            var land = AddState(sm, "Land", clips.land, 0, 200);
            var hurt = AddState(sm, "Hurt", clips.hurt, 300, 260);
            jump.timeParameterActive = true;
            jump.timeParameter = "JumpProgress";
            sm.defaultState = idle;

            // Idle <-> Run
            Transition(idle, run).AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");
            Transition(run, idle).AddCondition(AnimatorConditionMode.Less, RunThreshold, "Speed");

            // Hurt beats everything (added first, so it wins over an attack on the same frame).
            var toHurt = sm.AddAnyStateTransition(hurt);
            Configure(toHurt);
            toHurt.canTransitionToSelf = false;
            toHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
            var hurtToAir = Transition(hurt, jump);
            hurtToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Hurt");
            hurtToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var hurtToGround = Transition(hurt, idle);
            hurtToGround.AddCondition(AnimatorConditionMode.IfNot, 0f, "Hurt");
            hurtToGround.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            // Attacks: from any grounded state (including mid-swing, so mashing restarts the swing).
            foreach (var (target, mode) in new[] { (attack, AnimatorConditionMode.Less), (runAttack, AnimatorConditionMode.Greater) })
            {
                var t = sm.AddAnyStateTransition(target);
                Configure(t);
                t.canTransitionToSelf = true;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                t.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Hurt");
                t.AddCondition(mode, RunThreshold, "Speed");
            }
            ExitWhenDone(attack, idle);
            ExitWhenDone(runAttack, run);

            // Leaving the ground interrupts everything grounded.
            foreach (var from in new[] { idle, run, attack, runAttack, land })
                Transition(from, jump).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            // Landing: running straight out of a jump skips the landing crouch.
            var toRun = Transition(jump, run);
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            toRun.AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");
            var toLand = Transition(jump, land);
            toLand.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            toLand.AddCondition(AnimatorConditionMode.Less, RunThreshold, "Speed");
            Transition(land, run).AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");
            ExitWhenDone(land, idle);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion clip, float x, float y)
        {
            var state = sm.AddState(name, new Vector3(x, y));
            state.motion = clip;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            Configure(t);
            return t;
        }

        static void ExitWhenDone(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            Configure(t);
            t.hasExitTime = true;
            t.exitTime = 1f;
        }

        // Sprite animation: no blending, no waiting for the clip to finish unless asked.
        static void Configure(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.duration = 0f;
        }

        static bool WirePrefab(RuntimeAnimatorController controller, Sprite firstFrame)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var visual = root.transform.Find("Visual");
                if (!visual)
                {
                    Debug.LogError($"[PlayerAnimationBuilder] {PlayerPrefab} has no 'Visual' child.");
                    return false;
                }

                // Size now comes from the sprites' Pixels Per Unit, not the placeholder square's stretch.
                visual.localScale = Vector3.one;
                var sprite = visual.GetComponent<SpriteRenderer>();
                if (sprite) sprite.sprite = firstFrame;

                var animator = visual.GetComponent<Animator>();
                if (!animator) animator = visual.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                if (!root.GetComponent<PlayerAnimator>()) root.AddComponent<PlayerAnimator>();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
