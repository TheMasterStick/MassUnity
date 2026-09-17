namespace MassRPG.Core.Creatures
{
    /// <summary>
    /// Settled baseline behavior categories. Passive never initiates combat; Neutral retaliates
    /// when attacked; Aggressive can acquire valid targets inside its authored aggro rules.
    /// </summary>
    public enum CreatureDisposition
    {
        Passive,
        Neutral,
        Aggressive
    }
}
