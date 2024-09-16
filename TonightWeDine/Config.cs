using BepInEx.Configuration;

namespace TonightWeDine;

internal class TonightWeDineConfig
{
    internal enum DineLayoutEnum
    {
        V60 = -1,
        V50,
        Hybrid
    }

    internal static ConfigEntry<DineLayoutEnum> dineLayout;

    public static void Bind(ConfigFile config)
    {
        dineLayout = config.Bind(
            "7-Dine",
            "Layout",
            DineLayoutEnum.V50,
            "Dine's Exterior Layout\n\n" +
            "V60 = V60's exterior\n\n" +
            "V50 = V50's exterior\n\n" +
            "Hybird = V50's exterior with V49's entrances"
        );
    }
}
