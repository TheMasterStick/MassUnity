using System;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;

namespace MassRPG.Core.Characters
{
    public enum CombatStyle
    {
        Melee,
        Ranged,
        Magic
    }

    public enum MeleeTrainingStyle
    {
        Accurate,
        Aggressive,
        Defensive,
        Controlled
    }

    /// <summary>
    /// Engine-independent player simulation state. Unity presentation must not be stored here.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(Guid characterId, string name, int inventoryCapacity = InventoryState.DefaultCapacity)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            CharacterId = characterId;
            Name = string.IsNullOrWhiteSpace(name) ? "Adventurer" : name;
            Skills = new SkillSet();
            Inventory = new InventoryState(inventoryCapacity);
            Equipment = new EquipmentState();
            Movement = new MovementState();
            Combat = new CombatState();
            Production = new ProductionState();
            CurrentHitpoints = MaxHitpoints;
            CombatStyle = CombatStyle.Melee;
            MeleeTrainingStyle = MeleeTrainingStyle.Aggressive;
            Tile = new GridCoord(WorldConstants.WorldWidthTiles / 2, WorldConstants.WorldHeightTiles / 2);
            Plane = WorldConstants.SurfacePlane;
            Storey = 0;
        }

        public Guid CharacterId { get; }
        public string Name { get; set; }
        public SkillSet Skills { get; }
        public InventoryState Inventory { get; }
        public EquipmentState Equipment { get; }
        public MovementState Movement { get; }
        public CombatState Combat { get; }
        public ProductionState Production { get; }
        public int CurrentHitpoints { get; set; }
        public CombatStyle CombatStyle { get; set; }
        public MeleeTrainingStyle MeleeTrainingStyle { get; set; }

        /// <summary>
        /// Ranged ammunition is deliberately not an equipment slot in the final MassRPG loadout.
        /// This optional id records the player's selected ammunition stack while the arrows remain
        /// ordinary inventory items consumed by authoritative combat.
        /// </summary>
        public ContentId? SelectedAmmunitionItemId { get; set; }

        public GridCoord Tile { get; set; }
        public int Plane { get; set; }
        public int Storey { get; set; }

        public GridLocation Location
        {
            get => new GridLocation(Tile, Plane, Storey);
            set
            {
                Tile = value.Tile;
                Plane = value.Plane;
                Storey = value.Storey;
            }
        }

        public int MaxHitpoints => Skills.GetLevel(SkillId.Hitpoints);
        public int CombatLevel => CombatLevelCalculator.Calculate(Skills);
        public bool IsAlive => CurrentHitpoints > 0;
    }
}
