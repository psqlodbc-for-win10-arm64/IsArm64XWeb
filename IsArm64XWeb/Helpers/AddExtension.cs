using IsArm64XWeb.Usecases;
using Microsoft.Extensions.DependencyInjection;

namespace IsArm64XWeb.Helpers
{
    public static class AddExtension
    {
        public static IServiceCollection AddIsArm64XWeb(this IServiceCollection services)
        {
            services.AddSingleton<GenerateDefUsecase>();
            //NextService: services.AddSingleton<$ClassName$>();
            return services;
        }
    }
}