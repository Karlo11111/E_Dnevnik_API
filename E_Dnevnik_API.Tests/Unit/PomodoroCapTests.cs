using System.Text.Json;
using E_Dnevnik_API.Controllers;
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Tests.Unit;

public class PomodoroCapTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PomodoroController MakeController(AppDbContext db, out string email)
    {
        email = "test@test.com";
        var sessionStore = new SessionStore();
        var token = sessionStore.CreateSession(new System.Net.CookieContainer(), email);
        var controller = new PomodoroController(sessionStore, db);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = $"Bearer {token}";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        return controller;
    }

    [Fact]
    public async Task CompleteSession_EighthSession_SucceedsWithCappedFalse()
    {
        using var db = MakeDb();
        var controller = MakeController(db, out var email);
        db.PomodoroSessions.Add(new PomodoroSession
        {
            Email = email,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SessionsCompleted = 7,
            TotalMinutes = 175,
        });
        await db.SaveChangesAsync();

        var result = await controller.CompleteSession();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"capped\":false", json);
    }

    [Fact]
    public async Task CompleteSession_NinthSession_ReturnsCappedTrue()
    {
        using var db = MakeDb();
        var controller = MakeController(db, out var email);
        db.PomodoroSessions.Add(new PomodoroSession
        {
            Email = email,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SessionsCompleted = 8,
            TotalMinutes = 200,
        });
        await db.SaveChangesAsync();

        var result = await controller.CompleteSession();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"capped\":true", json);
    }

    [Fact]
    public async Task CompleteSession_YesterdaySessionsAtCap_DoNotBlockToday()
    {
        using var db = MakeDb();
        var controller = MakeController(db, out var email);
        db.PomodoroSessions.Add(new PomodoroSession
        {
            Email = email,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            SessionsCompleted = 8,
            TotalMinutes = 200,
        });
        await db.SaveChangesAsync();

        var result = await controller.CompleteSession();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"capped\":false", json);
    }
}
