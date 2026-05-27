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
}
