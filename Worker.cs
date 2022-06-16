using StackExchange.Redis;
namespace worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private IDatabase _redis;

    // public Worker(ILogger<Worker> logger)
    // {
    //     _logger = logger;
    // }
    public Worker(ILogger<Worker> logger, IDatabase database)
    {
        _logger = logger;
        _redis = database;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
