using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace worker;

public class BGTestWorker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private int won, iterationTotalWon = 0 ;
    private int draw, iterationTotalDraw = 0 ;
    Dictionary<string, DNBDto> nextUrls = new Dictionary<string, DNBDto>();
    private OddsPortal _oddsPortal;
    private SportyBetDNB _sportyBetdnb;

    public BGTestWorker(ILogger<Worker> logger)
    {
        _logger = logger;
        _oddsPortal = new OddsPortal(_logger);
        _sportyBetdnb = new SportyBetDNB(_logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // _oddsPortal.DoTask();
        _sportyBetdnb.DoTask();


        return;
        // while (!stoppingToken.IsCancellationRequested)
        // {
        //     _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        //     await Task.Delay(500000, stoppingToken);
        // }

        // if stoppingToken.IsCancellationRequested dont run logic
        // if Execute async starts a timer instance
        // 
    }
}
