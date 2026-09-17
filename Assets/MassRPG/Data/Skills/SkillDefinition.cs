using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Skills
{
    public enum SkillCategory
    {
        Combat,
        Gathering,
        Production,
        Support
    }

    public sealed class SkillDefinition
    {
        public SkillDefinition(ContentId id, SkillId skill, string displayName, SkillCategory category, int startingLevel)
        {
            Id = id;
            Skill = skill;
            DisplayName = displayName;
            Category = category;
            StartingLevel = startingLevel;
        }

        public ContentId Id { get; }
        public SkillId Skill { get; }
        public string DisplayName { get; }
        public SkillCategory Category { get; }
        public int StartingLevel { get; }
    }
}
