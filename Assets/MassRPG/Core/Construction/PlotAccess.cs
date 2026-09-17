using System;

namespace MassRPG.Core.Construction
{
    public enum PlotTier
    {
        Small,
        Medium,
        Large
    }

    [Flags]
    public enum PlotPermission
    {
        None = 0,
        Enter = 1 << 0,
        UseOuterGate = 1 << 1,
        UseDoors = 1 << 2,
        UseContainers = 1 << 3,
        Farm = 1 << 4,
        Build = 1 << 5,
        Demolish = 1 << 6,
        ManageAccess = 1 << 7,
        Full = Enter | UseOuterGate | UseDoors | UseContainers | Farm | Build | Demolish | ManageAccess
    }

    /// <summary>
    /// Reusable owner-defined permission preset (for example "Farmhand" or "Gate + house").
    /// Named rulesets avoid arbitrary member caps and keep per-player assignments compact.
    /// </summary>
    public sealed class PlotAccessRuleSet
    {
        public PlotAccessRuleSet(Guid ruleSetId, string name, PlotPermission permissions)
        {
            if (ruleSetId == Guid.Empty) throw new ArgumentException("Ruleset id cannot be empty.", nameof(ruleSetId));
            RuleSetId = ruleSetId;
            Name = string.IsNullOrWhiteSpace(name) ? "Access" : name;
            Permissions = permissions;
        }

        public Guid RuleSetId { get; }
        public string Name { get; set; }
        public PlotPermission Permissions { get; set; }
    }
}
