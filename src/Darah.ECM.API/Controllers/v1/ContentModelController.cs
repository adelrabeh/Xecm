using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/content-model")]
[Authorize]
[Produces("application/json")]
public sealed class ContentModelController : ControllerBase
{
    private readonly EcmDbContext _db;
    public ContentModelController(EcmDbContext db) => _db = db;

    // ── GET schema definition ──────────────────────────────────────────────────
    [HttpGet("schema")]
    [AllowAnonymous]
    public IActionResult GetSchema() => Ok(ApiResponse<object>.Ok(new
    {
        version = "2.0",
        name = "Darah Institutional Content Model",
        nameAr = "نموذج المحتوى المؤسسي — دارة الملك عبدالعزيز",
        baseType = "darah:knowledgeAsset",
        types = new[]
        {
            new { code="darah:knowledgeAsset",      nameAr="أصل معرفي (قاعدة)",    nameEn="Knowledge Asset", icon="📦", color="#6366f1" },
            new { code="darah:book",                nameAr="كتاب",                   nameEn="Book",            icon="📗", color="#16a34a" },
            new { code="darah:manuscript",          nameAr="مخطوطة",                 nameEn="Manuscript",      icon="📜", color="#b45309" },
            new { code="darah:historicalDocument",  nameAr="وثيقة تاريخية",          nameEn="Historical Document", icon="📋", color="#7c3aed" },
            new { code="darah:imageAsset",          nameAr="صورة / مادة مرئية",      nameEn="Image Asset",     icon="🖼", color="#db2777" },
            new { code="darah:mapAsset",            nameAr="خريطة",                  nameEn="Map",             icon="🗺", color="#0891b2" },
            new { code="darah:researchPaper",       nameAr="بحث / دراسة",            nameEn="Research Paper",  icon="🔬", color="#059669" },
            new { code="darah:audioVisualAsset",    nameAr="مادة صوتية / مرئية",     nameEn="Audio-Visual",    icon="🎬", color="#dc2626" },
            new { code="darah:administrativeRecord",nameAr="سجل إداري",              nameEn="Administrative Record", icon="🗃", color="#0369a1" },
            new { code="darah:periodical",          nameAr="دورية / مجلة",            nameEn="Periodical",      icon="📰", color="#ca8a04" },
            new { code="darah:thesis",              nameAr="رسالة علمية",             nameEn="Thesis",          icon="🎓", color="#9333ea" },
        },
        aspects = new[]
        {
            new { code="darah:cataloging",    nameAr="الفهرسة",         fields = new[]{ "titleAr","titleEn","titleAlt","descriptionAr","language","source","creator","callNumber","productionYear","pageCount" } },
            new { code="darah:classification",nameAr="التصنيف",         fields = new[]{ "primarySubject","secondarySubjects","keywords","geographicScope","temporalCoverage","era","darahCategory","tags" } },
            new { code="darah:retention",     nameAr="الحفظ والاستبقاء",fields = new[]{ "retention","retentionExpiry","isLegalHold","disposalMethod","archivalReference" } },
            new { code="darah:rights",        nameAr="الحقوق والوصول",  fields = new[]{ "confidentiality","rightsStatement","license","copyright","isPublicAccess","isDownloadable","accessRestrictions" } },
            new { code="darah:digitization",  nameAr="الرقمنة",          fields = new[]{ "digitizationStatus","ocrQuality","ocrConfidence","scannerModel","resolution","digitizedBy","digitizedAt","masterFileFormat" } },
            new { code="darah:review",        nameAr="المراجعة والجودة", fields = new[]{ "status","statusNote","approvedBy","approvedAt","reviewedBy","qualityScore" } },
        },
        fieldDefinitions = GetFieldDefinitions()
    }));

    // ── LIST assets ────────────────────────────────────────────────────────────
    [HttpGet("assets")]
    public async Task<IActionResult> List(
        [FromQuery] AssetType? type,
        [FromQuery] AssetStatus? status,
        [FromQuery] string? q,
        [FromQuery] string? subject,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.KnowledgeAssets.AsNoTracking();
        if (type.HasValue) query = query.Where(a => a.Type == type.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.TitleAr.Contains(q) || (a.TitleEn != null && a.TitleEn.Contains(q))
                || (a.AssetCode != null && a.AssetCode.Contains(q)));
        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(a => a.PrimarySubject != null && a.PrimarySubject.Contains(subject));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    // ── GET single asset ───────────────────────────────────────────────────────
    [HttpGet("assets/{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var asset = await _db.KnowledgeAssets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssetId == id, ct);
        if (asset is null) return NotFound(ApiResponse<object>.Fail("الأصل غير موجود"));
        return Ok(ApiResponse<object>.Ok(asset));
    }

    // ── CREATE asset ───────────────────────────────────────────────────────────
    [HttpPost("assets")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var asset = KnowledgeAsset.Create(req.TitleAr, req.Type, userId, req.TitleEn, req.Source);
        _db.KnowledgeAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            asset.AssetId, asset.AssetCode, asset.TitleAr,
            message = "تم إنشاء الأصل المعرفي بنجاح"
        }));
    }

    // ── UPDATE status ──────────────────────────────────────────────────────────
    [HttpPost("assets/{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var asset = await _db.KnowledgeAssets.FindAsync(new object[]{id}, ct);
        if (asset is null) return NotFound();
        asset.UpdateStatus(req.Status, userId, req.Note);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── STATS ──────────────────────────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var all = await _db.KnowledgeAssets.AsNoTracking().ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            total = all.Count,
            byType = all.GroupBy(a => a.Type)
                .Select(g => new { type = g.Key.ToString(), nameAr = TypeNameAr(g.Key), count = g.Count() }),
            byStatus = all.GroupBy(a => a.Status)
                .Select(g => new { status = g.Key.ToString(), count = g.Count() }),
            byDigitization = all.GroupBy(a => a.DigitizationStatus)
                .Select(g => new { status = g.Key.ToString(), count = g.Count() }),
            digitizedPercent = all.Count == 0 ? 0 :
                Math.Round((double)all.Count(a => a.DigitizationStatus >= DigitizationStatus.Digitized) / all.Count * 100, 1),
        }));
    }

    private static string TypeNameAr(AssetType t) => t switch {
        AssetType.Book               => "كتاب",
        AssetType.Manuscript         => "مخطوطة",
        AssetType.HistoricalDocument => "وثيقة تاريخية",
        AssetType.ImageAsset         => "صورة",
        AssetType.MapAsset           => "خريطة",
        AssetType.ResearchPaper      => "بحث",
        AssetType.AudioVisualAsset   => "صوتي/مرئي",
        AssetType.AdministrativeRecord => "سجل إداري",
        AssetType.Periodical         => "دورية",
        AssetType.Thesis             => "رسالة علمية",
        _                            => "أصل معرفي",
    };

    private static object GetFieldDefinitions() => new[]
    {
        // Cataloging
        new { field="titleAr",         labelAr="العنوان بالعربية",        aspect="cataloging",    required=true,  type="text" },
        new { field="titleEn",         labelAr="العنوان بالإنجليزية",     aspect="cataloging",    required=false, type="text" },
        new { field="titleAlt",        labelAr="العنوان البديل",           aspect="cataloging",    required=false, type="text" },
        new { field="descriptionAr",   labelAr="الوصف",                   aspect="cataloging",    required=false, type="textarea" },
        new { field="language",        labelAr="اللغة",                   aspect="cataloging",    required=true,  type="select", options=new[]{"ar","en","ar,en","fa","tr","other"} },
        new { field="source",          labelAr="الجهة / المصدر",          aspect="cataloging",    required=false, type="text" },
        new { field="creator",         labelAr="المؤلف / المنتج",          aspect="cataloging",    required=false, type="text" },
        new { field="productionYear",  labelAr="سنة الإنتاج",             aspect="cataloging",    required=false, type="number" },
        new { field="productionPeriod",labelAr="الحقبة الزمنية",           aspect="cataloging",    required=false, type="text" },
        new { field="callNumber",      labelAr="رقم الحفظ / الاستدعاء",   aspect="cataloging",    required=false, type="text" },
        new { field="pageCount",       labelAr="عدد الصفحات",             aspect="cataloging",    required=false, type="number" },
        new { field="dimensions",      labelAr="الأبعاد",                 aspect="cataloging",    required=false, type="text" },
        // Classification
        new { field="primarySubject",  labelAr="التصنيف الموضوعي الرئيسي",aspect="classification",required=true,  type="taxonomy" },
        new { field="secondarySubjects",labelAr="موضوعات فرعية",          aspect="classification",required=false, type="taxonomy_multi" },
        new { field="keywords",        labelAr="الكلمات المفتاحية",        aspect="classification",required=false, type="tags" },
        new { field="geographicScope", labelAr="النطاق الجغرافي",         aspect="classification",required=false, type="text" },
        new { field="temporalCoverage",labelAr="الحقبة التاريخية",        aspect="classification",required=false, type="text" },
        new { field="era",             labelAr="العصر",                   aspect="classification",required=false, type="select",
              options=new[]{"ما قبل الإسلام","صدر الإسلام","الدولة الأولى","الدولة الثانية","المملكة الحديثة","المعاصر"} },
        // Status & Review
        new { field="status",          labelAr="حالة الأصل",              aspect="review",        required=true,  type="select",
              options=new[]{"Draft","UnderReview","Approved","Published","Restricted","Archived","Withdrawn"} },
        new { field="qualityScore",    labelAr="درجة الجودة",             aspect="review",        required=false, type="number" },
        // Rights
        new { field="confidentiality", labelAr="مستوى السرية",            aspect="rights",        required=true,  type="select",
              options=new[]{"Public","Internal","Confidential","StrictlyConfidential"} },
        new { field="license",         labelAr="الترخيص",                 aspect="rights",        required=false, type="select",
              options=new[]{"CC-BY","CC-BY-NC","CC-BY-SA","Proprietary","Public Domain","All Rights Reserved"} },
        new { field="isPublicAccess",  labelAr="متاح للعموم",             aspect="rights",        required=false, type="boolean" },
        new { field="isDownloadable",  labelAr="قابل للتنزيل",            aspect="rights",        required=false, type="boolean" },
        // Retention
        new { field="retention",       labelAr="جدول الاستبقاء",          aspect="retention",     required=true,  type="select",
              options=new[]{"Permanent","Years25","Years10","Years7","Years5","Years3"} },
        new { field="isLegalHold",     labelAr="حجز قانوني",              aspect="retention",     required=false, type="boolean" },
        new { field="archivalReference",labelAr="المرجع الأرشيفي",        aspect="retention",     required=false, type="text" },
        // Digitization
        new { field="digitizationStatus",labelAr="حالة الرقمنة",          aspect="digitization",  required=true,  type="select",
              options=new[]{"NotDigitized","InProgress","Digitized","QualityChecked","Published"} },
        new { field="ocrQuality",      labelAr="جودة OCR",                aspect="digitization",  required=false, type="select",
              options=new[]{"None","Low","Medium","High","Excellent"} },
        new { field="ocrConfidence",   labelAr="دقة OCR (%)",             aspect="digitization",  required=false, type="number" },
        new { field="resolution",      labelAr="الدقة (DPI)",             aspect="digitization",  required=false, type="number" },
        new { field="masterFileFormat",labelAr="تنسيق الملف الأصلي",      aspect="digitization",  required=false, type="select",
              options=new[]{"TIFF","RAW","PDF/A","JPEG2000","PNG","MP4","WAV"} },
    };
}

public sealed record CreateAssetRequest(string TitleAr, AssetType Type, string? TitleEn = null, string? Source = null);
public sealed record UpdateStatusRequest(AssetStatus Status, string? Note = null);
