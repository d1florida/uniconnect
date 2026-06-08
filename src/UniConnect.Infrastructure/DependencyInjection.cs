using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Auth;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Infrastructure.Security;
using UniConnect.Infrastructure.Services;
using UniConnect.Delivery.Interfaces;
using UniConnect.GeneralFleet.Interfaces;
using UniConnect.Insights.Interfaces;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.Tenant.Interfaces;
using UniConnect.RoboTaxi.Interfaces;
using UniConnect.Infrastructure.Services.Insights;
using UniConnect.Infrastructure.Services.RoutePlanning;
using UniConnect.RoutePlanning.Options;

namespace UniConnect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment = null)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    RoleClaimType = "role",
                    NameClaimType = JwtRegisteredClaimNames.Name
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantApiKeyService, TenantApiKeyService>();
        services.AddScoped<ITenantUserService, TenantUserService>();
        services.AddScoped<IGeneralFleetService, GeneralFleetService>();
        services.AddScoped<IRoboTaxiService, RoboTaxiService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IOperationalEventRecorder, OperationalEventRecorder>();
        services.AddScoped<ICustomerDirectory, CustomerDirectory>();
        services.AddScoped<IDriverDirectory, DriverDirectory>();
        services.AddScoped<IDriverScheduleRequestService, DriverScheduleRequestService>();
        services.AddScoped<IDriverScheduleResolver, DriverScheduleResolver>();
        services.AddScoped<TenantDeliverySettingsService>();
        services.AddScoped<ITenantDeliverySettingsService>(sp => sp.GetRequiredService<TenantDeliverySettingsService>());
        services.AddScoped<IDepotDirectory, DepotDirectory>();
        services.AddScoped<IFixedRouteDirectory, FixedRouteDirectory>();
        services.AddScoped<IInsightsService, InsightsService>();
        services.Configure<RoutePlanningOptions>(configuration.GetSection(RoutePlanningOptions.SectionName));
        services.Configure<PlanningRulesOptions>(configuration.GetSection(PlanningRulesOptions.SectionName));
        services.AddHttpClient(nameof(GeocodingService), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<RoutePlanningOptions>>().Value;
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.NominatimUserAgent);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient(GeocodingService.CensusClientName, client =>
        {
            client.BaseAddress = new Uri("https://geocoding.geo.census.gov/geocoder/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        var dataProtection = services.AddDataProtection().SetApplicationName("UniConnect");
        if (hostEnvironment is not null)
        {
            var keysPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "data-protection-keys");
            Directory.CreateDirectory(keysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<ITenantSecretProtector, TenantSecretProtector>();
        services.AddScoped<TenantGeocodingConfigProvider>();
        services.AddScoped<ITenantGeocodingConfigProvider>(sp => sp.GetRequiredService<TenantGeocodingConfigProvider>());
        services.AddScoped<TenantGeocodingSettingsService>();
        services.AddScoped<ITenantGeocodingSettingsService>(sp => sp.GetRequiredService<TenantGeocodingSettingsService>());
        services.AddHttpClient(GeocodingService.GoogleClientName, client =>
        {
            client.BaseAddress = new Uri("https://maps.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IGeocodingService, GeocodingService>();
        services.AddHttpClient(OsrmTravelMatrix.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<RoutePlanningOptions>>().Value;
            var baseUrl = opts.OsrmBaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<HaversineTravelMatrix>();
        services.AddScoped<OsrmTravelMatrix>();
        services.AddScoped<ITravelMatrixBuilder, TravelMatrixBuilder>();
        services.AddScoped<ITravelTimeMatrix>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RoutePlanningOptions>>().Value;
            return opts.TravelTimeProvider.Equals("Osrm", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<OsrmTravelMatrix>()
                : sp.GetRequiredService<HaversineTravelMatrix>();
        });
        services.AddScoped<OrderGeocodingHelper>();
        services.AddHttpClient(nameof(PlanningPolicyCompiler));
        services.AddHttpClient(nameof(PlanRunExplainer));
        services.AddScoped<IPlanningPolicyCompiler, PlanningPolicyCompiler>();
        services.AddScoped<IPlanningPolicyProvider, PlanningPolicyProvider>();
        services.AddScoped<IPlanRunExplainer, PlanRunExplainer>();
        services.AddScoped<TenantPlanningRulesService>();
        services.AddScoped<ITenantPlanningRulesService>(sp => sp.GetRequiredService<TenantPlanningRulesService>());
        services.AddScoped<IRoutePlanningService, RoutePlanningService>();

        return services;
    }
}
