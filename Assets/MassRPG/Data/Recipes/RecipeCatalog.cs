using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Recipes
{
    public interface IRecipeDefinitionSource
    {
        bool TryGet(ContentId recipeId, out RecipeDefinition definition);
    }

    public sealed class RecipeCatalog : IRecipeDefinitionSource
    {
        private readonly Dictionary<ContentId, RecipeDefinition> _recipes = new Dictionary<ContentId, RecipeDefinition>();

        public IEnumerable<RecipeDefinition> All => _recipes.Values;
        public int Count => _recipes.Count;

        public void Register(RecipeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_recipes.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate recipe id '{definition.Id}'.");
            _recipes.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId recipeId, out RecipeDefinition definition)
            => _recipes.TryGetValue(recipeId, out definition);
    }
}
