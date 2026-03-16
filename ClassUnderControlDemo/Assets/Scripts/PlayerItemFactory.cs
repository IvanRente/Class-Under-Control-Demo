public static class PlayerItemFactory
{
    public static PlayerInventoryItem Create(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
    {
        if (definition == null)
            return null;

        switch (definition.itemId)
        {
            case PlayerItemSystem.ItemId.Shield:
                return new Shield(itemSystem, definition);
            case PlayerItemSystem.ItemId.WaterPistol:
                return new WaterGun(itemSystem, definition);
            case PlayerItemSystem.ItemId.AirHorn:
                return new AirHorn(itemSystem, definition);
            default:
                return null;
        }
    }

    public static string GetDefaultDisplayName(PlayerItemSystem.ItemId itemId)
    {
        switch (itemId)
        {
            case PlayerItemSystem.ItemId.WaterPistol:
                return "Water Pistol";
            case PlayerItemSystem.ItemId.AirHorn:
                return "Air Horn";
            default:
                return "Shield";
        }
    }

    public static void ApplyDurabilityDefaults(PlayerItemDefinition definition, PlayerItemSystem.ItemId itemId)
    {
        if (definition.maxDurability < 0f)
            definition.maxDurability = itemId == PlayerItemSystem.ItemId.WaterPistol ? 10f : 100f;

        if (definition.durabilityCostPerUse < 0f)
        {
            switch (itemId)
            {
                case PlayerItemSystem.ItemId.Shield:
                    definition.durabilityCostPerUse = 10f;
                    break;
                case PlayerItemSystem.ItemId.AirHorn:
                    definition.durabilityCostPerUse = 25f;
                    break;
                default:
                    definition.durabilityCostPerUse = 0f;
                    break;
            }
        }

        if (definition.durabilityCostPerSecond < 0f)
            definition.durabilityCostPerSecond = itemId == PlayerItemSystem.ItemId.WaterPistol ? 1f : 0f;
    }
}
