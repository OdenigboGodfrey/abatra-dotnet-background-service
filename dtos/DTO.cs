using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class DTODNB
{
    // game/match object
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string url;
    public string homeTeamTitle;
    public string awayTeamTitle;
    public string matchTime;
    public double homeTeamOdd;
    public double awayTeamOdd;
    public double homeTeamDNBOdd;
    public double awayTeamDNBOdd;
    public int status = 0;
    public double HAOddMoney;
    public double DNBOddMoney;
    public bool systemApproved = false;
    public string? eventId;
    public string? score;
    public string? time;
    public string? winner;
    public double _1x2OdddWinGains = 0.0;
    public double dnbWinGains = 0.0;
    public double outcome = 0.0;
    public DateTime createdOn = DateTime.Now;
    public DateTime createDate = DateTime.Today;
    public string? category = "pre-match";
    public string? matchId = Guid.NewGuid().ToString();
}

[BsonIgnoreExtraElements]
public class OddDTO : ICloneable
{
    public object Clone()
    {
        var _clone = this.MemberwiseClone();
        return _clone;
    }
    
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string matchId;
    public double homeTeamOdd;
    public double awayTeamOdd;
    public double drawOdd;
    public double homeTeamDNBOdd;
    public double awayTeamDNBOdd;
    public string homeTeamTitle;
    public string awayTeamTitle;
    public OverUnderOddDTO OUOdd;
    public DateTime createdOn = DateTime.Now;
    public DateTime createDate = DateTime.Today;
    public string? category = "fulltime";
    public string? type = "dnb";
    public bool systemApproved = false;
    public double firstOddMoney;
    public double secondOddMoney;
    public double thirdOddMoney;
    public double firstOddGain;
    public double secondOddGain;
    public double thirdOddGain;
    public double? abatrage = 0.0;
    public string? winner;
    public double outcome = 0.0;
    public string[]? score;
    public double firstOdd;
    public double secondOdd;
    public string? status;
    public double ggOdd;
    public double ngOdd;
}

public class OverUnderOddDTO
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public double over05;
    public double under05;
    public double over15;
    public double under15;
    public double over25;
    public double under25;
    public double over35;
    public double under35;
    public double over45;
    public double under45;
    public double over55;
    public double under55;
    public double homeTeamOver05;
    public double homeTeamUnder05;
    public double homeTeamOver15;
    public double homeTeamUnder15;
    public double homeTeamOver25;
    public double homeTeamUnder25;
    public double homeTeamOver35;
    public double homeTeamUnder35;
    public double homeTeamOver45;
    public double homeTeamUnder45;
    public double homeTeamOver55;
    public double homeTeamUnder55;

    public double awayTeamOver05;
    public double awayTeamUnder05;
    public double awayTeamOver15;
    public double awayTeamUnder15;
    public double awayTeamOver25;
    public double awayTeamUnder25;
    public double awayTeamOver35;
    public double awayTeamUnder35;
    public double awayTeamOver45;
    public double awayTeamUnder45;
    public double awayTeamOver55;
    public double awayTeamUnder55;
    
    public DateTime createdOn = DateTime.Now;
    public DateTime createDate = DateTime.Today;
}
