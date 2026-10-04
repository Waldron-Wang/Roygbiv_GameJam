using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Player Animations.
    /// Turns the frame PNGs in Art/Player/idle and Art/Player/run (sorted by file name) into looping clips,
    /// builds an Idle/Run controller driven by PlayerAnimator's "Speed" param, and wires it onto Player.prefab.
    /// Safe to re-run after adding or replacing frames: clips and controller are rebuilt in place (same GUIDs).
    /// </summary>
    public static class PlayerAnimationBuilder
    {
        const string Dir = "Assets/_Project/Art/Player";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const float IdleFps = 8f, RunFps = 12f;
        const float RunThreshold = 0.1f;

        static readonly EditorCurveBinding SpriteBinding =
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        [MenuItem("ROYGBIV/Build Player Animations")]
        public static void Build()
        {
            var idleFrames = LoadFrames(Dir + "/idle");
            var runFrames = LoadFrames(Dir + "/run");
            if (idleFrames.Length == 0 || runFrames.Length == 0) return;

            var idle = BuildClip("Player_Idle", idleFrames, IdleFps);
            var run = BuildClip("Player_Run", runFrames, RunFps);
            var controller = BuildController(idle, run);
            AssetDatabase.SaveAssets();

            if (WirePrefab(controller, idleFrames[0]))
                Debug.Log($"[PlayerAnimationBuilder] Built Idle ({idleFrames.Length} frames) + Run ({runFrames.Length} frames) and wired {PlayerPrefab}.");
        }

        static Sprite[] LoadFrames(string folder)
        {
            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s)
                .ToArray();
            if (frames.Length == 0)
                Debug.LogError($"[PlayerAnimationBuilder] No sprites in {folder}. Are the PNGs imported as Texture Type 'Sprite (2D and UI)'?");
            return frames;
        }

        static AnimationClip BuildClip(string name, Sprite[] frames, float fps)
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

            // One key per frame, plus a repeat of the last frame so it gets its full duration before looping.
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[Mathf.Min(i, frames.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController BuildController(AnimationClip idle, AnimationClip run)
        {
            string path = Dir + "/Player.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (var p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);

            var idleState = sm.AddState("Idle", new Vector3(300f, 0f));
            idleState.motion = idle;
            var runState = sm.AddState("Run", new Vector3(300f, 120f));
            runState.motion = run;
            sm.defaultState = idleState;

            AddTransition(idleState, runState, AnimatorConditionMode.Greater);
            AddTransition(runState, idleState, AnimatorConditionMode.Less);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(mode, RunThreshold, "Speed");
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
