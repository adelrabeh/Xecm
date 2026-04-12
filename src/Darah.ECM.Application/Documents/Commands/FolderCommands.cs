using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Notifications;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Documents.Commands;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public sealed record FolderDto(
    int FolderId, string NameAr, string? NameEn, int LibraryId,
    int? ParentFolderId, string Path, int DepthLevel, int SortOrder,
    DateTime CreatedAt, List<FolderDto>? Children = null, int DocumentCount = 0);

public sealed record DocumentListItemDto(
    Guid DocumentId, string DocumentNumber, string TitleAr, string? TitleEn,
    string? DocumentTypeNameAr, string? LibraryNameAr, string StatusCode,
    string ClassificationCode, string? FileExtension, long? FileSizeBytes,
    bool IsCheckedOut, bool IsLegalHold, DateTime CreatedAt,
    string? CreatedByNameAr, Guid? PrimaryWorkspaceId);

public sealed record GetFolderTreeQuery(int LibraryId)
    : IRequest<ApiResponse<List<FolderDto>>>;

public sealed record GetFolderContentsQuery(int LibraryId, int? FolderId, int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<FolderContentsDto>>;

public sealed record FolderContentsDto(
    FolderDto? CurrentFolder, List<FolderDto> SubFolders,
    List<DocumentListItemDto> Documents, int TotalDocuments, List<BreadcrumbItem> Breadcrumb);

public sealed record BreadcrumbItem(int FolderId, string NameAr, string? NameEn, string Path);

// ─── CREATE FOLDER ────────────────────────────────────────────────────────────
public sealed record CreateFolderCommand(
    string NameAr, string? NameEn, int LibraryId, int? ParentFolderId,
    string? Description, int SortOrder = 0)
    : IRequest<ApiResponse<FolderDto>>;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(300).WithMessage("اسم المجلد مطلوب");
        RuleFor(x => x.LibraryId).GreaterThan(0).WithMessage("يجب تحديد المكتبة");
    }
}

public sealed class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, ApiResponse<FolderDto>>
{
    private readonly IFolderRepository _folderRepo;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public CreateFolderCommandHandler(IFolderRepository folderRepo, ICurrentUser user, IAuditService audit)
        { _folderRepo = folderRepo; _user = user; _audit = audit; }

    public async Task<ApiResponse<FolderDto>> Handle(CreateFolderCommand cmd, CancellationToken ct)
    {
        string parentPath = "/";
        int depth = 0;
        if (cmd.ParentFolderId.HasValue)
        {
            var parent = await _folderRepo.GetByIdAsync(cmd.ParentFolderId.Value, ct);
            if (parent is null) return ApiResponse<FolderDto>.Fail("المجلد الأب غير موجود");
            parentPath = parent.Path;
            depth = parent.DepthLevel + 1;
        }

        if (depth > 10) return ApiResponse<FolderDto>.Fail("لا يمكن إنشاء مجلدات بعمق أكثر من 10 مستويات");

        var folder = Folder.Create(cmd.NameAr, cmd.LibraryId, _user.UserId,
            cmd.ParentFolderId, cmd.NameEn, cmd.Description, cmd.SortOrder);

        await _folderRepo.AddAsync(folder, ct);
        await _folderRepo.CommitAsync(ct);
        folder.SetPath(parentPath, depth);
        await _folderRepo.CommitAsync(ct);

        await _audit.LogAsync("FolderCreated", "Folder", folder.FolderId.ToString(),
            newValues: new { folder.NameAr, folder.LibraryId }, ct: ct);

        return ApiResponse<FolderDto>.Ok(new FolderDto(
            folder.FolderId, folder.NameAr, folder.NameEn, folder.LibraryId,
            folder.ParentFolderId, folder.Path, folder.DepthLevel, folder.SortOrder, folder.CreatedAt),
            "تم إنشاء المجلد بنجاح");
    }
}

// ─── MOVE FOLDER ──────────────────────────────────────────────────────────────
public sealed record MoveFolderCommand(int FolderId, int? NewParentFolderId, int NewLibraryId)
    : IRequest<ApiResponse<bool>>;

public sealed class MoveFolderCommandHandler : IRequestHandler<MoveFolderCommand, ApiResponse<bool>>
{
    private readonly IFolderRepository _folderRepo;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public MoveFolderCommandHandler(IFolderRepository folderRepo, ICurrentUser user, IAuditService audit)
        { _folderRepo = folderRepo; _user = user; _audit = audit; }

    public async Task<ApiResponse<bool>> Handle(MoveFolderCommand cmd, CancellationToken ct)
    {
        var folder = await _folderRepo.GetByIdAsync(cmd.FolderId, ct);
        if (folder is null) return ApiResponse<bool>.Fail("المجلد غير موجود");

        if (cmd.NewParentFolderId.HasValue)
        {
            var newParent = await _folderRepo.GetByIdAsync(cmd.NewParentFolderId.Value, ct);
            if (newParent is null) return ApiResponse<bool>.Fail("المجلد الأب الجديد غير موجود");
            if (newParent.Path.StartsWith(folder.Path))
                return ApiResponse<bool>.Fail("لا يمكن نقل مجلد إلى داخل نفسه");
            folder.Move(cmd.NewParentFolderId, $"{newParent.Path}{cmd.FolderId}/", newParent.DepthLevel + 1, _user.UserId);
        }
        else
            folder.Move(null, $"/{cmd.FolderId}/", 0, _user.UserId);

        await _folderRepo.CommitAsync(ct);
        await _audit.LogAsync("FolderMoved", "Folder", cmd.FolderId.ToString(), ct: ct);
        return ApiResponse<bool>.Ok(true, "تم نقل المجلد بنجاح");
    }
}
