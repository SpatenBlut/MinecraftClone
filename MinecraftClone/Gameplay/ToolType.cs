namespace MinecraftClone.Gameplay;

public enum ToolType
{
    None,
    WoodenPickaxe,
    StonePickaxe,
    IronPickaxe,
    GoldPickaxe,
    DiamondPickaxe,
    WoodenAxe,
    StoneAxe,
    IronAxe,
    GoldAxe,
    DiamondAxe,
    WoodenShovel,
    StoneShovel,
    IronShovel,
    GoldShovel,
    DiamondShovel,
}

public static class ToolTypeExtensions
{
    public static ToolCategory Category(this ToolType t) => t switch
    {
        ToolType.WoodenPickaxe  or ToolType.StonePickaxe  or
        ToolType.IronPickaxe   or ToolType.GoldPickaxe    or
        ToolType.DiamondPickaxe                              => ToolCategory.Pickaxe,

        ToolType.WoodenAxe     or ToolType.StoneAxe       or
        ToolType.IronAxe       or ToolType.GoldAxe         or
        ToolType.DiamondAxe                                  => ToolCategory.Axe,

        ToolType.WoodenShovel  or ToolType.StoneShovel    or
        ToolType.IronShovel    or ToolType.GoldShovel      or
        ToolType.DiamondShovel                               => ToolCategory.Shovel,

        _                                                    => ToolCategory.None,
    };
}

public enum ToolCategory { None, Pickaxe, Axe, Shovel }
