using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Menu: ROYGBIV > Build Skeleton.
    /// Generates placeholder sprites, ColorData / GameConfig assets, prefabs, all scenes and Build Settings.
    /// Safe to re-run: anything that already exists is left untouched, so it never overwrites teammates' work.
    /// (Delete an asset/scene and re-run to regenerate just that one.)
    /// </summary>
    public static class SkeletonBuilder
    {
        const string Root = "Assets/_Project";
        const string Art = Root + "/Art/Placeholder";
        const string DataColors = Root + "/Data/Colors";
        const string DataDialogue = Root + "/Data/Dialogue";
        const string Prefabs = Root + "/Prefabs";
        const string Scenes = Root + "/Scenes";
        const string ResourcesDir = Root + "/Resources";

        static readonly ColorId[] PlayOrder =
            { ColorId.Yellow, ColorId.Orange, ColorId.Red, ColorId.Green, ColorId.Blue, ColorId.Indigo, ColorId.Violet };

        struct ColorSpec
        {
            public string name, emotion, boss;
            public Color tint;
            public AbilityId ability;
            public int bossHp;
            public Type bossType;
        }

        static readonly Dictionary<ColorId, ColorSpec> Specs = new()
        {
            [ColorId.Yellow] = new ColorSpec { name = "Yellow", emotion = "Warmth, joy, happiness", boss = "Sun-Construct", tint = new Color(1f, .85f, .15f), ability = AbilityId.LightShot, bossHp = 11, bossType = typeof(YellowBoss) },
            [ColorId.Orange] = new ColorSpec { name = "Orange", emotion = "Excitement, enthusiasm, youth", boss = "Orange Runner", tint = new Color(1f, .55f, .1f), ability = AbilityId.Dash, bossHp = 3, bossType = typeof(OrangeBoss) },
            [ColorId.Red] = new ColorSpec { name = "Red", emotion = "Hot-blooded, anger", boss = "Red Rager", tint = new Color(.9f, .15f, .15f), ability = AbilityId.BlazeStrike, bossHp = 12, bossType = typeof(RedBoss) },
            [ColorId.Green] = new ColorSpec { name = "Green", emotion = "Envy, disgust", boss = "Bramble Thief", tint = new Color(.2f, .75f, .3f), ability = AbilityId.None, bossHp = 12, bossType = typeof(GreenBoss) },
            [ColorId.Blue] = new ColorSpec { name = "Blue", emotion = "Loneliness, sadness, melancholy", boss = "Rising Gloom", tint = new Color(.2f, .4f, .95f), ability = AbilityId.HeavySlam, bossHp = 1, bossType = typeof(BlueBoss) },
            [ColorId.Indigo] = new ColorSpec { name = "Indigo", emotion = "Calm, perceptiveness, spirituality", boss = "Indigo Seer", tint = new Color(.3f, .2f, .65f), ability = AbilityId.None, bossHp = 12, bossType = typeof(IndigoBoss) },
            [ColorId.Violet] = new ColorSpec { name = "Violet", emotion = "Royalty, wisdom, creativity", boss = "Violet Sovereign", tint = new Color(.6f, .3f, .85f), ability = AbilityId.None, bossHp = 15, bossType = typeof(VioletBoss) },
        };

        static Sprite square, circle;
        static PhysicsMaterial2D noFriction;

        [MenuItem("ROYGBIV/Build Skeleton (scenes, prefabs, data)")]
        public static void Build()
        {
            foreach (var dir in new[] { Art, DataColors, DataDialogue, Prefabs + "/Bosses", Scenes, ResourcesDir, Root + "/Audio" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            square = MakeSprite("Square", false);
            circle = MakeSprite("Circle", true);
            noFriction = LoadOrCreate($"{Art}/NoFriction.physicsMaterial2D", () => new PhysicsMaterial2D { friction = 0f, bounciness = 0f });

            var colors = MakeColorData();
            MakeGameConfig(colors);

            var playerShot = MakeProjectile("Projectile_PlayerShot", new Color(1f, .95f, .6f), 0.35f, 14f, false);
            var enemyOrb = MakeProjectile("Projectile_EnemyOrb", new Color(.85f, .85f, .85f), 0.6f, 5f, true);
            var player = MakePlayer(playerShot);
            var bosses = new Dictionary<ColorId, GameObject>();
            foreach (var id in PlayOrder) bosses[id] = MakeBoss(id, enemyOrb);
            MakeOrangeAttacks(); // the Orange boss prefab picks these up when it's first created

            MakeMenuScene("MainMenu", typeof(MainMenuScreen));
            MakeMenuScene("Hub", typeof(HubScreen));
            MakeMenuScene("Ending", typeof(EndingScreen), LoadOrCreate($"{DataDialogue}/Dialogue_Ending.asset", () => Dialogue("Scientist", "[Ending — writer TODO]")));
            foreach (var id in PlayOrder) MakeLevelScene(id, colors[id], player, bosses[id]);
            MakeSandbox(player);

            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ROYGBIV] Skeleton built. Open Assets/_Project/Scenes/MainMenu and press Play.");
        }

        /// <summary>For batch mode: Unity -batchmode -quit -executeMethod Roygbiv.EditorTools.SkeletonBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build();
        }

        // ------------------------------------------------------------------ data

        static Dictionary<ColorId, ColorData> MakeColorData()
        {
            var result = new Dictionary<ColorId, ColorData>();
            for (int i = 0; i < PlayOrder.Length; i++)
            {
                var id = PlayOrder[i];
                var s = Specs[id];
                result[id] = LoadOrCreate($"{DataColors}/Color_{i + 1}_{s.name}.asset", () =>
                {
                    var d = ScriptableObject.CreateInstance<ColorData>();
                    d.id = id;
                    d.displayName = s.name;
                    d.tint = s.tint;
                    d.emotion = s.emotion;
                    d.sceneName = "Level_" + s.name;
                    d.grantedAbility = s.ability;
                    d.storyFragment = LoadOrCreate($"{DataDialogue}/Story_{s.name}.asset",
                        () => Dialogue("Memory", $"[Story fragment after reclaiming {s.name} — writer TODO]"));
                    return d;
                });
            }
            return result;
        }

        static void MakeGameConfig(Dictionary<ColorId, ColorData> colors)
        {
            LoadOrCreate($"{ResourcesDir}/GameConfig.asset", () =>
            {
                var c = ScriptableObject.CreateInstance<GameConfig>();
                foreach (var id in PlayOrder) c.colorOrder.Add(colors[id]);
                return c;
            });
        }

        static DialogueData Dialogue(string speaker, string text)
        {
            var d = ScriptableObject.CreateInstance<DialogueData>();
            d.lines.Add(new DialogueLine { speaker = speaker, text = text });
            return d;
        }

        // ------------------------------------------------------------------ prefabs

        static Projectile MakeProjectile(string name, Color color, float size, float speed, bool reflectable, Action<Projectile> configure = null)
        {
            return LoadOrCreatePrefab($"{Prefabs}/{name}.prefab", () =>
            {
                var go = new GameObject(name);
                go.transform.localScale = Vector3.one * size;
                go.AddComponent<SpriteRenderer>().sprite = circle;
                go.GetComponent<SpriteRenderer>().color = color;
                go.GetComponent<SpriteRenderer>().sortingOrder = 5;
                go.AddComponent<CircleCollider2D>().isTrigger = true;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var p = go.AddComponent<Projectile>();
                p.speed = speed;
                p.reflectable = reflectable;
                configure?.Invoke(p);
                return go;
            }).GetComponent<Projectile>();
        }

        static GameObject MakePlayer(Projectile shot)
        {
            return LoadOrCreatePrefab($"{Prefabs}/Player.prefab", () =>
            {
                var go = new GameObject("Player");
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.8f, 1.2f);
                col.sharedMaterial = noFriction;

                var health = go.AddComponent<Health>();
                Set(health, "team", Team.Player);
                Set(health, "maxHealth", 5);
                Set(health, "hitInvulnerability", 1f);

                go.AddComponent<PlayerMotor>();
                var combat = go.AddComponent<PlayerCombat>();
                Set(go.AddComponent<AbilityLoadout>(), "syncWithProgress", true);
                go.AddComponent<PlayerController>();

                Visual(go.transform, square, new Color(.9f, .9f, .95f), new Vector2(0.8f, 1.2f), 10);

                var melee = MakeHitbox(go.transform, "MeleeHitbox", new Vector2(0.9f, 0f), new Vector2(1f, 1f), Team.Player, 1, true);
                Set(combat, "meleeHitbox", melee);

                var abilities = new GameObject("Abilities").transform;
                abilities.SetParent(go.transform, false);
                AddAbilities(abilities, shot, Team.Player, AbilityId.LightShot, AbilityId.Dash, AbilityId.BlazeStrike, AbilityId.HeavySlam);
                return go;
            });
        }

        static void AddAbilities(Transform parent, Projectile shot, Team team, params AbilityId[] ids)
        {
            var go = parent.gameObject;
            foreach (var id in ids)
            {
                switch (id)
                {
                    case AbilityId.LightShot:
                        Set(go.AddComponent<LightShotAbility>(), "projectilePrefab", shot);
                        break;
                    case AbilityId.Dash:
                        go.AddComponent<DashAbility>();
                        break;
                    case AbilityId.BlazeStrike:
                        var blaze = MakeHitbox(parent, "BlazeHitbox", new Vector2(1.1f, 0f), new Vector2(1.6f, 1.2f), team, 3, false);
                        Set(go.AddComponent<BlazeStrikeAbility>(), "strikeHitbox", blaze);
                        break;
                    case AbilityId.HeavySlam:
                        var slam = MakeHitbox(parent, "SlamHitbox", new Vector2(0f, -0.6f), new Vector2(2.5f, 0.8f), team, 2, false);
                        Set(go.AddComponent<HeavySlamAbility>(), "landingHitbox", slam);
                        break;
                }
            }
        }

        static GameObject MakeBoss(ColorId id, Projectile orb)
        {
            var s = Specs[id];
            return LoadOrCreatePrefab($"{Prefabs}/Bosses/Boss_{s.name}.prefab", () =>
            {
                var go = new GameObject("Boss_" + s.name);
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2f, 2f);

                var health = go.AddComponent<Health>();
                Set(health, "team", Team.Enemy);
                Set(health, "maxHealth", s.bossHp);
                Set(health, "hitInvulnerability", 0.2f);

                var visual = Visual(go.transform, square, s.tint, new Vector2(2f, 2f), 8);
                Set(visual.gameObject.AddComponent<Recolorable>(), "color", id);
                go.AddComponent<HitFeedback>();

                if (id == ColorId.Blue) // rising hazard: a wide kill-trigger under the player
                {
                    col.isTrigger = true;
                    col.size = new Vector2(30f, 1f);
                    visual.localScale = new Vector3(30f, 1f, 1f);
                }

                if (id == ColorId.Green) // the thief needs its own copies of the abilities it steals
                {
                    go.AddComponent<AbilityLoadout>();
                    var abilities = new GameObject("StolenAbilities").transform;
                    abilities.SetParent(go.transform, false);
                    AddAbilities(abilities, orb, Team.Enemy, AbilityId.LightShot, AbilityId.Dash, AbilityId.BlazeStrike);
                }

                var boss = (BossBase)go.AddComponent(s.bossType);
                Set(boss, "color", id);
                Set(boss, "displayName", s.boss);
                foreach (var field in new[] { "orbPrefab", "firePrefab", "projectilePrefab" })
                    if (new SerializedObject(boss).FindProperty(field) != null) Set(boss, field, orb);
                if (id == ColorId.Orange) SetUpOrangeBoss((OrangeBoss)boss);
                if (id == ColorId.Red) SetUpRedBoss((RedBoss)boss);
                return go;
            });
        }

        static Hitbox MakeHitbox(Transform parent, string name, Vector2 localPos, Vector2 size, Team team, int damage, bool reflects)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<BoxCollider2D>().size = size;
            go.GetComponent<BoxCollider2D>().isTrigger = true;
            var h = go.AddComponent<Hitbox>();
            h.team = team;
            h.damage = damage;
            h.reflectsProjectiles = reflects;
            go.SetActive(false); // Hitbox.Open() turns it on
            return h;
        }

        static Transform Visual(Transform parent, Sprite sprite, Color color, Vector2 size, int order)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        // ------------------------------------------------------------------ scenes

        static void MakeMenuScene(string name, Type screen, DialogueData endingDialogue = null)
        {
            var path = $"{Scenes}/{name}.unity";
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            var ui = new GameObject(name + "UI");
            var comp = ui.AddComponent(screen);
            if (endingDialogue) Set(comp, "endingDialogue", endingDialogue);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void MakeLevelScene(ColorId id, ColorData data, GameObject playerPrefab, GameObject bossPrefab)
        {
            var path = $"{Scenes}/{data.sceneName}.unity";
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera();
            var tint = data.tint;

            var level = new GameObject("LevelController").AddComponent<LevelController>();
            Set(level, "color", id);

            var env = new GameObject("Environment").transform;
            Block(env, "KillZone", new Vector2(0, -15), new Vector2(400, 2), Color.clear, null, true)
                .gameObject.AddComponent<LevelTrigger>().SetKillPlayer();

            var player = Instance(playerPrefab, new Vector2(-12, -1.5f));
            var boss = Instance(bossPrefab, new Vector2(12, -1));
            Set(level, "boss", boss.GetComponent<BossBase>());

            switch (id)
            {
                case ColorId.Orange: // auto-scrolling chase on an endless, generated track
                    cam.GetComponent<CameraFollow>().autoScrollSpeed = 6f; // ChaseDirector takes over at runtime
                    boss.transform.position = new Vector3(-3, -1.5f, 0); // OrangeBoss positions itself on fight start
                    MakeOrangeChase(tint);
                    break;

                case ColorId.Blue: // rising platformer, finish at the top
                    Block(env, "Ground", new Vector2(0, -3), new Vector2(40, 1), tint, id);
                    for (int i = 0; i < 12; i++)
                        Block(env, "Ledge", new Vector2(i % 2 == 0 ? -4 : 4, i * 3f), new Vector2(5, 0.5f), tint, id);
                    Block(env, "Wall", new Vector2(-12, 15), new Vector2(1, 40), tint, id);
                    Block(env, "Wall", new Vector2(12, 15), new Vector2(1, 40), tint, id);
                    player.transform.position = new Vector3(0, -1.5f, 0);
                    boss.transform.position = new Vector3(0, -8f, 0);
                    Block(env, "Goal (CompleteLevel)", new Vector2(0, 37), new Vector2(6, 2), Color.white, id, true)
                        .gameObject.AddComponent<LevelTrigger>().SetAction(LevelTrigger.Action.CompleteLevel);
                    break;

                default: // arena
                    Block(env, "Ground", new Vector2(0, -3), new Vector2(40, 1), tint, id);
                    Block(env, "Wall", new Vector2(-20, 3), new Vector2(1, 12), tint, id);
                    Block(env, "Wall", new Vector2(20, 3), new Vector2(1, 12), tint, id);
                    Block(env, "Platform", new Vector2(-6, 0.5f), new Vector2(4, 0.5f), tint, id);
                    Block(env, "Platform", new Vector2(6, 0.5f), new Vector2(4, 0.5f), tint, id);
                    break;
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        static void MakeSandbox(GameObject playerPrefab)
        {
            var path = $"{Scenes}/Sandbox.unity";
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            var env = new GameObject("Environment").transform;
            Block(env, "Ground", new Vector2(0, -3), new Vector2(60, 1), Color.gray, null);
            Block(env, "Platform", new Vector2(-5, 0.5f), new Vector2(4, 0.5f), Color.gray, null);
            Block(env, "Platform", new Vector2(3, 2.5f), new Vector2(4, 0.5f), Color.gray, null);
            Instance(playerPrefab, new Vector2(-8, -1.5f));

            var dummy = Block(env, "TrainingDummy", new Vector2(8, -1.5f), new Vector2(1, 2), new Color(.8f, .4f, .4f), null);
            dummy.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var h = dummy.gameObject.AddComponent<Health>();
            Set(h, "maxHealth", 999);
            EditorSceneManager.SaveScene(scene, path);
        }

        static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.07f, .07f, .09f);
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraFollow>();
            return cam;
        }

        /// <summary>Solid placeholder block: sprite + collider (+ Recolorable when it belongs to a color).</summary>
        static Transform Block(Transform parent, string name, Vector2 pos, Vector2 size, Color color, ColorId? recolor, bool trigger = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = trigger;
            if (!trigger) col.sharedMaterial = noFriction;
            if (recolor.HasValue) Set(go.AddComponent<Recolorable>(), "color", recolor.Value);
            return go.transform;
        }

        static GameObject Instance(GameObject prefab, Vector2 pos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            return go;
        }

        static void SetBuildScenes()
        {
            var names = new List<string> { "MainMenu", "Hub" };
            foreach (var id in PlayOrder) names.Add("Level_" + Specs[id].name);
            names.Add("Ending");
            names.Add("Sandbox");
            var list = new List<EditorBuildSettingsScene>();
            foreach (var n in names) list.Add(new EditorBuildSettingsScene($"{Scenes}/{n}.unity", true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ Orange chase

        /// <summary>
        /// Upgrades an Orange level made before the chase rework: removes the fixed ground, bumps and stun
        /// switches, adds the ChaseDirector + ChaseCourse, and wires the boss prefab's attacks.
        /// Safe to re-run.
        /// </summary>
        [MenuItem("ROYGBIV/Upgrade Orange Chase (Level_Orange)")]
        public static void UpgradeOrangeChase()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            square = MakeSprite("Square", false);
            circle = MakeSprite("Circle", true); // the firecracker / barrel prefabs need it
            noFriction = LoadOrCreate($"{Art}/NoFriction.physicsMaterial2D", () => new PhysicsMaterial2D { friction = 0f, bounciness = 0f });

            var bossPath = $"{Prefabs}/Bosses/Boss_Orange.prefab";
            var bossRoot = PrefabUtility.LoadPrefabContents(bossPath);
            SetUpOrangeBoss(bossRoot.GetComponent<OrangeBoss>());
            PrefabUtility.SaveAsPrefabAsset(bossRoot, bossPath);
            PrefabUtility.UnloadPrefabContents(bossRoot);

            var scene = EditorSceneManager.OpenScene($"{Scenes}/Level_Orange.unity");
            var stale = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.parent && t.parent.name == "Environment" && (t.name == "Ground" || t.name == "Bump" || t.GetComponent<ShootableSwitch>()))
                    stale.Add(t.gameObject);
            foreach (var go in stale) Object.DestroyImmediate(go);

            if (!Object.FindAnyObjectByType<ChaseDirector>(FindObjectsInactive.Include))
                MakeOrangeChase(Specs[ColorId.Orange].tint);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ROYGBIV] Orange chase upgraded: removed {stale.Count} old course objects.");
        }

        static void MakeOrangeAttacks()
        {
            // Both can be shot down, or punched back (they home onto the boss, which shrugs them off).
            MakeProjectile("Projectile_Firecracker", new Color(1f, .45f, .1f), 0.45f, 8f, true, p =>
            {
                p.shootable = true;
                p.reflectHomesOnShooter = true;
            });
            MakeProjectile("Projectile_Barrel", new Color(.6f, .35f, .15f), 0.9f, 3f, true, p =>
            {
                p.shootable = true;
                p.reflectHomesOnShooter = true;
                p.destroyOnWorld = false; // rolls along the ground, through obstacles
                p.lifetime = 8f;
            });
        }

        static void SetUpOrangeBoss(OrangeBoss boss)
        {
            MakeOrangeAttacks();
            Set(boss, "firecrackerPrefab", AssetDatabase.LoadAssetAtPath<Projectile>($"{Prefabs}/Projectile_Firecracker.prefab"));
            Set(boss, "barrelPrefab", AssetDatabase.LoadAssetAtPath<Projectile>($"{Prefabs}/Projectile_Barrel.prefab"));
            // 3 HP = 3 catches, one phase per catch. 2/3 and 1/3 sit just ABOVE the default 0.66 / 0.33.
            SetFloats(boss, "phaseThresholds", 0.7f, 0.4f);
        }

        static void SetUpRedBoss(RedBoss boss)
        {
            // Fire is dodged, not punched back.
            Set(boss, "firePrefab", MakeProjectile("Projectile_Fireball", new Color(1f, .4f, .08f), 0.55f, 9f, false));

            // The procedural animation squashes and leans a Pose whose origin is the feet, so it reads as weight.
            var visual = boss.transform.Find("Visual");
            var pose = new GameObject("Pose").transform;
            pose.SetParent(boss.transform, false);
            pose.localPosition = new Vector3(0f, -1f, 0f);
            visual.SetParent(pose, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);
            Set(boss, "pose", pose);
            Set(boss.GetComponent<HitFeedback>(), "visual", visual); // no longer a direct child, so it can't find it by name

            // 12 HP, 4 hits per overheat: one phase per overheat. 8/12 and 4/12 sit just BELOW 0.67 / 0.34.
            SetFloats(boss, "phaseThresholds", 0.67f, 0.34f);
        }

        static void MakeOrangeChase(Color tint)
        {
            var chase = new GameObject("Chase");
            chase.AddComponent<ChaseDirector>();
            var course = chase.AddComponent<ChaseCourse>();

            var template = new GameObject("BlockTemplate (copied by ChaseCourse)");
            template.transform.SetParent(chase.transform, false);
            var sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = tint;
            Set(template.AddComponent<Recolorable>(), "color", ColorId.Orange);
            template.SetActive(false);

            Set(course, "blockTemplate", template);
            Set(course, "solidMaterial", noFriction);
        }

        // ------------------------------------------------------------------ helpers

        static Sprite MakeSprite(string name, bool round)
        {
            var path = $"{Art}/{name}.png";
            if (!File.Exists(path))
            {
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool inside = !round || Vector2.Distance(new Vector2(x, y), center) <= size / 2f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) return existing;
            var asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject LoadOrCreatePrefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var go = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Set a (possibly private [SerializeField]) field through Unity's serialization.</summary>
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'"); return; }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case bool b: p.boolValue = b; break;
                case Enum e: p.intValue = Convert.ToInt32(e); break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                default: throw new ArgumentException($"Unsupported type {value?.GetType()}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloats(Object target, string field, params float[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"{target.GetType().Name} has no serialized array '{field}'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetKillPlayer(this LevelTrigger t) => t.SetAction(LevelTrigger.Action.KillPlayer);
        static void SetAction(this LevelTrigger t, LevelTrigger.Action action) => Set(t, "action", action);
    }
}
