using System.Net;
using System.Web;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;

public class Utility
{
    public static dynamic CreateResponse(HttpStatusCode statusCode = HttpStatusCode.BadRequest, bool status = false, string message = "An error occurred.", dynamic data = null, Dictionary<string, dynamic> extraData = null)
    {
        if (data == null) { data = new List<string>(); }
        return prepareJson(status, data, message, extraData);
    }

    public static Dictionary<String, dynamic> prepareJson<T>(bool status, T objectReturned, string message, Dictionary<String, dynamic> ExtraData = null)
    {
        dynamic data = objectReturned;
        if (data == null) data = new List<string>();

        var ToBeReturned = new Dictionary<String, dynamic>() {
                {"status", status },
                {"data", data },
                {"message", message }
            };

        if (ExtraData != null)
        {
            ExtraData.ToList().ForEach(x => ToBeReturned.Add(x.Key, x.Value));
        }


        return ToBeReturned;
    }

    public static  string MyDictionaryToJson<T>(T dict)
    {
        return JsonConvert.SerializeObject(dict);
        // var entries = dict.Select(d =>
        //     string.Format("\"{0}\": [{1}]", d.Key, JsonSerializer.Serialize(d.Value)));
        // return "{" + string.Join(",", entries) + "}";
    }

    public static T MyJsonToDictionary<T>(string json)
    {
        //return JsonConvert.DeserializeObject<Dictionary<string, DTODNB>>(json);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public static void WaitForJs(RemoteWebDriver driver)
    {
        int delay = 5;
        // 5 secs delay
        while (delay > 0)
        {
            Thread.Sleep(1000);
            var jquery = (bool)(driver as IJavaScriptExecutor)
                .ExecuteScript("return window.jQuery == undefined");
            if (jquery)
            {
                break;
            }
            delay--;
        }
    }

}