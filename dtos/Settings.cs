public sealed class AppSettings
{
    public string? RedisCacheOptions { get; set; }
    public int ServiceTimer { get; set; }
    public int ServiceTimerMins { get; set; }
    public int DNBCutOff { get; set; }
    public int DefaultBetAmount { get; set; }
    public string? MongoConnectionString {get; set;}
    public string? MongoDB {get; set;}
    public int DNB1x2Cutoff {get; set;}
    
    
    
}