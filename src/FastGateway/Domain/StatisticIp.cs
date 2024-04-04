﻿namespace FastGateway.Domain;

public sealed class StatisticIp
{
    public long Id { get; set; }
    
    public string Ip { get; set; }
    
    public int Count { get; set; }

    public ushort Year { get; set; }
    
    public byte Month { get; set; }
    
    public byte Day { get; set; }
    
    /// <summary>
    /// 归属地
    /// </summary>
    public string? Location { get; set; }

    public string ServiceId { get; set; }
}