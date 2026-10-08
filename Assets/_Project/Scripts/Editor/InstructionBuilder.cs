using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Instructions.
    /// Creates Data/Instructions/Instruction_&lt;Color&gt;.asset for the seven colors (caption, keys, demo, accent,
    /// and the demo's sprites from Art/Player, Art/Boss and Art/Placeholder), then points each ColorData's
    /// `instruction` at its card if that field is empty: that's what gives the level its Tip button. Cards that
    /// already exist are left alone, so Inspector tweaks survive a re-run; ROYGBIV > Reset Instructions to Defaults
    /// rewrites them (it asks first). Each card's `nudge` is the vague first hint that keeps the Tip from spoiling a
    /// first try (see InstructionData); Yellow (the parry tutorial) and Violet have none, so theirs are open from the start. Green, Blue, Indigo and Violet draw their bosses in code (DemoBosses), so their
    /// cards carry only the player's frames.
    /// </summary>
    public static class InstructionBuilder
    {
        const string Dir = "Assets/_Project/Data/Instructions";
        const string ColorsDir = "Assets/_Project/Data/Colors";
        const string PlayerArt = "Assets/_Project/Art/Player";
        const string BossArt = "Assets/_Project/Art/Boss";
        const string Circle = "Assets/_Project/Art/Placeholder/Circle.png";

        struct Card
        {
            public ColorId color;
            public string accent, caption;
            public InstructionDemo demo;
            public string[] keys;
            public string loop, action;     // Art/Player subfolders
            public float loopFps, actionFps;
            public string run, jump, hurt;  // more Art/Player subfolders (the later demos)
            public string boss, bossHurt;   // Art/Boss file names
            public string[] bossMove;
            public bool dontLink;           // make the asset, but no Tip button for this color
            public string nudge;            // vague first hint (no demo); empty = the full card from the start
            public bool duringFight;        // no Tip button before the boss fight starts (Yellow's intro level)
        }

        // Keys match InputReader: attack = Left Click (hold + release = Blaze Strike), Light Shot = Right Click or C,
        // jump = Space, dash = Left Shift (+ Down = Down Dash), Serenity = Q.
        static readonly Card[] Cards =
        {
            new()
            {
                duringFight = true,
                color = ColorId.Yellow, accent = "#FFD93B", demo = InstructionDemo.Reflect,
                caption = "Press [LMB] to reflect the <color=#FFD93B>light orbs</color> back at it",
                keys = new[] { "LMB" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                boss = "yellowIdle", bossHurt = "yellowHurt",
            },
            new()
            {
                nudge = "You'll never outrun it. Look up: something there can <color=#FF9A2E>hold it still</color>",
                color = ColorId.Orange, accent = "#FF9A2E", demo = InstructionDemo.ShootLatch,
                caption = "Shoot the <color=#FF9A2E>latch</color> [RMB] / [C] to trap it, then catch it 3 times",
                keys = new[] { "RMB", "C" },
                loop = "run", loopFps = 12f,
                boss = "orangeIdle", bossHurt = "orangeHurt", bossMove = new[] { "orangeRun1", "orangeRun2" },
            },
            new()
            {
                nudge = "Its <color=#FF4A3D>rage</color> can't burn forever. Push it past its limit",
                color = ColorId.Red, accent = "#FF4A3D", demo = InstructionDemo.Overheat,
                caption = "Hit it until it <color=#FF4A3D>overheats</color>, then strike!",
                keys = new[] { "LMB" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                boss = "redIdle", bossHurt = "redHurt",
            },
            new()
            {
                // Steal: it takes the ability you used last (keys[0]) into a pod; break it up close (keys[1]).
                nudge = "What it takes, it keeps close. So <color=#4CD964>get close</color>",
                color = ColorId.Green, accent = "#4CD964", demo = InstructionDemo.Steal,
                caption = "Break the <color=#4CD964>pods</color> up close to take your abilities back",
                keys = new[] { "Shift", "LMB" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                run = "run", jump = "jump", hurt = "Hurt",
            },
            new()
            {
                // Climb: hop up the ledges, a tear knocks you down, the water rises; catch it at the summit.
                nudge = "Waiting only lets the water rise. <color=#3D8BFF>Keep climbing</color>",
                color = ColorId.Blue, accent = "#3D8BFF", demo = InstructionDemo.Climb,
                caption = "Climb before the <color=#3D8BFF>tears</color> flood the shaft, then catch it at the top",
                keys = new[] { "Space" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                run = "run", jump = "jump", hurt = "Hurt",
            },
            new()
            {
                // FlipControls: the slots are left, right, jump, attack, shoot, dash; each curse trades their keys.
                nudge = "When your hands lie, the <color=#7B6CFF>sigil</color> tells the truth",
                color = ColorId.Indigo, accent = "#7B6CFF", demo = InstructionDemo.FlipControls,
                caption = "Each curse <color=#7B6CFF>scrambles</color> your controls. Read the sigil",
                keys = new[] { "Left", "Right", "Space", "LMB", "RMB", "Shift" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                run = "run", jump = "jump", hurt = "Hurt",
            },
            new()
            {
                // Gauntlet: dash, double jump, Down + dash (surf), shoot, hold attack (Blaze Strike), Serenity.
                color = ColorId.Violet, accent = "#C266FF", demo = InstructionDemo.Gauntlet,
                caption = "Use <color=#C266FF>every ability</color> to reach the king",
                keys = new[] { "Shift", "Space", "Down", "RMB", "LMB", "Q" },
                loop = "idle", loopFps = 8f, action = "attack", actionFps = 20f,
                run = "run", jump = "jump", hurt = "Hurt",
            },
        };

        [MenuItem("ROYGBIV/Build Instructions")]
        public static void Build() => Run(overwrite: false);

        [MenuItem("ROYGBIV/Reset Instructions to Defaults")]
        public static void ResetToDefaults()
        {
            if (EditorUtility.DisplayDialog("Reset instruction cards",
                    "Rewrite all seven Instruction_<Color> assets with the default captions, keys and sprites? " +
                    "Changes made to them in the Inspector are lost.", "Reset", "Cancel"))
                Run(overwrite: true);
        }

        static void Run(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Instructions");

            int created = 0, reset = 0, linked = 0;
            foreach (var card in Cards)
            {
                string path = $"{Dir}/Instruction_{card.color}.asset";
                var data = AssetDatabase.LoadAssetAtPath<InstructionData>(path);
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<InstructionData>();
                    Fill(data, card);
                    AssetDatabase.CreateAsset(data, path);
                    created++;
                }
                else if (overwrite)
                {
                    Fill(data, card);
                    EditorUtility.SetDirty(data);
                    reset++;
                }

                if (card.dontLink) continue;
                var colorData = FindColor(card.color);
                if (!colorData)
                {
                    Debug.LogWarning($"[InstructionBuilder] No ColorData for {card.color} in {ColorsDir}; {path} isn't linked.");
                    continue;
                }
                if (!colorData.instruction)
                {
                    colorData.instruction = data;
                    EditorUtility.SetDirty(colorData);
                    linked++;
                }
                else if (colorData.instruction != data)
                    Debug.Log($"[InstructionBuilder] {colorData.name} already uses {colorData.instruction.name}; left as is.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[InstructionBuilder] Cards created: {created}, reset: {reset}. ColorData linked: {linked}.");
        }

        static void Fill(InstructionData d, Card c)
        {
            d.caption = c.caption;
            d.subCaption = ""; // cards are demo + one caption; the field stays for a designer who wants a second line
            if (!ColorUtility.TryParseHtmlString(c.accent, out d.accent)) d.accent = Color.white;
            d.demo = c.demo;
            d.keys = c.keys ?? new string[0];
            d.playerLoop = Frames(c.loop);
            d.playerLoopFps = c.loopFps > 0f ? c.loopFps : 8f;
            d.playerAction = Frames(c.action);
            d.playerActionFps = c.actionFps > 0f ? c.actionFps : 20f;
            d.playerRun = Frames(c.run);
            d.playerRunFps = 12f;
            d.playerJump = Frames(c.jump);
            d.playerJumpFps = 15f;
            d.playerHurt = Frames(c.hurt).FirstOrDefault();
            d.boss = BossSprite(c.boss);
            d.bossHurt = BossSprite(c.bossHurt);
            d.bossMove = (c.bossMove ?? new string[0]).Select(BossSprite).Where(s => s).ToArray();
            d.prop = AssetDatabase.LoadAssetAtPath<Sprite>(Circle);
            // No spoilers: nudge after the first death in the fight, the full card after the third. No nudge = open from the start.
            d.nudge = c.nudge ?? "";
            bool gated = !string.IsNullOrEmpty(c.nudge);
            d.nudgeAfterDeaths = gated ? 1 : 0;
            d.demoAfterDeaths = gated ? 3 : 0;
            d.onlyDuringBossFight = c.duringFight;
        }

        /// <summary>Every sprite in Art/Player/&lt;folder&gt;, sorted by file name (same as PlayerAnimationBuilder).</summary>
        static Sprite[] Frames(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return new Sprite[0];
            string path = $"{PlayerArt}/{folder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogWarning($"[InstructionBuilder] Missing {path}; the card falls back to a stick figure.");
                return new Sprite[0];
            }
            return AssetDatabase.FindAssets("t:Sprite", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s)
                .ToArray();
        }

        static Sprite BossSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{BossArt}/{name}.png");
            if (!sprite) Debug.LogWarning($"[InstructionBuilder] Missing sprite {BossArt}/{name}.png.");
            return sprite;
        }

        static ColorData FindColor(ColorId id) =>
            AssetDatabase.FindAssets("t:ColorData", new[] { ColorsDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<ColorData>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(c => c && c.id == id);
    }
}
