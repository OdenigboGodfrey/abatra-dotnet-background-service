using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using System.Collections.ObjectModel;
using System.Text.Json;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace worker;

public class SportyBetDNB : OddsPortal
{
    // private readonly ILogger<Worker> _logger;
    Dictionary<string, DTODNB> nextUrls = new Dictionary<string, DTODNB>();
    String SeleniumURL = "http://127.0.0.1:4444/wd/hub";
    // String SeleniumURL = "http://host.docker.internal:4444/wd/hub";
    // String SeleniumURL = "http://172.28.0.2:4444/wd/hub";
    //String SeleniumURL = "http://172.17.0.1:4444/wd/hub";
    int defaultAmount;

    public SportyBetDNB(ILogger<Worker> logger) : base(logger)
    {
        defaultAmount = settings.DefaultBetAmount;
    }

    public override void DoTask()
    {
        try
        {
            //development: "https://www.sportybet.com/ng/sport/football"?time=2
            startScraping();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    override
    protected void startScraping(string url = "https://www.sportybet.com/ng/sport/football/today")
    {
        try
        {
            var options = new ChromeOptions();

            options.AddArgument("--headless");
            options.AddArgument("no-sandbox");

            //scrap for the titles and their respective urls(href)
            using (RemoteWebDriver driver = new RemoteWebDriver(new Uri(SeleniumURL), options.ToCapabilities(), TimeSpan.FromMinutes(1)))
            {
                // navigate to url
                driver.Navigate().GoToUrl(url);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);


                ReadOnlyCollection<IWebElement> main = null;
                /***
                {
                    match: {
                        home,
                        away,
                        homeOdd,
                        awayOdd,
                        homeDNBOdd,
                        awayDNBOdd,
                    }
                }
                **/

                try
                {
                    var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
                    // var fetchedGames = _redis.StringGet(currentDate);
                    var _filter = mongoContext.getFilterBuilder<DTODNB>();

                    var data = mongoContext.find<DTODNB>(settings.MongoDB).ToList();

                    var filterDefinition = _filter.And(
                        _filter.Gte(x => x.createDate, DateTime.Today),
                        _filter.Lt(x => x.createDate, DateTime.Today.Add(new TimeSpan(1,0,0,0,0)))
                    );

                    var games = mongoContext.findWhere<DTODNB>(settings.MongoDB, filterDefinition).ToList();
                    // Console.WriteLine("Called data {0}", Utility.MyDictionaryToJson(data));
                    // Console.WriteLine("================================================================");
                    Console.WriteLine("Called fetchedGames {0}", Utility.MyDictionaryToJson(games));
                    driver.Close();
                    driver.Dispose();
                    driver.Quit();
                    Environment.Exit(0);

                    main = driver.FindElements(By.CssSelector("div.match-row"));

                    for (var i = 0; i < main.Count();)
                    {
                        try
                        {
                            var element = main[i];
                            var teams = element.FindElements(By.CssSelector("div div div.teams"));
                            foreach (var item in teams)
                            {
                                try
                                {
                                    var homeElement = item.FindElement(By.CssSelector("div.home-team"));
                                    var awayElement = item.FindElement(By.CssSelector("div.away-team"));
                                    _logger.LogInformation("Home Content : {0} vs {1}", homeElement.Text, awayElement.Text);

                                    string key = $"{homeElement.Text}-{awayElement.Text}";
                                    //prevent duplications
                                    if (nextUrls.ContainsKey(key) || games.Any(x => x.homeTeamTitle == homeElement.Text && x.awayTeamTitle == awayElement.Text)) {
                                        Console.WriteLine("Game already scrapped, skipping");
                                        continue;
                                    };

                                    nextUrls.Add(key, new DTODNB()
                                    {
                                        homeTeamTitle = homeElement.Text,
                                        awayTeamTitle = awayElement.Text,
                                    });
                                    item.Click();

                                    _logger.LogInformation("Driver.Url : {0}", driver.Url);
                                    nextUrls[key].url = driver.Url;
                                    // - scrap dnb home
                                    // - scrap dnb away
                                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

                                    Utility.WaitForJs(driver);

                                    scrapDetailed(driver, nextUrls[key].url, key);

                                    driver.Navigate().Back();
                                    main = driver.FindElements(By.CssSelector("div.match-row"));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogInformation("Error at index " + teams.IndexOf(item) + " - " + ex.ToString());
                                }
                            }
                            i++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation(ex.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public void scrapDetailed(RemoteWebDriver driver, string url, string key)
    {
        try
        {
            try
            {
                // navigate to url
                driver.Navigate().GoToUrl(url);
                double firstOdd, secondOdd;
                Utility.WaitForJs(driver);


                var elements = driver.FindElements(By.CssSelector(".m-detail-wrapper .m-table__wrapper"));
                if (elements.Count() == 0)
                {
                    _logger.LogInformation($"{key}: No elements found");
                    nextUrls[key].status = -1;
                    return;
                };
                var _homeOdd = double.Parse((elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell span:nth-child(2)"))).Text);
                var _awayOdd = double.Parse((elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(3) span:nth-child(2)"))).Text);
                var _time = driver.FindElements(By.CssSelector(".m-t-info .game-time"));
                var eventId = driver.FindElements(By.CssSelector(".m-t-info .event-id"));

                nextUrls[key].matchTime = _time[0].Text;
                nextUrls[key].eventId = eventId[0].Text;

                // nextUrls[key].homeTeamOdd = double.Parse(_homeOdd);
                // nextUrls[key].awayTeamOdd = double.Parse(_awayOdd);
                if (_homeOdd > _awayOdd)
                {
                    nextUrls[key].awayTeamOdd = _awayOdd;
                    firstOdd = _awayOdd;
                }
                else
                {
                    nextUrls[key].homeTeamOdd = _homeOdd;
                    firstOdd = _homeOdd;
                }


                _logger.LogInformation("Home Odd vs Away Odd : {0} vs {1}, ID: {2}", _homeOdd, _awayOdd, eventId[0].Text);

                if (elements.Count() < 1) {
                    nextUrls[key].status = -1;
                }
                
                for (var i = 0; i < elements.Count(); i++)
                {
                    try
                    {
                        var header = elements[i].FindElement(By.CssSelector(".m-table-header .m-table-row .m-table-cell .m-table-header-title"));

                        if (header.Text == "Draw No Bet")
                        {
                            var _homeDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span:nth-child(2)"))).Text);
                            var _awayDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);

                            if (_homeDNBOdd > _awayDNBOdd)
                            {
                                nextUrls[key].homeTeamDNBOdd = _homeDNBOdd;
                                secondOdd = _homeDNBOdd;
                            }
                            else
                            {
                                nextUrls[key].awayTeamDNBOdd = _awayDNBOdd;
                                secondOdd = _awayDNBOdd;
                            }

                            // nextUrls[key].awayTeamDNBOdd = double.Parse(_awayDNBOdd.Text);
                            // nextUrls[key].homeTeamDNBOdd = double.Parse(_homeDNBOdd.Text);

                            double total = firstOdd + secondOdd;
                            nextUrls[key].DNBOddMoney = Math.Round((firstOdd / total) * 100);
                            nextUrls[key].HAOddMoney = Math.Round((secondOdd / total) * 100);
                            // nextUrls[key].DNBOddMoney > nextUrls[key].HAOddMoney && 
                            if (nextUrls[key].HAOddMoney <= settings.DNBCutOff)
                            {
                                nextUrls[key].systemApproved = true;
                            }

                            // calculate what gains would be 
                            nextUrls[key]._1x2OdddWinGains = firstOdd * ((nextUrls[key].HAOddMoney / 100) * defaultAmount);
                            nextUrls[key].dnbWinGains = secondOdd * ((nextUrls[key].DNBOddMoney / 100) * defaultAmount);


                            _logger.LogInformation("DNB home Odd vs DNB away Odd : {0} vs {1} - key: {2},\n HA {3} DNBCutoff {4}", _homeDNBOdd, _awayDNBOdd, key, nextUrls[key].HAOddMoney, settings.DNBCutOff);
                            
                            nextUrls[key].status = 0;

                            Console.WriteLine($"Scrapping DNB {key} complete\n Writing to DB");
                            mongoContext.insertOne<DTODNB>(settings.MongoDB, nextUrls[key]);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                }

            }
            catch (Exception ex)
            {
                _logger.LogInformation("Exception : {0}", ex.Message);
            }
            // }
            //sleep for 3 seconds
            Thread.Sleep(3000);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

}