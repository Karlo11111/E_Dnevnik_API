using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.Services;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Tests.Unit;

public class CacheServiceTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetOrFetch_FreshCache_ReturnsCachedData()
    {
        using var db = MakeDb();
        db.StudentCache.Add(new StudentCache
        {
            Email = "a@b.com",
            GradesData = "\"cached\"",
            GradesCachedAt = DateTime.UtcNow - TimeSpan.FromMinutes(30),
        });
        await db.SaveChangesAsync();

        var svc = new CacheService(db);
        var fetchCount = 0;

        var (data, _, fromCache) = await svc.GetOrFetch<string>(
            "a@b.com",
            c => c.GradesData,
            c => c.GradesCachedAt,
            (c, d, t) => { c.GradesData = d; c.GradesCachedAt = t; },
            CacheService.GradesTTL,
            async () => { fetchCount++; return "cached"; }
        );

        Assert.True(fromCache);
        Assert.Equal("cached", data);
        Assert.Equal(0, fetchCount);
    }

    [Fact]
    public async Task GetOrFetch_StaleCache_FetchesFresh()
    {
        using var db = MakeDb();
        db.StudentCache.Add(new StudentCache
        {
            Email = "a@b.com",
            GradesData = "\"old\"",
            GradesCachedAt = DateTime.UtcNow - TimeSpan.FromHours(3),
        });
        await db.SaveChangesAsync();

        var svc = new CacheService(db);
        var fetchCount = 0;

        var (data, _, fromCache) = await svc.GetOrFetch<string>(
            "a@b.com",
            c => c.GradesData,
            c => c.GradesCachedAt,
            (c, d, t) => { c.GradesData = d; c.GradesCachedAt = t; },
            CacheService.GradesTTL,
            async () => { fetchCount++; return "fresh"; }
        );

        Assert.False(fromCache);
        Assert.Equal("fresh", data);
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task GetOrFetch_ForceRefreshWithinCooldown_ReturnsCached()
    {
        using var db = MakeDb();
        db.StudentCache.Add(new StudentCache
        {
            Email = "a@b.com",
            GradesData = "\"cached\"",
            GradesCachedAt = DateTime.UtcNow - TimeSpan.FromMinutes(30),
            LastForceRefreshAt = DateTime.UtcNow - TimeSpan.FromMinutes(5),
        });
        await db.SaveChangesAsync();

        var svc = new CacheService(db);
        var fetchCount = 0;

        var (_, _, fromCache) = await svc.GetOrFetch<string>(
            "a@b.com",
            c => c.GradesData,
            c => c.GradesCachedAt,
            (c, d, t) => { c.GradesData = d; c.GradesCachedAt = t; },
            CacheService.GradesTTL,
            async () => { fetchCount++; return "fresh"; },
            forceRefresh: true
        );

        Assert.True(fromCache);
        Assert.Equal(0, fetchCount);
    }

    [Fact]
    public async Task GetOrFetch_ForceRefreshAfterCooldown_FetchesFresh()
    {
        using var db = MakeDb();
        db.StudentCache.Add(new StudentCache
        {
            Email = "a@b.com",
            GradesData = "\"cached\"",
            GradesCachedAt = DateTime.UtcNow - TimeSpan.FromMinutes(30),
            LastForceRefreshAt = DateTime.UtcNow - TimeSpan.FromMinutes(20),
        });
        await db.SaveChangesAsync();

        var svc = new CacheService(db);
        var fetchCount = 0;

        var (_, _, fromCache) = await svc.GetOrFetch<string>(
            "a@b.com",
            c => c.GradesData,
            c => c.GradesCachedAt,
            (c, d, t) => { c.GradesData = d; c.GradesCachedAt = t; },
            CacheService.GradesTTL,
            async () => { fetchCount++; return "fresh"; },
            forceRefresh: true
        );

        Assert.False(fromCache);
        Assert.Equal(1, fetchCount);
    }
}
