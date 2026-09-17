using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;
using MassRPG.Core.World;

namespace MassRPG.Client.Skilling
{
    /// <summary>
    /// Client bridge for gathering, farming, firemaking and production intent. All skill checks,
    /// resource consumption, timings and resulting rewards remain authoritative.
    /// </summary>
    public sealed class SkillingActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public SkillingActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision Gather(ResourceNodeKey node)
            => _authority.Submit(new GatherResourceRequest(Guid.NewGuid(), _characterId, node));

        public AuthorityDecision Plant(ResourceNodeKey patch, ContentId cropId)
            => _authority.Submit(new PlantCropRequest(Guid.NewGuid(), _characterId, patch, cropId));

        public AuthorityDecision Harvest(ResourceNodeKey patch)
            => _authority.Submit(new HarvestCropRequest(Guid.NewGuid(), _characterId, patch));

        public AuthorityDecision LightFire(ContentId logItemId)
            => _authority.Submit(new LightFireRequest(Guid.NewGuid(), _characterId, logItemId));

        public AuthorityDecision StartProduction(ContentId recipeId, int quantity, GridLocation? stationLocation = null)
            => _authority.Submit(new StartProductionRequest(Guid.NewGuid(), _characterId, recipeId, quantity, stationLocation));

        public AuthorityDecision CancelProduction()
            => _authority.Submit(new CancelProductionRequest(Guid.NewGuid(), _characterId));
    }
}
