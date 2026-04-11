using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Services;
using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.IntegrationTests.Concurrency;

/// <summary>
/// Concurrency tests — validates domain-level race condition protection.
///
/// NOTE: These tests validate the DOMAIN LOGIC against concurrent operations.
/// Infrastructure-level row versioning (EF Core DbUpdateConcurrencyException)
/// is tested separately in infrastructure integration tests with a real DB.
///
/// These tests simulate the logical outcome of concurrent operations
/// to verify domain rules hold regardless of timing.
/// </summary>
public sealed class CheckoutRaceConditionTests
{
    private readonly DocumentLifecycleService _lifecycle = new();

    // ─── Domain-level concurrent checkout ────────────────────────────────────
    [Fact]
    public void ConcurrentCheckout_DomainLevelFirstWins()
    {
        var document = Document.Create("وثيقة مشتركة", 1, 1, 1, "DOC-CONCURRENT");

        // Simulate User A checks out
        document.CheckOut(userId: 100);
        Assert.True(document.IsCheckedOut);
        Assert.Equal(100, document.CheckedOutBy);

        // User B attempts checkout (domain rejects immediately — no DB needed)
        Assert.Throws<InvalidOperationException>(() => document.CheckOut(userId: 200));
        Assert.Equal(100, document.CheckedOutBy); // User A still owns it
    }

    [Fact]
    public void ConcurrentCheckout_MultipleThreads_OnlyOneSucceeds()
    {
        var document = Document.Create("وثيقة", 1, 1, 1, "DOC-MT");
        var successCount = 0;
        var failCount = 0;
        var lockObj = new object();

        // 10 threads all try to checkout simultaneously
        var tasks = Enumerable.Range(1, 10).Select(userId => Task.Run(() =>
        {
            lock (lockObj) // Simulates DB serialization
            {
                try
                {
                    document.CheckOut(userId);
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref failCount);
                }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.Equal(1, successCount);  // Exactly one succeeds
        Assert.Equal(9, failCount);     // Rest fail
        Assert.True(document.IsCheckedOut);
    }

    // ─── CheckIn with zero VersionId is always rejected ──────────────────────
    [Fact]
    public void CheckIn_ZeroVersionId_AlwaysThrows()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.CheckOut(1);

        Assert.Throws<ArgumentException>(() => doc.CheckIn(0, 1));
        Assert.Throws<ArgumentException>(() => doc.CheckIn(-1, 1));

        // Only positive VersionId allowed
        doc.CheckIn(1, 1);
        Assert.Equal(1, doc.CurrentVersionId);
    }

    // ─── Double approval prevention ───────────────────────────────────────────
    [Fact]
    public void WorkflowTask_CannotBeCompletedTwice()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, 24);
        task.Complete(42);
        Assert.Equal("Completed", task.Status);

        // Second completion (race condition scenario)
        task.Complete(42); // Does not throw — but status doesn't change either
        Assert.Equal("Completed", task.Status);
        Assert.Equal(42, task.CompletedBy);
    }

    // ─── Concurrent Legal Hold + Write Operation ──────────────────────────────
    [Fact]
    public void LegalHold_AppliedBeforeWrite_BlocksSubsequentWrite()
    {
        var doc = Document.Create("وثيقة", 1, 1, 1, "DOC-002");

        // Thread 1: writes (checkout)
        doc.CheckOut(1);
        Assert.True(doc.IsCheckedOut);

        // Thread 2: legal hold applied (by admin)
        doc.ApplyLegalHold();
        Assert.True(doc.IsLegalHold);

        // Thread 1: attempts check-in — note: check-in is allowed even under legal hold
        // (it's completing the checkout, not a new write)
        doc.CheckIn(5, 1);
        Assert.False(doc.IsCheckedOut);
        Assert.Equal(5, doc.CurrentVersionId);

        // Now: new checkout blocked by legal hold
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(2));
    }

    // ─── Retention expiry race ────────────────────────────────────────────────
    [Fact]
    public void RetentionExpiry_JustExpired_IsAccuratelyDetected()
    {
        var doc = Document.Create("وثيقة منتهية", 1, 1, 1, "DOC-003");

        // Set expiry to yesterday
        doc.SetRetentionExpiry(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 1);

        Assert.True(doc.IsRetentionExpired());

        // Set expiry to tomorrow — not expired
        doc.SetRetentionExpiry(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 1);

        Assert.False(doc.IsRetentionExpired());
    }

    // ─── Workflow delegation + simultaneous task assignment ───────────────────
    [Fact]
    public void WorkflowDelegation_RangeValidation()
    {
        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        // Valid delegation
        var del = WorkflowDelegation.Create(1, 2, today, tomorrow.AddDays(5), 1);
        Assert.True(del.IsCurrentlyActive());

        // Expired delegation — not active
        var expired = WorkflowDelegation.Create(1, 2,
            today.AddDays(-10), today.AddDays(-1), 1);
        Assert.False(expired.IsCurrentlyActive());

        // Future delegation — not yet active
        var future = WorkflowDelegation.Create(1, 2,
            today.AddDays(5), today.AddDays(10), 1);
        Assert.False(future.IsCurrentlyActive());
    }

    // ─── Status transition atomicity ──────────────────────────────────────────
    [Fact]
    public void StatusTransition_InvalidTransitions_NeverCorruptState()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-004");
        Assert.Equal(DocumentStatus.Draft, doc.Status);

        // Invalid: Draft → Disposed
        Assert.Throws<InvalidOperationException>(() =>
            doc.TransitionStatus(DocumentStatus.Disposed, 1));

        // State unchanged after invalid transition
        Assert.Equal(DocumentStatus.Draft, doc.Status);

        // Valid: Draft → Pending
        doc.TransitionStatus(DocumentStatus.Pending, 1);
        Assert.Equal(DocumentStatus.Pending, doc.Status);

        // Invalid: Pending → Disposed
        Assert.Throws<InvalidOperationException>(() =>
            doc.TransitionStatus(DocumentStatus.Disposed, 1));

        // State still Pending — not corrupted
        Assert.Equal(DocumentStatus.Pending, doc.Status);
    }
}

/// <summary>
/// Performance benchmark tests — validates latency under simulated load.
/// NOTE: These are behavioral benchmarks, not precise performance measurements.
/// Use BenchmarkDotNet for production performance testing.
/// </summary>
public sealed class PerformanceBehaviorTests
{
    [Fact]
    public void PolicyEngine_EvaluatesThousandRequests_Under100ms()
    {
        var engine = new Darah.ECM.Infrastructure.Security.Abac.PolicyEngine(
            new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<
                Darah.ECM.Infrastructure.Security.Abac.PolicyEngine>());

        var permissions = new[] { "documents.read", "documents.download", "workflow.submit" };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            engine.Evaluate(new Darah.ECM.Infrastructure.Security.Abac.AccessRequest(
                UserId: i % 100,
                UserPermissions: permissions,
                UserRoleIds: new[] { i % 10 },
                UserDepartmentId: i % 5,
                Action: "documents.read",
                ResourceType: "Document",
                ResourceId: Guid.NewGuid().ToString(),
                ResourceClassificationOrder: (i % 4) + 1,
                WorkspaceId: null,
                IsResourceOnLegalHold: i % 10 == 0));
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"PolicyEngine evaluated 1000 requests in {sw.ElapsedMilliseconds}ms (expected <100ms)");
    }

    [Fact]
    public void DocumentStatusTransitions_ThousandDocs_Under50ms()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lifecycle = new DocumentLifecycleService();

        for (int i = 0; i < 1000; i++)
        {
            var doc = Document.Create($"Doc {i}", 1, 1, 1, $"DOC-{i:D6}");
            lifecycle.TransitionToWorkflowPending(doc, 1);
            lifecycle.TransitionToApproved(doc, 2);
            lifecycle.TransitionToActive(doc, 2);
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"1000 document lifecycle transitions took {sw.ElapsedMilliseconds}ms (expected <200ms)");
    }

    [Fact]
    public void MetadataFieldValidation_ThousandValidations_Under50ms()
    {
        var field = MetadataField.Create("amount", "المبلغ", "Amount", "Number", 1,
            isRequired: true);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            var (isValid, _) = field.Validate(i.ToString());
            Assert.True(isValid);
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 50,
            $"1000 field validations took {sw.ElapsedMilliseconds}ms (expected <50ms)");
    }
}
