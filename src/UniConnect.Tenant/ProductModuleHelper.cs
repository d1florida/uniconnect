using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant;

public static class ProductModuleHelper
{
    public static ProductModule Combine(IEnumerable<ProductModule> modules)
    {
        var combined = ProductModule.None;
        foreach (var module in modules)
        {
            if (module == ProductModule.None) continue;
            combined |= module;
        }
        return combined;
    }

    public static IReadOnlyList<ProductModule> Expand(ProductModule modules) =>
        Enum.GetValues<ProductModule>()
            .Where(m => m != ProductModule.None && modules.HasFlag(m))
            .ToList();

    public static bool HasModule(ProductModule modules, ProductModule module) =>
        module != ProductModule.None && modules.HasFlag(module);

    public static ProductModule EffectiveModules(ProductModule userAccess, ProductModule tenantModules) =>
        userAccess & tenantModules;

    public static void ValidateTenantModules(ProductModule modules)
    {
        if (HasModule(modules, ProductModule.RoutePlanning) && !HasModule(modules, ProductModule.Delivery))
            throw new ArgumentException("Route planning requires the Delivery module.");
        if (HasModule(modules, ProductModule.Insights) && !HasModule(modules, ProductModule.Delivery))
            throw new ArgumentException("Insights requires the Delivery module.");
    }
}
