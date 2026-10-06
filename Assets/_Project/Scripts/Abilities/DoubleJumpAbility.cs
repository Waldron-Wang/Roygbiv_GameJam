using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Extra mid-air jump(s). Press jump while airborne to hop again.
    /// Does its own ground check from the body's contacts, so it doesn't depend on the player controller.
    /// </summary>
    public class DoubleJumpAbility : AbilityBase
    {
        [Tooltip("Upward speed of the extra jump.")]
        [SerializeField] float jumpVelocity = 12f;
        [Tooltip("1 = double jump, 2 = triple jump, etc.")]
        [SerializeField] int extraJumps = 1;
        [Tooltip("A contact counts as ground if its normal points up at least this much.")]
        [SerializeField, Range(0f, 1f)] float groundNormalY = 0.7f;

        public override AbilityId Id => AbilityId.DoubleJump;

        int jumpsLeft;
        readonly List<ContactPoint2D> contacts = new();
        ContactFilter2D filter;
        bool filterReady;

        public override bool HandleInput(in PlayerIntent intent)
        {
            if (!intent.jumpPressed) return false;
            if (Owner == null || IsGrounded()) return false; // the normal jump handles this press
            if (jumpsLeft <= 0) return false;
            return TryActivate();
        }

        protected override void Activate()
        {
            jumpsLeft--;
            var body = Owner.Body;
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
        }

        void Update()
        {
            if (Owner != null && IsGrounded())
                jumpsLeft = extraJumps;
        }

        bool IsGrounded()
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D();
                filter.NoFilter();
                filter.useTriggers = false;
                filterReady = true;
            }

            int n = Owner.Body.GetContacts(filter, contacts);
            for (int i = 0; i < n; i++)
                if (contacts[i].normal.y >= groundNormalY) return true;
            return false;
        }
    }
}