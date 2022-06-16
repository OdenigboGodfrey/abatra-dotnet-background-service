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
    // String SeleniumURL = "http://127.0.0.1:4444/wd/hub";
    // String SeleniumURL = "http://host.docker.internal:4444/wd/hub";
    // String SeleniumURL = "http://172.28.0.2:4444/wd/hub";
    String SeleniumURL = "http://172.17.0.1:4444/wd/hub";

    public SportyBetDNB(ILogger<Worker> logger) : base(logger)
    { }

    public override void DoTask()
    {
        Console.WriteLine("SportyBetDNB " + _redis.StringGet("second"));
        startScraping("https://www.sportybet.com/ng/sport/football?time=2");
        // processScrappedData();
        detail();
        _redis.StringSet(DateTime.Now.ToString("yyyy-MM-dd"), MyDictionaryToJson(nextUrls));
    }

    private void detail()
    {
        for (var i = 0; i < nextUrls.Keys.Count; i++)
        {
            try
            {
                // scrap individual page for more details
                var _key = nextUrls.Keys.ElementAt(i);
                var item = nextUrls[_key];
                if (item.status != 0)
                {
                    //processed
                    continue;
                }
                scrapDetailed(item.url, _key);
                Console.WriteLine($"Scrapping ${_key} complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }

    private void processScrappedData()
    {
        try
        {
            // match: {
            //         home,
            //         away,
            //         homeOdd,
            //         awayOdd,
            //         homeDNBOdd,
            //         awayDNBOdd,
            //     }
            /**
                if homeodd/awaydnb is safer, then save to redis
                if awayodd/homednb is safer, then save to redis
                // saving format is: [date]: [
                //     {
                        match,
                        homeOdd
                        awayDNBOdd
                    // },
                    //     {
                        match,
                        homeDNBOdd
                        awayOdd
                    // }
                // ]
            **/

            //  if homeodd/awaydnb is safer, then save to redis
            // if awayodd/homednb is safer, then save to redis
            // saving format is: [date]: [
            //     {

            // }
            // ]
            var output = new Dictionary<string, DTODNB>();
            for (var i = 0; i < nextUrls.Keys.Count; i++)
            {
                // scrap individual page for more details
                var key = nextUrls.Keys.ElementAt(i);
                var value = nextUrls.Values.ElementAt(i);
                // select the smaller of the home and away odd
                // select the larger of the homeDNBOdd and awayDNBOdd
                var selectOdd = value.homeTeamOdd > value.awayTeamOdd ? value.awayTeamOdd : value.homeTeamOdd;
                var selectedDNBOdd = value.homeTeamDNBOdd < value.awayTeamDNBOdd ? value.awayTeamDNBOdd : value.homeTeamDNBOdd;
                output.Add(key, new DTODNB
                {
                    url = value.url,
                    homeTeamTitle = value.homeTeamTitle,
                    awayTeamTitle = value.awayTeamTitle,
                    homeTeamOdd = selectOdd,
                    awayTeamOdd = selectedDNBOdd,
                });
            }
            _redis.StringSet(DateTime.Now.ToString("yyyy-MM-dd"), MyDictionaryToJson(output));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
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
            using (RemoteWebDriver driver = new RemoteWebDriver(new Uri(SeleniumURL), options.ToCapabilities()))
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

    public void scrapDetailed(string url, string key)
    {
        try
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("no-sandbox");


            // using (var driver = new ChromeDriver(".", options))
            using (RemoteWebDriver driver = new RemoteWebDriver(new Uri(SeleniumURL), options.ToCapabilities()))
            {
                try
                {
                    // navigate to url
                    driver.Navigate().GoToUrl(url);
                    double firstOdd,secondOdd;


                    var elements = driver.FindElements(By.CssSelector(".m-detail-wrapper .m-table__wrapper"));
                    if (elements.Count() == 0)
                    {
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
                                if (firstOdd < secondOdd) {

                                }
                                nextUrls[key].DNBOddMoney = Math.Round((firstOdd / total) * 100);
                                nextUrls[key].HAOddMoney = Math.Round((secondOdd / total) * 100);
                                if (nextUrls[key].DNBOddMoney > nextUrls[key].HAOddMoney) {
                                    nextUrls[key].systemApproved = true;
                                }


                                _logger.LogInformation("DNB home Odd vs DNB away Odd : {0} vs {1} - key: {2}", _homeDNBOdd, _awayDNBOdd, key);
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
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void miniCalculations() {}

    string MyDictionaryToJson(Dictionary<string, DTODNB> dict)
    {
        return JsonConvert.SerializeObject(dict);
        // var entries = dict.Select(d =>
        //     string.Format("\"{0}\": [{1}]", d.Key, JsonSerializer.Serialize(d.Value)));
        // return "{" + string.Join(",", entries) + "}";
    }

    public T MyJsonToDictionary<T>(string json)
    {
        //return JsonConvert.DeserializeObject<Dictionary<string, DTODNB>>(json);
        return JsonConvert.DeserializeObject<T>(json);
    }
}