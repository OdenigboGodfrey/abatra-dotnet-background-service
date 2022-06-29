using worker;
using StackExchange.Redis;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // services.AddHostedService<Worker>();
        services.AddHostedService<BGTestWorker>();
        // services.AddScoped<ImongoAbatinoDbContext, MonogoAbatinoDBContext>();
    })
    .Build();

await host.RunAsync();
