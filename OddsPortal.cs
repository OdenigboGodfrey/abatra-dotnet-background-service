using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace worker;

public class OddsPortal
{
    protected readonly ILogger<Worker> _logger;
    protected IDatabase _redis;
    protected MongoContext mongoContext;
    IConfiguration? configuration;
    private String redisHost;
    protected AppSettings settings;

    public OddsPortal()
    {}

    public OddsPortal(ILogger<Worker> logger)
    {
        _logger = logger;
        
        configuration = BGTestWorker.configuration;
        
        settings = BGTestWorker.settings;
        mongoContext = BGTestWorker.mongoContext;

        // Console.WriteLine("configuration: " + settings.RedisCacheOptions);
        redisHost = settings.RedisCacheOptions;

        // var redis = ConnectionMultiplexer.Connect(redisHost);
        // _redis = redis.GetDatabase();
    }



    private int won, iterationTotalWon = 0;
    private int draw, iterationTotalDraw = 0;
    Dictionary<string, DNBDto> nextUrls = new Dictionary<string, DNBDto>();
    public virtual void DoTask()
    {
        // https://www.oddsportal.com/soccer/luxembourg/national-division-2019-2020/results/
        // https://www.oddsportal.com/soccer/luxembourg/national-division-2020-2021/results/#/page/{0}/
        // https://www.oddsportal.com/soccer/luxembourg/national-division-2018-2019/results/

        startScraping("https://www.oddsportal.com/soccer/slovakia/2-liga/results/");
        foreach (var item in nextUrls)
        {
            iterationTotalDraw = 0;
            iterationTotalWon = 0;

            for (int i = 1; i <= 4; i++)
            {
                try
                {
                    var url = item.Value.url;

                    Scrap(string.Format(url + "#/page/{0}/", i));
                    item.Value.totalDraw = iterationTotalDraw;
                    item.Value.totalWon = iterationTotalWon;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }
            }

        }


        _logger.LogInformation("===================================");
        _logger.LogInformation("{won} Total Won: {draw} Total Draw", won, draw);
        _logger.LogInformation("===================================");
        _logger.LogInformation("===================================");
        var jsonStr = JsonSerializer.Serialize(nextUrls.Keys.ToList());
        _logger.LogInformation("DNB {0}", jsonStr, JsonSerializer.Serialize(nextUrls.Values.ToList()));
        _logger.LogInformation("===================================");

    }

    protected virtual void startScraping(string url = "https://www.oddsportal.com/soccer/luxembourg/national-division/results/")
    {
        //scrap for the titles and their respective urls(href)
        using (var driver = new ChromeDriver("."))
        {
            // navigate to url
            driver.Navigate().GoToUrl(url);

            IWebElement mainFitlers = null;


            try
            {
                mainFitlers = driver.FindElement(By.CssSelector("div.main-menu-gray > ul.main-filter"));
                var LIs = mainFitlers.FindElements(By.CssSelector("li"));
                foreach (var item in LIs)
                {
                    var content = item.FindElement(By.CssSelector("span strong a"));
                    _logger.LogInformation("content.Text: {0}, {1}", content.Text, content.GetAttribute("href"));
                    nextUrls.Add(content.Text, new DNBDto
                    {
                        url = content.GetAttribute("href"),
                        totalDraw = 0,
                        totalWon = 0,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }

        }
    }

    protected void Scrap(string url = "https://www.oddsportal.com/soccer/luxembourg/national-division/results/")
    {
        //scrap each page individually to get the win:loose ratio
        using (var driver = new ChromeDriver("."))
        {
            try
            {
                // navigate to url
                driver.Navigate().GoToUrl(url);


                IWebElement tournamentTable, paginationNextPage, paginationLastPage = null;
                string lastUrl = "";
                // get pagination ready
                tournamentTable = driver.FindElement(By.CssSelector("div#tournamentTable"));

                try
                {
                    // get pagination ready
                    paginationNextPage = driver.FindElement(By.CssSelector("#pagination > a:nth-child(4)"));
                    paginationLastPage = driver.FindElement(By.CssSelector("#pagination > a:nth-child(5)"));

                    lastUrl = paginationLastPage.GetAttribute("href");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }

                _logger.LogInformation("{lastUrl} ", lastUrl);
                var pagination = tournamentTable.FindElements(By.CssSelector("div a"));

                var xx = pagination.ToList();

                var matches = driver.FindElements(By.CssSelector(".deactivate"));

                foreach (var item in matches)
                {
                    try
                    {
                        var winningTeam = item.FindElement(By.CssSelector(".name a .bold"));
                        var losingTeam = item.FindElement(By.CssSelector(".name a"));

                        if (winningTeam != null) _logger.LogInformation("winningTeam {winningTeam}", winningTeam.Text);
                        if (losingTeam != null) _logger.LogInformation("losingTeam {losingTeam}", losingTeam.Text);
                        won++;
                        iterationTotalWon++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("Error {}", ex);
                        draw++;
                        iterationTotalDraw++;
                    }
                }

                // runScrapPagination(pagination.ToList(), 4, xx.Count - 1);
                // Scrap()
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }
        }
    }

    protected void runScrap(IWebElement driver)
    {
        try
        {
            var matches = driver.FindElements(By.CssSelector(".deactivate"));

            foreach (var item in matches)
            {
                try
                {
                    var winningTeam = item.FindElement(By.CssSelector(".name a .bold"));
                    var losingTeam = item.FindElement(By.CssSelector(".name a"));

                    if (winningTeam != null) _logger.LogInformation("winningTeam {winningTeam}", winningTeam.Text);
                    if (losingTeam != null) _logger.LogInformation("losingTeam {losingTeam}", losingTeam.Text);
                    won++;

                }
                catch (Exception ex)
                {
                    _logger.LogInformation("Error {}", ex);
                    draw++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("ex {}", ex.ToString());
        }
    }

    protected void runScrapPagination(List<IWebElement> paginations, int currentNo, int endNo)
    {
        try
        {

            var matches = paginations[currentNo].FindElements(By.CssSelector(".deactivate"));

            _logger.LogInformation("Scrapping Index {}", currentNo);

            foreach (var item in matches)
            {
                try
                {
                    var winningTeam = item.FindElement(By.CssSelector(".name a .bold"));
                    var losingTeam = item.FindElement(By.CssSelector(".name a"));

                    if (winningTeam != null) _logger.LogInformation("winningTeam {winningTeam}", winningTeam.Text);
                    if (losingTeam != null) _logger.LogInformation("losingTeam {losingTeam}", losingTeam.Text);
                    won++;

                }
                catch (Exception ex)
                {
                    _logger.LogInformation("Error {}", ex);
                    draw++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("ex {}", ex.ToString());
        }
    }

}
