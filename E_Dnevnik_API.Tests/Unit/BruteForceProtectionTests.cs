using E_Dnevnik_API.ScrapingServices;

namespace E_Dnevnik_API.Tests.Unit;

public class BruteForceProtectionTests
{
    [Fact]
    public void IsBlocked_NoAttempts_ReturnsFalse()
    {
        var protection = new LoginBruteForceProtection();
        Assert.False(protection.IsBlocked("user@example.com"));
    }

    [Fact]
    public void IsBlocked_FourFailures_ReturnsFalse()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 4; i++)
            protection.RecordFailure("user@example.com");
        Assert.False(protection.IsBlocked("user@example.com"));
    }

    [Fact]
    public void IsBlocked_FiveFailures_ReturnsTrue()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 5; i++)
            protection.RecordFailure("user@example.com");
        Assert.True(protection.IsBlocked("user@example.com"));
    }

    [Fact]
    public void IsBlocked_CaseInsensitive()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 5; i++)
            protection.RecordFailure("USER@EXAMPLE.COM");
        Assert.True(protection.IsBlocked("user@example.com"));
    }

    [Fact]
    public void RecordSuccess_AfterLockout_ResetsAndUnblocks()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 5; i++)
            protection.RecordFailure("user@example.com");
        protection.RecordSuccess("user@example.com");
        Assert.False(protection.IsBlocked("user@example.com"));
    }

    [Fact]
    public void IsBlocked_DifferentEmails_AreTrackedIndependently()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 5; i++)
            protection.RecordFailure("user1@example.com");

        Assert.True(protection.IsBlocked("user1@example.com"));
        Assert.False(protection.IsBlocked("user2@example.com"));
    }

    [Fact]
    public void RecordSuccess_DuringPartialFailures_ResetsCounter()
    {
        var protection = new LoginBruteForceProtection();
        for (int i = 0; i < 3; i++)
            protection.RecordFailure("user@example.com");
        protection.RecordSuccess("user@example.com");

        // After success, 2 more failures should NOT trigger lockout (counter was reset)
        for (int i = 0; i < 4; i++)
            protection.RecordFailure("user@example.com");
        Assert.False(protection.IsBlocked("user@example.com"));
    }
}
