using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Onboarding.Function.DependencyInjection;
using Shared.Models.CosmosDb;
using Shared.Models.DependencyInjection;

IHost host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddSharedServices();
        services.AddCosmosDb(context.Configuration, context.HostingEnvironment);
        services.AddOnboardingServices();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

await host.RunAsync();
