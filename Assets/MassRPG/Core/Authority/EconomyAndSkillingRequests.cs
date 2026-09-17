using System;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;
using MassRPG.Core.World;

namespace MassRPG.Core.Authority
{
    public sealed class DepositBankItemRequest : GameRequest
    {
        public DepositBankItemRequest(Guid requestId, Guid characterId, GridLocation bankLocation, int inventorySlot, int quantity)
            : base(requestId, characterId)
        {
            BankLocation = bankLocation;
            InventorySlot = inventorySlot;
            Quantity = quantity;
        }
        public GridLocation BankLocation { get; }
        public int InventorySlot { get; }
        public int Quantity { get; }
    }

    public sealed class WithdrawBankItemRequest : GameRequest
    {
        public WithdrawBankItemRequest(Guid requestId, Guid characterId, GridLocation bankLocation, ContentId itemId, int quantity)
            : base(requestId, characterId)
        {
            BankLocation = bankLocation;
            ItemId = itemId;
            Quantity = quantity;
        }
        public GridLocation BankLocation { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public sealed class BuyShopItemRequest : GameRequest
    {
        public BuyShopItemRequest(Guid requestId, Guid characterId, ContentId shopId, GridLocation shopLocation, ContentId itemId, int quantity)
            : base(requestId, characterId)
        {
            ShopId = shopId;
            ShopLocation = shopLocation;
            ItemId = itemId;
            Quantity = quantity;
        }
        public ContentId ShopId { get; }
        public GridLocation ShopLocation { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public sealed class SellShopItemRequest : GameRequest
    {
        public SellShopItemRequest(Guid requestId, Guid characterId, ContentId shopId, GridLocation shopLocation, int inventorySlot, int quantity)
            : base(requestId, characterId)
        {
            ShopId = shopId;
            ShopLocation = shopLocation;
            InventorySlot = inventorySlot;
            Quantity = quantity;
        }
        public ContentId ShopId { get; }
        public GridLocation ShopLocation { get; }
        public int InventorySlot { get; }
        public int Quantity { get; }
    }

    public sealed class PlantCropRequest : GameRequest
    {
        public PlantCropRequest(Guid requestId, Guid characterId, ResourceNodeKey patch, ContentId cropId)
            : base(requestId, characterId)
        {
            Patch = patch;
            CropId = cropId;
        }
        public ResourceNodeKey Patch { get; }
        public ContentId CropId { get; }
    }

    public sealed class HarvestCropRequest : GameRequest
    {
        public HarvestCropRequest(Guid requestId, Guid characterId, ResourceNodeKey patch)
            : base(requestId, characterId)
        {
            Patch = patch;
        }
        public ResourceNodeKey Patch { get; }
    }

    public sealed class LightFireRequest : GameRequest
    {
        public LightFireRequest(Guid requestId, Guid characterId, ContentId logItemId)
            : base(requestId, characterId)
        {
            LogItemId = logItemId;
        }
        public ContentId LogItemId { get; }
    }

    public sealed class EatFoodRequest : GameRequest
    {
        public EatFoodRequest(Guid requestId, Guid characterId, int inventorySlot)
            : base(requestId, characterId)
        {
            InventorySlot = inventorySlot;
        }
        public int InventorySlot { get; }
    }

    /// <summary>
    /// Client intent to drink the potion occupying one inventory slot. The client does not submit
    /// effect magnitudes, durations or skill changes; authority resolves all of those from item data.
    /// </summary>
    public sealed class DrinkPotionRequest : GameRequest
    {
        public DrinkPotionRequest(Guid requestId, Guid characterId, int inventorySlot)
            : base(requestId, characterId)
        {
            InventorySlot = inventorySlot;
        }
        public int InventorySlot { get; }
    }

    public sealed class DropInventoryItemRequest : GameRequest
    {
        public DropInventoryItemRequest(Guid requestId, Guid characterId, int inventorySlot, int quantity)
            : base(requestId, characterId)
        {
            InventorySlot = inventorySlot;
            Quantity = quantity;
        }
        public int InventorySlot { get; }
        public int Quantity { get; }
    }

    public sealed class TakeGroundItemRequest : GameRequest
    {
        public TakeGroundItemRequest(Guid requestId, Guid characterId, Guid groundItemId)
            : base(requestId, characterId)
        {
            GroundItemId = groundItemId;
        }
        public Guid GroundItemId { get; }
    }
}
