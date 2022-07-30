using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using System.Collections.ObjectModel;
using System.Text.Json;
using Newtonsoft.Json;
using StackExchange.Redis;
using worker.enums;

namespace worker;

public class SportyBetDNB : OddsPortal
{
    // private readonly ILogger<Worker> _logger;
    Dictionary<string, DTODNB> nextUrls = new Dictionary<string, DTODNB>();
    //String SeleniumURL = "http://127.0.0.1:4444/wd/hub";
    // String SeleniumURL = "http://host.docker.internal:4444/wd/hub";
    // String SeleniumURL = "http://172.28.0.2:4444/wd/hub";
    String SeleniumURL = "http://172.17.0.1:4444/wd/hub";
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
            //startVirtualScraping();
            Console.WriteLine("Scrapping process completed.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    // https://www.sportybet.com/ng/sport/football/today
    // https://www.sportybet.com/ng/sport/football/live_list
    // https://www.sportybet.com/ng/sport/football?time=24
    // https://www.sportybet.com/ng/sport/football/live_list
    override
    protected void startScraping(string url = "https://www.sportybet.com/ng/sport/football?time=24")
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
                    var _filter = mongoContext.getFilterBuilder();

                    var data = mongoContext.find(settings.MongoDB).ToList();

                    var filterDefinition = _filter.And(
                        _filter.Gte(x => x.createDate, DateTime.Today),
                        _filter.Lt(x => x.createDate, DateTime.Today.Add(new TimeSpan(1, 0, 0, 0, 0)))
                    );

                    var games = mongoContext.findWhere(settings.MongoDB, filterDefinition).ToList();

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
                                    if (games.Any(x => x.homeTeamTitle == homeElement.Text && x.awayTeamTitle == awayElement.Text && x.createDate == DateTime.Today))
                                    {
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

                                    if (url.Contains("live"))
                                    {
                                        scrapLiveDetailed(driver, nextUrls[key].url, key);
                                    }
                                    else
                                    {
                                        scrapDetailed(driver, nextUrls[key].url, key);
                                    }


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
                var _drawOdd = double.Parse((elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);
                var _awayOdd = double.Parse((elements[0].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(3) span:nth-child(2)"))).Text);
                var _time = driver.FindElements(By.CssSelector(".m-t-info .game-time"));
                var eventId = driver.FindElements(By.CssSelector(".m-t-info .event-id"));

                nextUrls[key].matchTime = _time[0].Text;
                nextUrls[key].eventId = eventId[0].Text;

                var oddInfo = new OddDTO
                {
                    awayTeamOdd = _awayOdd,
                    homeTeamOdd = _homeOdd,
                    drawOdd = _drawOdd,
                    matchId = nextUrls[key].matchId ?? "",
                    homeTeamTitle = nextUrls[key].homeTeamTitle,
                    awayTeamTitle = nextUrls[key].awayTeamTitle,
                };

                var firstHalfoddInfo = (OddDTO)oddInfo.Clone();
                var secondHalfoddInfo = (OddDTO)oddInfo.Clone();
                var firstHalfDNBoddInfo = (OddDTO)oddInfo.Clone();
                var secondHalfDNBoddInfo = (OddDTO)oddInfo.Clone();

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


                _logger.LogInformation($"Home Odd vs Away Odd vs Draw Odd {_homeOdd} vs {_awayOdd} vs {_drawOdd}, ID: {eventId[0].Text}");

                for (var i = 0; i < elements.Count(); i++)
                {
                    try
                    {
                        var header = elements[i].FindElement(By.CssSelector(".m-table-header .m-table-row .m-table-cell .m-table-header-title"));

                        if (false)
                        //if (header.Text.ToLower().Contains("over") && header.Text.ToLower().Contains("under"))
                        {
                            var _overOddTitle = ((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span"))).Text);
                            var _overOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span:nth-child(2)"))).Text);

                            var _underOddTitle = ((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span"))).Text);
                            var _underOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);

                            var ouObject = new OverUnderOddDTO();

                            // 1st half
                            if (header.Text.ToLower().Contains("1st half"))
                            {
                                modifyOUObject(_overOddTitle, ouObject, _overOdd, _underOdd);
                            }
                            // 2nd half
                            else if (header.Text.ToLower().Contains("2nd half"))
                            {
                                modifyOUObject(_overOddTitle, ouObject, _overOdd, _underOdd);
                            }
                            // full time
                            else
                            {
                                modifyOUObject(_overOddTitle, ouObject, _overOdd, _underOdd);
                            }

                        }

                        if (header.Text == ("1X2"))
                        {
                            var ftAbatrageOddInfo = (OddDTO)oddInfo.Clone();
                            ftAbatrageOddInfo.type = EventType.fullTime1x2.ToString();
                            ftAbatrageOddInfo.category = MatchCategory.fullTime.ToString();
                            ftAbatrageOddInfo.abatrage = Utility.calcAbatrage(oddInfo.homeTeamOdd, oddInfo.drawOdd, oddInfo.awayTeamOdd);
                            Console.WriteLine("Abatrage: {0}", ftAbatrageOddInfo.abatrage);
                            processAbatrage(ftAbatrageOddInfo);
                            writeToDb<OddDTO>(ftAbatrageOddInfo);
                        }

                        if (header.Text == ("1st Half - 1X2"))
                        {

                            firstHalfoddInfo.category = MatchCategory.firstHalf.ToString();
                            firstHalfoddInfo.type = EventType.firstHalf1x2.ToString();
                            // 1x2
                            var _1sthomeOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell span:nth-child(2)"))).Text);
                            var _1stdrawOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);
                            var _1stawayOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(3) span:nth-child(2)"))).Text);
                            firstHalfoddInfo.homeTeamOdd = _1sthomeOdd;
                            firstHalfoddInfo.awayTeamOdd = _1stawayOdd;
                            firstHalfoddInfo.drawOdd = _1stdrawOdd;

                            _logger.LogInformation("1st Half Home {0}: Draw {1}: Away {2}", _1sthomeOdd, _1stdrawOdd, _1stawayOdd);

                            firstHalfoddInfo.abatrage = Utility.calcAbatrage(firstHalfoddInfo.homeTeamOdd, firstHalfoddInfo.drawOdd, firstHalfoddInfo.awayTeamOdd);
                            Console.WriteLine("Abatrage: {0}", firstHalfoddInfo.abatrage);

                            processAbatrage(firstHalfoddInfo);
                            writeToDb<OddDTO>(firstHalfoddInfo);
                        }

                        if (header.Text == ("2nd Half - 1X2"))
                        {
                            secondHalfoddInfo.category = MatchCategory.secondHalf.ToString();
                            secondHalfoddInfo.type = EventType.secondHalf1x2.ToString();
                            // _logger.LogInformation("2nd Half");
                            // 1x2
                            var _2ndhomeOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell span:nth-child(2)"))).Text);
                            var _2nddrawOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);
                            var _2ndawayOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-table .m-outcome .m-table-cell:nth-child(3) span:nth-child(2)"))).Text);
                            secondHalfoddInfo.homeTeamOdd = _2ndhomeOdd;
                            secondHalfoddInfo.awayTeamOdd = _2ndawayOdd;
                            secondHalfoddInfo.drawOdd = _2nddrawOdd;
                            //secondHalfoddInfo.


                            _logger.LogInformation("2nd Half Home {0}: Draw {1}: Away {2}", _2ndhomeOdd, _2nddrawOdd, _2ndawayOdd);

                            secondHalfoddInfo.abatrage = Utility.calcAbatrage(secondHalfoddInfo.homeTeamOdd, secondHalfoddInfo.drawOdd, secondHalfoddInfo.awayTeamOdd);
                            Console.WriteLine("Abatrage: {0}", secondHalfoddInfo.abatrage);

                            processAbatrage(firstHalfoddInfo);
                            writeToDb<OddDTO>(secondHalfoddInfo);
                        }

                        if (header.Text == ("1st Half - Draw No Bet"))
                        {

                            firstHalfDNBoddInfo.category = MatchCategory.firstHalf.ToString();
                            firstHalfDNBoddInfo.type = EventType.firstHalfDNB.ToString();

                            var _homeDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span:nth-child(2)"))).Text);
                            var _awayDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);

                            firstHalfDNBoddInfo.awayTeamDNBOdd = _awayDNBOdd;
                            firstHalfDNBoddInfo.homeTeamDNBOdd = _homeDNBOdd;
                        }

                        if (header.Text == ("2nd Half - Draw No Bet"))
                        {
                            secondHalfDNBoddInfo.category = MatchCategory.secondHalf.ToString();
                            secondHalfDNBoddInfo.type = EventType.secondHalfDNB.ToString();

                            var _homeDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell span:nth-child(2)"))).Text);
                            var _awayDNBOdd = double.Parse((elements[i].FindElement(By.CssSelector(".m-outcome .m-table-cell:nth-child(2) span:nth-child(2)"))).Text);

                            secondHalfDNBoddInfo.awayTeamDNBOdd = _awayDNBOdd;
                            secondHalfDNBoddInfo.homeTeamDNBOdd = _homeDNBOdd;

                        }

                        if (header.Text == "Draw No Bet")
                        {
                            var fulltimeDNBoddInfo = (OddDTO)oddInfo.Clone(); ;
                            fulltimeDNBoddInfo.category = MatchCategory.fullTime.ToString();
                            fulltimeDNBoddInfo.type = EventType.fullTimeDNB.ToString();

                            _logger.LogInformation("FT DNB");
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

                            fulltimeDNBoddInfo.homeTeamDNBOdd = _homeDNBOdd;
                            fulltimeDNBoddInfo.awayTeamDNBOdd = _awayDNBOdd;

                            double total = firstOdd + secondOdd;
                            nextUrls[key].DNBOddMoney = Math.Round((firstOdd / total) * 100);
                            nextUrls[key].HAOddMoney = Math.Round((secondOdd / total) * 100);

                            if (nextUrls[key].HAOddMoney <= settings.DNBCutOff)
                            {
                                nextUrls[key].systemApproved = true;
                            }

                            // calculate what gains would be 
                            nextUrls[key]._1x2OdddWinGains = firstOdd * ((nextUrls[key].HAOddMoney / 100) * defaultAmount);
                            nextUrls[key].dnbWinGains = secondOdd * ((nextUrls[key].DNBOddMoney / 100) * defaultAmount);


                            _logger.LogInformation("DNB home Odd vs DNB away Odd : {0} vs {1} - key: {2},\n HA {3} DNBCutoff {4}", _homeDNBOdd, _awayDNBOdd, key, nextUrls[key].HAOddMoney, settings.DNBCutOff);

                            nextUrls[key].status = 0;

                            processDNBEvent(fulltimeDNBoddInfo);

                            if (fulltimeDNBoddInfo.secondOddMoney <= settings.DNB1x2Cutoff)
                            {
                                processDNB1x2Event(fulltimeDNBoddInfo, EventType.fullTimeDNB1x2);
                            }

                            writeToDb<OddDTO>(fulltimeDNBoddInfo);
                            //break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                // process dnb info after match scrapping is done 

                // first half
                firstHalfDNBoddInfo.homeTeamOdd = firstHalfoddInfo.homeTeamOdd;
                firstHalfDNBoddInfo.awayTeamOdd = firstHalfoddInfo.awayTeamOdd;
                processDNBEvent(firstHalfDNBoddInfo);

                _logger.LogInformation("1st Half DNB {0}: {1}", firstHalfDNBoddInfo.homeTeamDNBOdd, firstHalfDNBoddInfo.awayTeamDNBOdd);
                _logger.LogInformation("1st Half DNB Cash % {0}%: {1}%", firstHalfDNBoddInfo.firstOddMoney, firstHalfDNBoddInfo.secondOddMoney);

                if (firstHalfDNBoddInfo.secondOddMoney <= settings.DNB1x2Cutoff)
                {
                    processDNB1x2Event(firstHalfDNBoddInfo, EventType.firstHalfDNB1x2);
                }

                writeToDb<OddDTO>(firstHalfDNBoddInfo);

                // second half 
                secondHalfDNBoddInfo.homeTeamOdd = secondHalfoddInfo.homeTeamOdd;
                secondHalfDNBoddInfo.awayTeamOdd = secondHalfoddInfo.awayTeamOdd;

                processDNBEvent(secondHalfDNBoddInfo);

                _logger.LogInformation("2nd Half DNB {0}: {1} || {2}: {3}", secondHalfDNBoddInfo.homeTeamDNBOdd, secondHalfDNBoddInfo.awayTeamDNBOdd, secondHalfDNBoddInfo.homeTeamOdd, secondHalfDNBoddInfo.awayTeamOdd);
                _logger.LogInformation("2nd Half DNB Cash % {0}%: {1}%", secondHalfDNBoddInfo.firstOddMoney, secondHalfDNBoddInfo.secondOddMoney);

                if (secondHalfDNBoddInfo.secondOddMoney <= settings.DNB1x2Cutoff)
                {
                    processDNB1x2Event(secondHalfDNBoddInfo, EventType.secondHalfDNB1x2);
                }

                writeToDb<OddDTO>(secondHalfDNBoddInfo);


                Console.WriteLine($"Writing Game to DB");
                // save full time match
                writeToDb<DTODNB>(nextUrls[key]);

                // driver.Close();
                // driver.Dispose();
                // driver.Quit();

                // Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Exception : {0}", ex.Message);
            }

            //sleep for 3 seconds
            Thread.Sleep(3000);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void writeToDb<T>(T data)
    {
        Console.WriteLine("Writing DB");
        if (IsTypeof<DTODNB>(data)) {
            Console.WriteLine("Writing DB match");
            
            mongoContext.insertOne<T>(DbCollections.match.ToString(), data);
        }
        else if (IsTypeof<OddDTO>(data)) {
            Console.WriteLine("Writing DB event");
            mongoContext.insertOne<T>(DbCollections.matchEvent.ToString(), data);
        }

        //
    }

    public void scrapLiveDetailed(RemoteWebDriver driver, string url, string key)
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
                //var _time = driver.FindElements(By.CssSelector(".m-t-info .game-time"));
                //var eventId = driver.FindElements(By.CssSelector(".m-t-info .event-id"));

                nextUrls[key].matchTime = DateTime.Now.ToString("DDD DD/MM hh:mm");
                //nextUrls[key].eventId = eventId[0].Text;

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


                _logger.LogInformation("Home Odd vs Away Odd : {0} vs {1}", _homeOdd, _awayOdd);

                if (elements.Count() < 1)
                {
                    nextUrls[key].status = -1;
                }

                nextUrls[key].category = "live";

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
                            mongoContext.insertOne(settings.MongoDB, nextUrls[key]);
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
                _logger.LogInformation("Exception : {0}", ex.StackTrace);
                nextUrls[key].status = -2;
            }
            //sleep for 3 seconds
            Thread.Sleep(3000);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    protected void startVirtualScraping(string url = "https://www.sportybet.com/ng/sport/vFootball/live_list")
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
                    var _filter = mongoContext.getFilterBuilder();

                    var data = mongoContext.find(settings.MongoDB).ToList();

                    var filterDefinition = _filter.And(
                        _filter.Gte(x => x.createDate, DateTime.Today),
                        _filter.Lt(x => x.createDate, DateTime.Today.Add(new TimeSpan(1, 0, 0, 0, 0)))
                    );

                    var games = mongoContext.findWhere(settings.MongoDB, filterDefinition).ToList();
                    // Console.WriteLine("Called data {0}", Utility.MyDictionaryToJson(data));
                    // Console.WriteLine("================================================================");
                    // Console.WriteLine("Called fetchedGames {0}", Utility.MyDictionaryToJson(games));
                    // driver.Close();
                    // driver.Dispose();
                    // driver.Quit();
                    // Environment.Exit(0);

                    main = driver.FindElements(By.CssSelector("div.m-table-row"));
                    Console.WriteLine("Scrapping Elements");
                    for (var i = 0; i < main.Count();)
                    {
                        try
                        {
                            var element = main[i];
                            var teams = element.FindElements(By.CssSelector(".m-table-cell m-table-row"));
                            var odds = element.FindElements(By.CssSelector(".m-table-cell:nth-child(2) m-table-row"));

                            //var teams = element.FindElements(By.CssSelector("div div div.teams"));
                            foreach (var item in odds)
                            {
                                try
                                {
                                    var homeElement = item.FindElement(By.CssSelector("div div div.teams div.home-team"));
                                    var awayElement = item.FindElement(By.CssSelector("div div div.teams div.away-team"));

                                    var homeOddElement = item.FindElement(By.CssSelector("div:nth-child(2) div div.m-outcome:nth-child(1) span"));
                                    var drawOddElement = item.FindElement(By.CssSelector("div:nth-child(2) div div.m-outcome:nth-child(2) span"));
                                    var awayOddElement = item.FindElement(By.CssSelector("div:nth-child(2) div div.m-outcome:nth-child(3) span"));
                                    _logger.LogInformation("Home Content : {0} vs {1}", homeElement.Text, awayElement.Text);

                                    // var aba = (1 / double.Parse(homeOddElement.Text)) + (1 / double.Parse(drawOddElement.Text)) + (1 / double.Parse(awayOddElement.Text));
                                    // aba = aba * 100;
                                    var aba = Utility.calcAbatrage(double.Parse(homeOddElement.Text), ((byte)double.Parse(drawOddElement.Text)), double.Parse(awayOddElement.Text));

                                    _logger.LogInformation("Home Content : {0} vs {1} vs {2}. ABa: {3}", homeOddElement.Text, drawOddElement.Text, awayOddElement.Text, aba);

                                    string key = $"{homeElement.Text}-{awayElement.Text}";
                                    //prevent duplications
                                    // if (nextUrls.ContainsKey(key) || games.Any(x => x.homeTeamTitle == homeElement.Text && x.awayTeamTitle == awayElement.Text)) {
                                    //     Console.WriteLine("Game already scrapped, skipping");
                                    //     continue;
                                    // };

                                    nextUrls.Add(key, new DTODNB()
                                    {
                                        homeTeamTitle = homeElement.Text,
                                        awayTeamTitle = awayElement.Text,
                                        DNBOddMoney = aba,
                                    });
                                    item.Click();

                                    continue;
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

    private void modifyOUObject(string _overOddTitle, OverUnderOddDTO ouObject, double _overOdd, double _underOdd)
    {
        if (_overOddTitle.Contains("0.5"))
        {
            ouObject.over05 = _overOdd;
            ouObject.under05 = _underOdd;
        }
        else if (_overOddTitle.Contains("1.5"))
        {
            ouObject.over15 = _overOdd;
            ouObject.under15 = _underOdd;
        }
        else if (_overOddTitle.Contains("2.5"))
        {
            ouObject.over25 = _overOdd;
            ouObject.under25 = _underOdd;
        }
        else if (_overOddTitle.Contains("3.5"))
        {
            ouObject.over35 = _overOdd;
            ouObject.under35 = _underOdd;
        }
        else if (_overOddTitle.Contains("4.5"))
        {
            ouObject.over45 = _overOdd;
            ouObject.under45 = _underOdd;
        }
        else if (_overOddTitle.Contains("5.5"))
        {
            ouObject.over55 = _overOdd;
            ouObject.under55 = _underOdd;
        }
    }

    private void processDNBEvent(OddDTO odd)
    {
        //1x2
        double firstOdd = 0;
        //dnb
        double secondOdd = 0;

        // ensure DNB has higher odds than 1x2
        if (odd.homeTeamOdd > odd.awayTeamOdd)
        {
            firstOdd = odd.awayTeamOdd;
        }
        else
        {
            firstOdd = odd.homeTeamOdd;
        }

        if (odd.homeTeamDNBOdd > odd.awayTeamDNBOdd)
        {
            secondOdd = odd.homeTeamDNBOdd;
        }
        else
        {
            secondOdd = odd.awayTeamDNBOdd;
        }

        double total = firstOdd + secondOdd;

        odd.firstOdd = firstOdd;
        odd.secondOdd = secondOdd;

        // 1x2
        odd.firstOddMoney = Math.Round((secondOdd / total) * 100);
        // dnb
        odd.secondOddMoney = Math.Round((firstOdd / total) * 100);

        if (odd.firstOddMoney > 0 && odd.firstOddMoney <= settings.DNBCutOff)
        {
            odd.systemApproved = true;
        }

        // calculate what gains would be 
        odd.firstOddGain = firstOdd * ((odd.firstOddMoney / 100) * defaultAmount);
        odd.secondOddGain = secondOdd * ((odd.secondOddMoney / 100) * defaultAmount);
    }

    private void processDNB1x2Event(OddDTO odd, EventType eventType)
    {
        var dNB1x2oddInfo = (OddDTO)odd.Clone();
        dNB1x2oddInfo.category = odd.category;
        dNB1x2oddInfo.type = eventType.ToString();

        if (dNB1x2oddInfo.secondOddMoney > 0 && dNB1x2oddInfo.secondOddMoney <= settings.DNB1x2Cutoff)
        {
            dNB1x2oddInfo.systemApproved = true;
        }

        writeToDb<OddDTO>(dNB1x2oddInfo);
    }

    private void processAbatrage(OddDTO odd)
    {
        if (odd.abatrage < 100)
        {
            odd.systemApproved = true;
        }
    }

    bool IsTypeof<T>(object t)
    {
        return (t is T);
    }
}