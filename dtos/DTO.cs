class DTODNB
{
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

}