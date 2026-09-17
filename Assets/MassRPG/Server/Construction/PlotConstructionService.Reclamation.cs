using System;

namespace MassRPG.Server.Construction
{
    public sealed partial class PlotConstructionService
    {
        /// <summary>
        /// Removes the active modular-building state for a plot after the authoritative plot
        /// registry has decided the land may be reclaimed. This is intentionally internal: normal
        /// gameplay demolition must continue to use permission/material rules rather than bypassing
        /// them through a public "delete building" API.
        /// </summary>
        internal bool RemoveReclaimedPlotState(Guid plotId)
            => _buildings.Remove(plotId);
    }
}
