using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GoogleBook.Repository.Implementations;
using GoogleBook.Repository.Interfaces;

namespace GoogleBook.Repository
{
    public static class DependencyInjection
    {
        private const string DefaultGoogleBooksBaseUrl = "https://www.googleapis.com/";
        private const string DefaultWikidataBaseUrl = "https://query.wikidata.org/";

        public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IBookRepository, BookRepository>();

            string? googleBooksBaseUrl = configuration["ExternalApis:GoogleBooks:BaseUrl"] ?? DefaultGoogleBooksBaseUrl;
            string? wikidataBaseUrl = configuration["ExternalApis:Wikidata:BaseUrl"] ?? DefaultWikidataBaseUrl;

            services.AddHttpClient<IGoogleBooksClient, GoogleBooksClient>(client =>
            {
                client.BaseAddress = new Uri(googleBooksBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddHttpClient<IWikidataClient, WikidataClient>(client =>
            {
                client.BaseAddress = new Uri(wikidataBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GoogleBook.Api/1.0");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));
            });

            return services;
        }
    }
}
