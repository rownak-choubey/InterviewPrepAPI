using System.Reflection;

namespace InterviewPrepAPI.Localization;

public static class LocalizationExtensions
{
    public static IServiceCollection AddAppLocalization(this IServiceCollection services)
    {
        services.AddLocalization();

        return services;
    }

    public static IApplicationBuilder UseAppLocalization(this IApplicationBuilder app)
    {
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en"),
            SupportedCultures = new[]
            {
                new System.Globalization.CultureInfo("en")
            },
            SupportedUICultures = new[]
            {
                new System.Globalization.CultureInfo("en")
            }
        });

        return app;
    }
}
