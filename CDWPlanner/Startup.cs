using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: FunctionsStartup(typeof(CDWPlanner.Startup))]
[assembly: InternalsVisibleTo("CDWPlanner.Tests")]

namespace CDWPlanner
{
    class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IGitHubFileReader, GitHubFileReader>();
            builder.Services.AddSingleton<IPlanZoomMeeting, PlanZoomMeeting>();
            builder.Services.AddSingleton<IDataAccess, DataAccess>();
        }
    }
}
