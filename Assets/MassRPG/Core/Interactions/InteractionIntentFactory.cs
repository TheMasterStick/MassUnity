using System;
using MassRPG.Core.Authority;

namespace MassRPG.Core.Interactions
{
    /// <summary>
    /// Converts a logical interaction option into a transport-safe authoritative request without
    /// relying on presentation IDs or parsing string conventions. UI remains free to decide how an
    /// option is displayed; only typed target identity can become gameplay intent.
    /// </summary>
    public static class InteractionIntentFactory
    {
        public static bool TryCreateRequest(
            InteractionOption option,
            Guid requestId,
            Guid characterId,
            out GameRequest request,
            out string failureCode)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));

            request = null;
            failureCode = string.Empty;
            var target = option.Target;

            switch (option.Action)
            {
                case InteractionActionKind.WalkHere:
                    if (target.Kind != InteractionTargetKind.MovementTile)
                        return Fail("interaction_target_kind_mismatch", out failureCode);
                    request = new MoveToRequest(requestId, characterId, target.Location);
                    return true;

                case InteractionActionKind.Attack:
                    if (target.Kind != InteractionTargetKind.CombatCreature)
                        return Fail("interaction_target_kind_mismatch", out failureCode);
                    if (!target.InstanceId.HasValue)
                        return Fail("interaction_identity_missing", out failureCode);
                    request = new AttackCreatureRequest(requestId, characterId, target.InstanceId.Value);
                    return true;

                case InteractionActionKind.Gather:
                    if (target.Kind != InteractionTargetKind.Resource)
                        return Fail("interaction_target_kind_mismatch", out failureCode);
                    if (!target.ResourceNode.HasValue)
                        return Fail("interaction_identity_missing", out failureCode);
                    request = new GatherResourceRequest(requestId, characterId, target.ResourceNode.Value);
                    return true;

                case InteractionActionKind.Take:
                    if (target.Kind != InteractionTargetKind.GroundItem)
                        return Fail("interaction_target_kind_mismatch", out failureCode);
                    if (!target.InstanceId.HasValue)
                        return Fail("interaction_identity_missing", out failureCode);
                    request = new TakeGroundItemRequest(requestId, characterId, target.InstanceId.Value);
                    return true;

                default:
                    return Fail("unsupported_interaction_action", out failureCode);
            }
        }

        private static bool Fail(string code, out string failureCode)
        {
            failureCode = code;
            return false;
        }
    }
}
