using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using Roblox;

namespace Roblox.Services;

public static class GameServer2014Comm
{
    
    public static bool IsAuthorizedReportingIp(string? rawIp)
    {
        return true;
    }

    public sealed class HostSession
    {
        public string JobId { get; init; } = "";
        public string AuthToken { get; init; } = "";
        public string TempPlaceAccessKey { get; init; } = "";
        public long PlaceId { get; init; }
        public long UniverseId { get; init; }
        public long CreatorId { get; init; }
        public int CreatorType { get; init; }
        public int NetworkPort { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private static readonly ConcurrentDictionary<string, HostSession> SessionsByJob = new();
    private static readonly ConcurrentDictionary<string, (long placeId, DateTimeOffset exp)> TempAccessByKey = new();
    private static readonly ConcurrentDictionary<string, (long userId, DateTimeOffset exp)> VerificationTickets = new();

    
    public static HostSession CreateHostSession(long placeId, long universeId, long creatorId, int creatorType, int networkPort, TimeSpan ttl)
    {
        PruneExpired();
        var jobId = Guid.NewGuid().ToString("N");
        var authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var tempKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        
        var now = DateTimeOffset.UtcNow;
        var sessionExp = now.Add(ttl);
        var tempKeyExp = now.AddMinutes(2); 

        var session = new HostSession
        {
            JobId = jobId,
            AuthToken = authToken,
            TempPlaceAccessKey = tempKey,
            PlaceId = placeId,
            UniverseId = universeId,
            CreatorId = creatorId,
            CreatorType = creatorType,
            NetworkPort = networkPort,
            ExpiresAt = sessionExp,
        };

        SessionsByJob[jobId] = session;
        TempAccessByKey[tempKey] = (placeId, tempKeyExp);
        return session;
    }

 
    public static HostSession CreateHostSession(long placeId, long universeId, long creatorId, int creatorType, int networkPort, TimeSpan sessionTtl, TimeSpan tempPlaceAccessKeyTtl)
    {
        
        return CreateHostSession(placeId, universeId, creatorId, creatorType, networkPort, sessionTtl);
    }

    public static bool TryGetSession(string jobId, out HostSession? session)
    {
        PruneExpired();
        if (SessionsByJob.TryGetValue(jobId, out var s) && s.ExpiresAt > DateTimeOffset.UtcNow)
        {
            session = s;
            return true;
        }

        session = null;
        return false;
    }

    public static bool ValidateAuthToken(string jobId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(jobId))
            return false;
        return TryGetSession(jobId, out var s) && string.Equals(s!.AuthToken, token, StringComparison.Ordinal);
    }

    public static bool TryGetAuthorizedHostSession(string? jobId, string? authToken, out HostSession? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(authToken))
            return false;
        if (!TryGetSession(jobId, out var s) || s == null)
            return false;
        if (!string.Equals(s.AuthToken, authToken, StringComparison.Ordinal))
            return false;
        session = s;
        return true;
    }

    public static bool TryValidateTempPlaceAccess(long placeId, string? accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            return false;

        PruneExpired();

        if (!TempAccessByKey.TryGetValue(accessKey, out var entry))
            return false;

        if (entry.exp <= DateTimeOffset.UtcNow)
        {
            TempAccessByKey.TryRemove(accessKey, out _);
            return false;
        }

        return entry.placeId == placeId;
    }

    public static void RegisterVerificationTicket(string ticket, long userId, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            return;
        VerificationTickets[ticket.Trim()] = (userId, DateTimeOffset.UtcNow.Add(ttl));
    }

    public static bool TryVerifyTicket(string? ticket, long userId)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            return false;
        PruneExpired();
        if (!VerificationTickets.TryGetValue(ticket.Trim(), out var entry))
            return false;
        if (entry.exp <= DateTimeOffset.UtcNow)
        {
            VerificationTickets.TryRemove(ticket.Trim(), out _);
            return false;
        }

        return entry.userId == userId;
    }

    private static void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in SessionsByJob.Where(x => x.Value.ExpiresAt <= now))
        {
            SessionsByJob.TryRemove(kv.Key, out _);
        }

        foreach (var kv in TempAccessByKey.Where(x => x.Value.exp <= now))
        {
            TempAccessByKey.TryRemove(kv.Key, out _);
        }

        foreach (var kv in VerificationTickets.Where(x => x.Value.exp <= now))
        {
            VerificationTickets.TryRemove(kv.Key, out _);
        }
    }
}