using Darah.ECM.Application.Notifications;
using Darah.ECM.Domain.Entities;

namespace Darah.ECM.Infrastructure.Persistence;

/// <summary>
/// Stub implementations for repositories not yet fully implemented.
/// These prevent DI resolution failures at startup.
/// </summary>

public sealed class StubMetadataRepository : IMetadataRepository
{
    public Task<IEnumerable<MetadataField>> GetByDocumentTypeAsync(int typeId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<MetadataField>());
    public Task<IEnumerable<DocumentMetadataValue>> GetByDocumentAsync(Guid docId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<DocumentMetadataValue>());
    public Task UpsertAsync(DocumentMetadataValue value, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task<int> CommitAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}

public sealed class StubRecordsRepository : IRecordsRepository
{
    public Task<RecordClass?> GetClassAsync(int classId, CancellationToken ct = default)
        => Task.FromResult<RecordClass?>(null);
    public Task<RetentionPolicy?> GetPolicyAsync(int policyId, CancellationToken ct = default)
        => Task.FromResult<RetentionPolicy?>(null);
    public Task<RetentionPolicy?> GetRetentionPolicyAsync(int policyId, CancellationToken ct = default)
        => Task.FromResult<RetentionPolicy?>(null);
    public Task<LegalHold?> GetLegalHoldAsync(int holdId, CancellationToken ct = default)
        => Task.FromResult<LegalHold?>(null);
    public Task AddDocumentLegalHoldAsync(DocumentLegalHold hold, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task<DisposalRequest?> GetDisposalRequestAsync(int requestId, CancellationToken ct = default)
        => Task.FromResult<DisposalRequest?>(null);
    public Task AddDisposalRequestAsync(DisposalRequest request, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task AddDisposalDocumentsAsync(int requestId, Guid[] documentIds, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task<IEnumerable<LegalHold>> GetActiveHoldsForDocumentAsync(Guid docId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<LegalHold>());
    public Task<int> CommitAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}

public sealed class StubFolderRepository : IFolderRepository
{
    public Task<Folder?> GetByIdAsync(int folderId, CancellationToken ct = default)
        => Task.FromResult<Folder?>(null);
    public Task<IEnumerable<Folder>> GetByLibraryAsync(int libraryId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<Folder>());
    public Task AddAsync(Folder folder, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task<int> CommitAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}
