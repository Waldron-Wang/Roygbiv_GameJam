using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// ORANGE chase: the one owner of the scroll speed. Every frame it pushes the speed into the camera
    /// (CameraFollow.autoScrollSpeed) and the player (PlayerMotor.autoRunSpeed), so nobody has to keep
    /// three numbers equal by hand. The boss and the course read Speed / ScrollX from here.
    ///
    /// Speed ramps up at a fixed rate over the whole fight (Chrome dino style), whatever the boss phase.
    ///
    /// Fairness:
    ///  - Catch-up: behind their home spot (CameraFollow.autoScrollLead left of center) the player runs a bit
    ///    faster than the scroll, so clipping an obstacle costs a moment, not the run.
    ///  - A warm glow on the left edge grows as the player nears the kill line (CameraFollow.leftBehindGrace).
    ///  - Falling into a pit kills (the course is endless, so no KillZone sits under all of it).
    /// </summary>
    [DefaultExecutionOrder(-50)] // set this frame's speed before the boss / course read it
    public class ChaseDirector : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] float startSpeed = 6f;
        [SerializeField] float maxSpeed = 11f;
        [Tooltip("Added per second of fight.")]
        [SerializeField] float rampPerSecond = 0.04f;
        [Tooltip("Once the boss is beaten, the chase brakes to a stop over this many seconds.")]
        [SerializeField] float stopTime = 1.5f;

        [Header("Catch-up")]
        [Tooltip("Extra run speed per unit the player is behind their home spot.")]
        [SerializeField] float catchUpGain = 1.5f;
        [SerializeField] float maxCatchUp = 2.5f;
        [Tooltip("Ahead of home the player eases back by up to this much, so the gap to the boss stays readable.")]
        [SerializeField] float maxEaseBack = 1f;

        [Header("Fairness")]
        [Tooltip("World Y of the ground's top surface.")]
        [SerializeField] float groundY = -2.5f;
        [Tooltip("Falling this far below the ground kills.")]
        [SerializeField] float fallDeathDepth = 6f;
        [Tooltip("The edge glow starts this many units before the kill line.")]
        [SerializeField] float warnDistance = 4f;
        [SerializeField] float warnWidth = 1.5f;
        [SerializeField] Color warnColor = new(1f, 0.35f, 0.1f, 0.6f);

        CameraFollow cam;
        PlayerMotor motor;
        BossBase boss;
        SpriteRenderer warnGlow;
        float fightTime, brake;

        // "Stopped" is a crawl, not 0: at 0 the camera and the motor would drop out of auto-scroll mode
        // (the camera would swing back onto the player, the player would get their controls back).
        const float StoppedSpeed = 0.001f;

        public static ChaseDirector Current { get; private set; }
        public float Speed { get; private set; }
        public float GroundY => groundY;
        public float ScrollX => cam ? cam.ScrollX : transform.position.x;
        public float HalfWidth => cam ? cam.HalfWidth : 12f;
        public float LeftEdgeX => ScrollX - HalfWidth;
        public float RightEdgeX => ScrollX + HalfWidth;
        /// <summary>Where the player sits when they're keeping pace.</summary>
        public float HomeX => ScrollX - (cam ? cam.autoScrollLead : 4f);

        void Awake()
        {
            Current = this;
            Speed = startSpeed;
            cam = FindAnyObjectByType<CameraFollow>();
            // Before CameraFollow.Start, so it snaps into auto-scroll framing.
            if (cam) cam.autoScrollSpeed = Speed;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            if (PlayerController.Instance) motor = PlayerController.Instance.GetComponent<PlayerMotor>();
            if (LevelController.Current) boss = LevelController.Current.Boss;
            if (cam) warnGlow = FlatSprite.Create("LeftEdgeWarning", cam.transform, Vector2.zero, Vector2.one, Color.clear, 100);
        }

        void Update()
        {
            if (boss && boss.IsFighting) fightTime += Time.deltaTime;
            if (boss && boss.Health.IsDead)
            {
                if (brake <= 0f) brake = Speed / Mathf.Max(0.01f, stopTime); // constant braking from the speed at the win
                Speed = Mathf.MoveTowards(Speed, StoppedSpeed, brake * Time.deltaTime);
            }
            else Speed = Mathf.Min(maxSpeed, startSpeed + rampPerSecond * fightTime);
            if (cam) cam.autoScrollSpeed = Speed;

            var player = PlayerController.Instance;
            if (!player || player.Health.IsDead) { SetWarning(0f); return; }

            var p = player.transform.position;
            if (motor)
            {
                float behind = HomeX - p.x;
                float catchUp = Mathf.Clamp(behind * catchUpGain, -maxEaseBack, maxCatchUp);
                motor.autoRunSpeed = Mathf.Max(StoppedSpeed, Speed + catchUp);
            }

            if (p.y < groundY - fallDeathDepth) player.Health.Kill();

            float danger = cam ? 1f - (p.x - cam.KillLineX) / warnDistance : 0f;
            SetWarning(Mathf.Clamp01(danger));
        }

        void SetWarning(float danger)
        {
            if (!warnGlow || !cam) return;
            float halfHeight = cam.GetComponent<Camera>().orthographicSize;
            warnGlow.transform.localPosition = new Vector3(-cam.HalfWidth + warnWidth * 0.5f, 0f, 10f);
            warnGlow.transform.localScale = new Vector3(warnWidth, halfHeight * 2f, 1f);
            float pulse = danger > 0.6f ? 0.8f + 0.2f * Mathf.Sin(Time.time * 14f) : 1f;
            var c = warnColor;
            c.a *= danger * pulse;
            warnGlow.color = c;
        }
    }
}
