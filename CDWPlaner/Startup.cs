using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: FunctionsStartup(typeof(CDWPlaner.Startup))]
[assembly: InternalsVisibleTo("CDWPlaner.Tests")]

namespace CDWPlaner
{
    class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IGitHubFileReader, GitHubFileReader>();
            builder.Services.AddSingleton<IDataAccess, DataAccess>();
        }
    }
}
