using System;
using System.Collections.Generic;
using MassRPG.Core.Interactions;

namespace MassRPG.Client.Interactions
{
    /// <summary>
    /// Minimal first-playable option source. It only exposes gameplay actions that currently have
    /// typed authoritative request contracts. More specialized NPC/object/player providers can be
    /// composed later without making unsupported actions the left-click default in the meantime.
    /// </summary>
    public sealed class AuthoritativeInteractionOptionSource : IClientInteractionOptionSource
    {
        public void CollectOptions(InteractionTarget target, List<InteractionOption> options)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (options == null) throw new ArgumentNullException(nameof(options));

            switch (target.Kind)
            {
                case InteractionTargetKind.CombatCreature:
                    if (target.InstanceId.HasValue)
                        options.Add(new InteractionOption(
                            target,
                            InteractionActionKind.Attack,
                            "Attack " + target.DisplayName));
                    break;

                case InteractionTargetKind.Resource:
                    if (target.ResourceNode.HasValue)
                        options.Add(new InteractionOption(
                            target,
                            InteractionActionKind.Gather,
                            "Gather " + target.DisplayName));
                    break;

                case InteractionTargetKind.GroundItem:
                    if (target.InstanceId.HasValue)
                        options.Add(new InteractionOption(
                            target,
                            InteractionActionKind.Take,
                            "Take " + target.DisplayName));
                    break;
            }

            if (target.Kind != InteractionTargetKind.MovementTile)
                options.Add(new InteractionOption(
                    target,
                    InteractionActionKind.Examine,
                    "Examine " + target.DisplayName,
                    canBeDefaultAction: false));
        }
    }
}
