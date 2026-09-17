using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Skills
{
    public static class SkillCatalog
    {
        public static readonly IReadOnlyList<SkillDefinition> All = new[]
        {
            Def("skill.hitpoints", SkillId.Hitpoints, "Hitpoints", SkillCategory.Combat, 10),
            Def("skill.attack", SkillId.Attack, "Attack", SkillCategory.Combat, 1),
            Def("skill.strength", SkillId.Strength, "Strength", SkillCategory.Combat, 1),
            Def("skill.defence", SkillId.Defence, "Defence", SkillCategory.Combat, 1),
            Def("skill.ranged", SkillId.Ranged, "Ranged", SkillCategory.Combat, 1),
            Def("skill.magic", SkillId.Magic, "Magic", SkillCategory.Combat, 1),
            Def("skill.woodcutting", SkillId.Woodcutting, "Woodcutting", SkillCategory.Gathering, 1),
            Def("skill.mining", SkillId.Mining, "Mining", SkillCategory.Gathering, 1),
            Def("skill.fishing", SkillId.Fishing, "Fishing", SkillCategory.Gathering, 1),
            Def("skill.farming", SkillId.Farming, "Farming", SkillCategory.Gathering, 1),
            Def("skill.cooking", SkillId.Cooking, "Cooking", SkillCategory.Production, 1),
            Def("skill.firemaking", SkillId.Firemaking, "Firemaking", SkillCategory.Production, 1),
            Def("skill.smithing", SkillId.Smithing, "Smithing", SkillCategory.Production, 1),
            Def("skill.crafting", SkillId.Crafting, "Crafting", SkillCategory.Production, 1),
            Def("skill.fletching", SkillId.Fletching, "Fletching", SkillCategory.Production, 1),
            Def("skill.herblore", SkillId.Herblore, "Herblore", SkillCategory.Production, 1),
            Def("skill.construction", SkillId.Construction, "Construction", SkillCategory.Support, 1),
            Def("skill.agility", SkillId.Agility, "Agility", SkillCategory.Support, 1)
        };

        private static SkillDefinition Def(string id, SkillId skill, string name, SkillCategory category, int start)
            => new SkillDefinition(new ContentId(id), skill, name, category, start);
    }
}
