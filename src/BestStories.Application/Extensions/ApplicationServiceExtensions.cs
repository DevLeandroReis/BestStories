using BestStories.Application.Settings;
using BestStories.Application.UseCases;
using BestStories.Application.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BestStories.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HackerNewsApplicationSettings>(configuration.GetSection("HackerNews"));
        services.AddScoped<GetBestStoriesUseCase>();
        services.AddSingleton<GetBestStoriesValidator>();

        return services;
    }
}
