using Microsoft.Extensions.DependencyInjection;
using GoogleBook.Services.Implementations;
using GoogleBook.Services.Interfaces;

namespace GoogleBook.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddTransient<ITaggingService, TaggingService>();
            services.AddTransient<IBookCatalogService, BookCatalogService>();
            return services;
        }
    }
}
