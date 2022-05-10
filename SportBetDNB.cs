using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Collections.ObjectModel;
using System.Text.Json;
using Newtonsoft.Json;

namespace worker;

public class SportyBetDNB : OddsPortal
{
    private readonly ILogger<Worker> _logger;
    Dictionary<string, DTODNB> nextUrls = new Dictionary<string, DTODNB>();
    public SportyBetDNB(ILogger<Worker> logger) : base(logger)
    {
        _logger = logger;
    }

    public override void DoTask()
    {
        startScraping("https://www.sportybet.com/ng/sport/football");
        //startScraping();
        // _logger.LogInformation("NextUrls : {0}", nextUrls.Keys.Count());
        for (var i = 0; i < nextUrls.Keys.Count; i++)
        {
            var key = nextUrls.Keys.ElementAt(i);
            scrapDetailed(nextUrls[key].url, key);
        }

        // scrapDetailed("https://www.sportybet.com/ng/sport/football/sr:category:1/sr:tournament:17/sr:match:33040639");
        Console.WriteLine("Scrapping complete");
        Console.WriteLine(MyDictionaryToJson(nextUrls));
    }

    override
    protected void startScraping(string url = "https://www.sportybet.com/ng/sport/football/today")
    {
        var options = new ChromeOptions();

        options.AddArgument("--headless");
        //scrap for the titles and their respective urls(href)
        using (var driver = new ChromeDriver(".", options))
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
                                if (nextUrls.ContainsKey(key)) continue;

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


                                driver.Navigate().Back();
                                main = driver.FindElements(By.CssSelector("div.match-row"));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation(ex.ToString());
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

    void scrapDetailed(string url, string key)
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");


        using (var driver = new ChromeDriver(".", options))
        {
            try
            {
                // navigate to url
                driver.Navigate().GoToUrl(url);


                var elements = driver.FindElements(By.CssSelector(".m-detail-wrapper .m-table__wrapper"));
                if (elements.Count() == 0)
                {
                    return;
                };
                var _homeOdd = elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell span:nth-child(2)"));
                var _awayOdd = elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(3) span:nth-child(2)"));

                nextUrls[key].homeTeamOdd = double.Parse(_homeOdd.Text);
                nextUrls[key].awayTeamOdd = double.Parse(_awayOdd.Text);

                _logger.LogInformation("element : {0} vs {1}", _homeOdd.Text, _awayOdd.Text);

                for (var i = 0; i < elements.Count(); i++)
                {
                    var header = elements[i].FindElement(By.CssSelector(".m-table-header .m-table-row .m-table-cell .m-table-header-title"));
                    // Console.WriteLine("Header {0}", header.Text);
                    if (header.Text == "Draw No Bet")
                    {
                        var _homeDNBOdd = elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span:nth-child(2)"));
                        var _awayDNBOdd = elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"));

                        nextUrls[key].awayTeamDNBOdd = double.Parse(_awayDNBOdd.Text);
                        nextUrls[key].homeTeamDNBOdd = double.Parse(_homeDNBOdd.Text);

                        _logger.LogInformation("element : {0} vs {1}", _homeDNBOdd.Text, _awayDNBOdd.Text);
                        break;
                    }

                }


            }
            catch (Exception ex)
            {
                _logger.LogInformation("Exception : {0}", ex.Message);
            }
        }
    }

    string MyDictionaryToJson(Dictionary<string, DTODNB> dict)
    {
        return JsonConvert.SerializeObject(dict);
        // var entries = dict.Select(d =>
        //     string.Format("\"{0}\": [{1}]", d.Key, JsonSerializer.Serialize(d.Value)));
        // return "{" + string.Join(",", entries) + "}";
    }
}