using System;
using MassRPG.Core.Interactions;
using UnityEngine;

namespace MassRPG.Client.Interactions
{
    /// <summary>
    /// Presentation marker connecting one or more Unity colliders to a logical interaction target.
    /// It is configured from authoritative/presentation state at runtime; it does not invent target
    /// identity from a GameObject name, tag or collider instance.
    /// </summary>
    public sealed class InteractionTargetView : MonoBehaviour
    {
        public InteractionTarget LogicalTarget { get; private set; }
        public bool HasLogicalTarget => LogicalTarget != null;

        public void Configure(InteractionTarget target)
        {
            LogicalTarget = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void Clear() => LogicalTarget = null;
    }
}
