using System.Net;
using System.Web;

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
}