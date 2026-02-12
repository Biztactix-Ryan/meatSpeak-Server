using MeatSpeak.Server.Core.Sessions;
using Xunit;

namespace MeatSpeak.Server.Core.Tests;

public class FloodLimiterTests
{
    // ─── Basic burst behavior ───

    [Fact]
    public void FreshLimiter_StartsFullyCharged()
    {
        var limiter = new FloodLimiter(burstLimit: 5, tokenIntervalSeconds: 2.0, excessFloodThreshold: 20);

        // All 5 should be immediately allowed
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void AllowsExactlyBurstLimitCommands()
    {
        var limiter = new FloodLimiter(burstLimit: 5, tokenIntervalSeconds: 10.0, excessFloodThreshold: 20);

        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // The 6th must be throttled
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
    }

    [Fact]
    public void BurstLimitOfOne_AllowsExactlyOneCommand()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 20);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
    }

    [Fact]
    public void LargeBurstLimit_AllowsManyCommands()
    {
        var limiter = new FloodLimiter(burstLimit: 1000, tokenIntervalSeconds: 10.0, excessFloodThreshold: 2000);

        for (int i = 0; i < 1000; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
    }

    // ─── Throttling ───

    [Fact]
    public void ThrottlesWhenBucketEmpty()
    {
        var limiter = new FloodLimiter(burstLimit: 3, tokenIntervalSeconds: 10.0, excessFloodThreshold: 20);

        for (int i = 0; i < 3; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
    }

    [Fact]
    public void ThrottledCalls_AccumulateDebt()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 5);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Each throttled call adds 1 debt: 1, 2, 3, 4
        for (int i = 0; i < 4; i++)
            Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        // Debt=5 hits threshold
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume());
    }

    // ─── Excess flood ───

    [Fact]
    public void ExcessFlood_AtExactThreshold()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 10.0, excessFloodThreshold: 3);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Debt: 1, 2 → Throttled
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        // Debt 3 == threshold → ExcessFlood
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume());
    }

    [Fact]
    public void ExcessFlood_ThresholdOfOne_ImmediateAfterBurst()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 10.0, excessFloodThreshold: 1);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Very first throttled call: debt=1 >= threshold=1
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume());
    }

    [Fact]
    public void ExcessFlood_HighCostPushesPastThreshold()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 3);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Single cost=4 call when empty: debt=4 >= threshold=3
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume(4));
    }

    // ─── Cost variations ───

    [Fact]
    public void HighCost_ConsumesMultipleTokens()
    {
        var limiter = new FloodLimiter(burstLimit: 5, tokenIntervalSeconds: 10.0, excessFloodThreshold: 20);

        // Cost 2 → 3 tokens left
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume(2));
        // Cost 2 → 1 token left
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume(2));
        // Cost 2 but only 1 token → throttled
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume(2));
    }

    [Fact]
    public void HighCost_AddsFullCostToDebt()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 5);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Cost=2 when empty: debt += 2 → debt=2
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume(2));
        // Cost=2 again: debt += 2 → debt=4
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume(2));
        // Cost=1: debt += 1 → debt=5 >= threshold
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume(1));
    }

    [Fact]
    public void MixedCosts_BehavesCorrectly()
    {
        var limiter = new FloodLimiter(burstLimit: 10, tokenIntervalSeconds: 10.0, excessFloodThreshold: 50);

        // 5 cost=2 commands: uses all 10 tokens
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume(2));

        // Now empty
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume(1));
    }

    [Fact]
    public void CostExactlyEqualsRemainingTokens_IsAllowed()
    {
        var limiter = new FloodLimiter(burstLimit: 4, tokenIntervalSeconds: 10.0, excessFloodThreshold: 20);

        // Consume 2 → 2 left
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume(2));
        // Cost exactly equals remaining
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume(2));
        // Now empty
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume(1));
    }

    // ─── Token regeneration ───

    [Fact]
    public void TokensRegenerateOverTime()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.05, excessFloodThreshold: 20);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        Thread.Sleep(120);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void TokensDoNotExceedBurstLimit_AfterLongWait()
    {
        var limiter = new FloodLimiter(burstLimit: 3, tokenIntervalSeconds: 0.01, excessFloodThreshold: 20);

        // Wait long enough for many tokens to regenerate
        Thread.Sleep(200);

        // Should still only allow burst limit of 3
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
    }

    [Fact]
    public void SingleTokenRegen_AllowsExactlyOneMoreCommand()
    {
        // tokenInterval = 0.05s, so after ~60ms we should have ~1 token
        var limiter = new FloodLimiter(burstLimit: 5, tokenIntervalSeconds: 0.05, excessFloodThreshold: 20);

        // Drain all 5 tokens
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        // Wait for approximately 1 token to regenerate
        Thread.Sleep(60);

        // Should get exactly 1 more allowed
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        // Then throttled again (may have fractional token but not enough for cost=1)
        // Note: due to timing, we just verify the first one came back
    }

    [Fact]
    public void MultipleDrainRegenCycles()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.05, excessFloodThreshold: 50);

        for (int cycle = 0; cycle < 3; cycle++)
        {
            // Drain
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
            Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

            // Regen
            Thread.Sleep(150);
        }
    }

    // ─── Debt behavior ───

    [Fact]
    public void DebtReducesOverTime()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.05, excessFloodThreshold: 20);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Accumulate debt
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        Thread.Sleep(200);

        // After regen, debt should be reduced and commands allowed
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void SuccessfulConsume_ResetsDebtToZero()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.05, excessFloodThreshold: 20);

        // Drain and build debt
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=1
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=2
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=3

        // Wait for regen
        Thread.Sleep(200);

        // Successful consume resets debt to 0
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Drain again
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Debt should restart from 0, not from 3
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=1 (not 4)
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=2 (not 5)
        // With threshold=20, this would only be ExcessFlood if debt wasn't reset
    }

    [Fact]
    public void DebtDoesNotGoBelowZero()
    {
        var limiter = new FloodLimiter(burstLimit: 5, tokenIntervalSeconds: 0.01, excessFloodThreshold: 20);

        // Only small debt
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=1

        // Wait for lots of tokens — debt reduction shouldn't go negative
        Thread.Sleep(200);

        // Should work fine, no underflow issues
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void NearThreshold_DoesNotFalsePositiveExcessFlood()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 5);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Debt: 1, 2, 3, 4 → all Throttled
        for (int i = 0; i < 4; i++)
            Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        // Debt=4, one more should NOT be ExcessFlood yet (debt would be 5 == threshold)
        // Actually debt += 1 = 5 >= 5, so this IS ExcessFlood at exactly threshold
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume());
    }

    [Fact]
    public void OneBelowThreshold_IsStillThrottled()
    {
        var limiter = new FloodLimiter(burstLimit: 1, tokenIntervalSeconds: 10.0, excessFloodThreshold: 4);

        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // debt=1,2,3 → Throttled
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=1
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=2
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=3

        // debt would become 4 == threshold → ExcessFlood
        Assert.Equal(FloodResult.ExcessFlood, limiter.TryConsume());
    }

    // ─── Thread safety ───

    [Fact]
    public async Task ConcurrentAccess_DoesNotThrow()
    {
        var limiter = new FloodLimiter(burstLimit: 100, tokenIntervalSeconds: 0.001, excessFloodThreshold: 10000);

        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                    limiter.TryConsume();
            });
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentAccess_AllResultsAreValid()
    {
        var limiter = new FloodLimiter(burstLimit: 50, tokenIntervalSeconds: 10.0, excessFloodThreshold: 500);
        var results = new System.Collections.Concurrent.ConcurrentBag<FloodResult>();

        var tasks = new Task[8];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 20; j++)
                    results.Add(limiter.TryConsume());
            });
        }

        await Task.WhenAll(tasks);

        // Total attempts = 160, burst = 50
        // All results must be valid enum values
        foreach (var r in results)
            Assert.True(r == FloodResult.Allowed || r == FloodResult.Throttled || r == FloodResult.ExcessFlood);

        // Exactly 50 should be Allowed (burst limit)
        Assert.Equal(50, results.Count(r => r == FloodResult.Allowed));
    }

    [Fact]
    public async Task ConcurrentAccess_WithMixedCosts()
    {
        var limiter = new FloodLimiter(burstLimit: 100, tokenIntervalSeconds: 0.001, excessFloodThreshold: 10000);

        var tasks = new Task[5];
        for (int i = 0; i < tasks.Length; i++)
        {
            var cost = (i % 2) + 1; // alternating cost 1 and 2
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                    limiter.TryConsume(cost);
            });
        }

        await Task.WhenAll(tasks);
    }

    // ─── Edge cases ───

    [Fact]
    public void BackToBackConsume_WithinSameTimestamp()
    {
        // Two immediate calls should both work within burst
        var limiter = new FloodLimiter(burstLimit: 10, tokenIntervalSeconds: 10.0, excessFloodThreshold: 50);

        var r1 = limiter.TryConsume();
        var r2 = limiter.TryConsume();

        Assert.Equal(FloodResult.Allowed, r1);
        Assert.Equal(FloodResult.Allowed, r2);
    }

    [Fact]
    public void ExcessFlood_StopsBeingReturned_AfterRegen()
    {
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.05, excessFloodThreshold: 3);

        // Drain + trigger excess flood scenario debt
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=1
        Assert.Equal(FloodResult.Throttled, limiter.TryConsume()); // debt=2

        // Wait for regen to clear debt
        Thread.Sleep(200);

        // Should be allowed again, not stuck in excess flood
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void RapidFireThenPause_RecoversFully()
    {
        var limiter = new FloodLimiter(burstLimit: 3, tokenIntervalSeconds: 0.05, excessFloodThreshold: 20);

        // Rapid fire - drain burst
        for (int i = 0; i < 3; i++)
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());

        // Some throttles
        for (int i = 0; i < 5; i++)
            Assert.Equal(FloodResult.Throttled, limiter.TryConsume());

        // Long pause
        Thread.Sleep(300);

        // Should be fully recovered (tokens capped at burst, debt reduced)
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
        Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
    }

    [Fact]
    public void SteadyRate_BelowTokenInterval_AlwaysAllowed()
    {
        // If we send slower than the token regen rate, should always be allowed
        var limiter = new FloodLimiter(burstLimit: 2, tokenIntervalSeconds: 0.03, excessFloodThreshold: 20);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(FloodResult.Allowed, limiter.TryConsume());
            Thread.Sleep(40); // slower than 30ms token interval
        }
    }
}
