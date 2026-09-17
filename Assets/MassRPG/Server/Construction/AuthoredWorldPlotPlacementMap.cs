using System;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Server.Construction
{
    /// <summary>
    /// Authoritative player-plot placement view over loaded authored terrain plus overlapping
    /// semantic protection layers. Water/deep water and explicit tile NoBuild flags are rejected by
    /// the page store; semantic NoBuild areas additionally protect POIs, roads/corridors or any
    /// other hand-authored region without baking that protection into every tile.
    /// </summary>
    public sealed class AuthoredWorldPlotPlacementMap : IPlotPlacementMap
    {
        private readonly AuthoredWorldPageStore _world;
        private readonly WorldSemanticCatalog _semantics;

        public AuthoredWorldPlotPlacementMap(AuthoredWorldPageStore world, WorldSemanticCatalog semantics)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _semantics = semantics ?? throw new ArgumentNullException(nameof(semantics));
        }

        public bool CanReserveForPlayerPlot(GridLocation location)
        {
            if (location.Storey != 0) return false;
            if (!_world.IsBuildable(location)) return false;

            foreach (var area in _semantics.Areas)
            {
                if (area.Kind != WorldAreaKind.NoBuild) continue;
                if (area.Plane != location.Plane) continue;
                if (area.Shape.Contains(location.Tile)) return false;
            }

            foreach (var poi in _semantics.PointsOfInterest)
            {
                if (poi.Center.Plane != location.Plane) continue;
                if (poi.ProtectionFootprint != null && poi.ProtectionFootprint.Contains(location.Tile)) return false;
            }

            return true;
        }
    }
}
