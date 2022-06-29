using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using System.Timers;
using MongoDB.Bson;
using MongoDB.Driver;

namespace worker;

public class BGTestWorker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private int won, iterationTotalWon = 0;
    private int draw, iterationTotalDraw = 0;
    Dictionary<string, DNBDto> nextUrls = new Dictionary<string, DNBDto>();
    // private OddsPortal _oddsPortal;
    private SportyBetDNB _sportyBetdnb;
    private IDatabase _redis;
    public static IMongoDatabase _mongo;
    public static MongoContext mongoContext;
    private readonly System.Timers.Timer _timer;

    public static IConfiguration? configuration;
    public static AppSettings settings;

    public BGTestWorker(ILogger<Worker> logger)
    {
        _logger = logger;

        configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build();

        settings = configuration.GetRequiredSection("Settings").Get<AppSettings>();

        // configureMongo();
        mongoContext = new MongoContext();
        
        _sportyBetdnb = new SportyBetDNB(_logger);

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // initial run
        _sportyBetdnb.DoTask();

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.ServiceTimerMins));

        while (await timer.WaitForNextTickAsync() && !stoppingToken.IsCancellationRequested)
        {
            //Business logic
            Console.WriteLine("Called");
            _sportyBetdnb.DoTask();
        }
    }
}
