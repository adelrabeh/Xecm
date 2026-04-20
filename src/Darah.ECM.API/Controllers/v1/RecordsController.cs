using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>Record-based ECM API — primary entity for all content.</summary>
[ApiController]
[Route("api/v1/records")]
[Authorize]
[Produces("application/json")]
public sealed class RecordsController : ControllerBase
{
    private readonly EcmDbContext _db;
    public RecordsController(EcmDbContext db) => _db = db;

    // ── GET domains + types + field definitions (form schema) ─────────────────
    [HttpGet("schema")]
    [AllowAnonymous]
    public IActionResult GetSchema() => Ok(ApiResponse<object>.Ok(GetBuiltInSchema()));

    [HttpGet("domains")]
    public async Task<IActionResult> GetDomains(CancellationToken ct)
    {
        var domains = await _db.RecordDomains.AsNoTracking()
            .Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync(ct);
        if (!domains.Any())
            return Ok(ApiResponse<object>.Ok(GetBuiltInSchema().domains));
        return Ok(ApiResponse<object>.Ok(domains));
    }

    [HttpGet("types/{domainId:int}")]
    public async Task<IActionResult> GetTypes(int domainId, CancellationToken ct)
    {
        var types = await _db.RecordTypes.AsNoTracking()
            .Where(t => t.DomainId == domainId && t.IsActive).ToListAsync(ct);
        if (!types.Any())
        {
            var schema = GetBuiltInSchema();
            var domain = ((dynamic[])schema.domains).FirstOrDefault(d => d.id == domainId);
            return Ok(ApiResponse<object>.Ok(domain?.types ?? Array.Empty<object>()));
        }
        return Ok(ApiResponse<object>.Ok(types));
    }

    [HttpGet("fields")]
    public IActionResult GetFields([FromQuery] int? domainId, [FromQuery] int? typeId)
    {
        var schema = GetBuiltInSchema();
        var fields = ((dynamic[])schema.fields)
            .Where(f => (domainId == null || f.domainId == null || f.domainId == domainId)
                     && (typeId == null   || f.typeId == null   || f.typeId == typeId))
            .OrderBy(f => f.sortOrder)
            .ToArray();
        return Ok(ApiResponse<object>.Ok(fields));
    }

    // ── CRUD Records ───────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? domainId, [FromQuery] int? typeId,
        [FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.Records.AsNoTracking();
        if (domainId.HasValue) query = query.Where(r => r.DomainId == domainId.Value);
        if (typeId.HasValue)   query = query.Where(r => r.TypeId == typeId.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r => r.TitleAr.Contains(q) || (r.TitleEn != null && r.TitleEn.Contains(q))
                || r.RecordNumber.Contains(q));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var record = await _db.Records.AsNoTracking().FirstOrDefaultAsync(r => r.RecordId == id, ct);
        if (record is null) return NotFound();
        var attachments = await _db.RecordAttachments.AsNoTracking()
            .Where(a => a.RecordId == id).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { record, attachments }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecordRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var record = Record.Create(req.TitleAr, req.DomainId, req.TypeId, userId, req.TitleEn, req.Department);
        if (!string.IsNullOrWhiteSpace(req.MetadataJson))
            record.SetMetadata(req.MetadataJson, userId);
        _db.Records.Add(record);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            record.RecordId, record.RecordNumber, record.TitleAr, record.Status,
            message = "تم إنشاء السجل بنجاح"
        }));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRecordRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var record = await _db.Records.FindAsync(new object[]{id}, ct);
        if (record is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.MetadataJson))
            record.SetMetadata(req.MetadataJson, userId);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var record = await _db.Records.FindAsync(new object[]{id}, ct);
        if (record is null) return NotFound();
        record.UpdateStatus(RecordStatus.UnderReview, userId);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var all = await _db.Records.AsNoTracking().ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            total    = all.Count,
            draft    = all.Count(r => r.Status == RecordStatus.Draft),
            review   = all.Count(r => r.Status == RecordStatus.UnderReview),
            approved = all.Count(r => r.Status == RecordStatus.Approved),
            archived = all.Count(r => r.Status == RecordStatus.Archived),
            byDomain = all.GroupBy(r => r.DomainId)
                .Select(g => new { domainId=g.Key, count=g.Count() }),
        }));
    }

    // ── Built-in schema (seed data for fresh InMemory DB) ────────────────────
    private static dynamic GetBuiltInSchema()
    {
        var domains = new dynamic[]
        {
            new { id=1, code="LEG", nameAr="قانوني وتعاقدي",   nameEn="Legal",          icon="⚖️",  color="#7c3aed" },
            new { id=2, code="FIN", nameAr="مالي",             nameEn="Financial",      icon="💰",  color="#059669" },
            new { id=3, code="ADM", nameAr="إداري",            nameEn="Administrative", icon="🏛️",  color="#0369a1" },
            new { id=4, code="HIS", nameAr="تاريخي وأرشيفي",  nameEn="Historical",     icon="📜",  color="#b45309" },
            new { id=5, code="RES", nameAr="بحثي وأكاديمي",   nameEn="Research",       icon="🔬",  color="#0891b2" },
        };

        var types = new dynamic[]
        {
            // Legal
            new { id=101, domainId=1, code="CONTRACT",    nameAr="عقد",              icon="📋" },
            new { id=102, domainId=1, code="AGREEMENT",   nameAr="اتفاقية",          icon="🤝" },
            new { id=103, domainId=1, code="LICENSE",     nameAr="ترخيص",            icon="📜" },
            new { id=104, domainId=1, code="LEGAL_OPINION",nameAr="رأي قانوني",      icon="⚖️" },
            // Financial
            new { id=201, domainId=2, code="BUDGET",      nameAr="ميزانية",          icon="📊" },
            new { id=202, domainId=2, code="INVOICE",     nameAr="فاتورة",           icon="🧾" },
            new { id=203, domainId=2, code="REPORT_FIN",  nameAr="تقرير مالي",       icon="📈" },
            new { id=204, domainId=2, code="PROCUREMENT", nameAr="مشتريات",          icon="🛒" },
            // Administrative
            new { id=301, domainId=3, code="LETTER",      nameAr="خطاب / مراسلة",   icon="✉️" },
            new { id=302, domainId=3, code="MEMO",        nameAr="مذكرة داخلية",     icon="📝" },
            new { id=303, domainId=3, code="REPORT_ADM",  nameAr="تقرير إداري",      icon="📄" },
            new { id=304, domainId=3, code="MEETING",     nameAr="محضر اجتماع",      icon="🗓" },
            new { id=305, domainId=3, code="POLICY",      nameAr="سياسة / إجراء",    icon="📋" },
            // Historical
            new { id=401, domainId=4, code="MANUSCRIPT",  nameAr="مخطوطة",           icon="📜" },
            new { id=402, domainId=4, code="PHOTO",       nameAr="صورة تاريخية",     icon="🖼" },
            new { id=403, domainId=4, code="MAP",         nameAr="خريطة",            icon="🗺" },
            new { id=404, domainId=4, code="ARCHIVE_DOC", nameAr="وثيقة أرشيفية",    icon="📦" },
            // Research
            new { id=501, domainId=5, code="PAPER",       nameAr="ورقة بحثية",       icon="🔬" },
            new { id=502, domainId=5, code="THESIS",      nameAr="رسالة علمية",       icon="🎓" },
            new { id=503, domainId=5, code="STUDY",       nameAr="دراسة",            icon="📚" },
            new { id=504, domainId=5, code="ARTICLE",     nameAr="مقالة",            icon="📰" },
        };

        // Fields: scope → core=all, domain=domain-specific, type=type-specific
        var fields = new dynamic[]
        {
            // ── CORE (every record) ──
            new { key="title_ar",      labelAr="العنوان بالعربية",      type="text",     scope="core",   required=true,  domainId=(int?)null, typeId=(int?)null, sortOrder=1 },
            new { key="title_en",      labelAr="العنوان بالإنجليزية",   type="text",     scope="core",   required=false, domainId=(int?)null, typeId=(int?)null, sortOrder=2 },
            new { key="description",   labelAr="الوصف / الملخص",       type="textarea", scope="core",   required=false, domainId=(int?)null, typeId=(int?)null, sortOrder=3 },
            new { key="department",    labelAr="الإدارة / القسم",      type="text",     scope="core",   required=true,  domainId=(int?)null, typeId=(int?)null, sortOrder=4 },
            new { key="document_date", labelAr="تاريخ الوثيقة",        type="date",     scope="core",   required=false, domainId=(int?)null, typeId=(int?)null, sortOrder=5 },
            new { key="security",      labelAr="مستوى السرية",         type="select",   scope="core",   required=true,  domainId=(int?)null, typeId=(int?)null, sortOrder=6,
                  options=new[]{"عام","داخلي","سري","مقيد"} },
            new { key="tags",          labelAr="الكلمات المفتاحية",    type="tags",     scope="core",   required=false, domainId=(int?)null, typeId=(int?)null, sortOrder=7 },
            // ── LEGAL (domain=1) ──
            new { key="contract_number",  labelAr="رقم العقد",          type="text",     scope="domain", required=true,  domainId=(int?)1, typeId=(int?)null, sortOrder=10 },
            new { key="counterparty",     labelAr="الطرف الثاني",       type="text",     scope="domain", required=true,  domainId=(int?)1, typeId=(int?)null, sortOrder=11 },
            new { key="contract_start",   labelAr="تاريخ البداية",      type="date",     scope="domain", required=true,  domainId=(int?)1, typeId=(int?)null, sortOrder=12 },
            new { key="contract_end",     labelAr="تاريخ النهاية",      type="date",     scope="domain", required=true,  domainId=(int?)1, typeId=(int?)null, sortOrder=13 },
            new { key="contract_value",   labelAr="قيمة العقد",         type="currency", scope="domain", required=false, domainId=(int?)1, typeId=(int?)null, sortOrder=14 },
            new { key="currency",         labelAr="العملة",             type="select",   scope="domain", required=false, domainId=(int?)1, typeId=(int?)null, sortOrder=15,
                  options=new[]{"SAR","USD","EUR","GBP"} },
            // ── FINANCIAL (domain=2) ──
            new { key="fiscal_year",   labelAr="السنة المالية",         type="number",   scope="domain", required=true,  domainId=(int?)2, typeId=(int?)null, sortOrder=10 },
            new { key="cost_center",   labelAr="مركز التكلفة",          type="text",     scope="domain", required=false, domainId=(int?)2, typeId=(int?)null, sortOrder=11 },
            new { key="amount",        labelAr="المبلغ",                type="currency", scope="domain", required=false, domainId=(int?)2, typeId=(int?)null, sortOrder=12 },
            new { key="currency_fin",  labelAr="العملة",                type="select",   scope="domain", required=false, domainId=(int?)2, typeId=(int?)null, sortOrder=13,
                  options=new[]{"SAR","USD","EUR"} },
            new { key="budget_chapter",labelAr="باب الميزانية",         type="text",     scope="domain", required=false, domainId=(int?)2, typeId=(int?)null, sortOrder=14 },
            // ── ADMINISTRATIVE (domain=3) ──
            new { key="letter_number", labelAr="رقم الخطاب",            type="text",     scope="domain", required=false, domainId=(int?)3, typeId=(int?)null, sortOrder=10 },
            new { key="sender",        labelAr="الجهة المرسِلة",        type="text",     scope="domain", required=false, domainId=(int?)3, typeId=(int?)null, sortOrder=11 },
            new { key="receiver",      labelAr="الجهة المستقبِلة",      type="text",     scope="domain", required=false, domainId=(int?)3, typeId=(int?)null, sortOrder=12 },
            new { key="reference",     labelAr="المرجع / الاستشارة",    type="text",     scope="domain", required=false, domainId=(int?)3, typeId=(int?)null, sortOrder=13 },
            new { key="priority_adm",  labelAr="الأولوية",              type="select",   scope="domain", required=false, domainId=(int?)3, typeId=(int?)null, sortOrder=14,
                  options=new[]{"عاجل","مهم","عادي"} },
            // ── HISTORICAL (domain=4) ──
            new { key="historical_period", labelAr="الحقبة التاريخية",  type="select",   scope="domain", required=false, domainId=(int?)4, typeId=(int?)null, sortOrder=10,
                  options=new[]{"ما قبل الإسلام","صدر الإسلام","الدولة الأولى","الدولة الثانية","المملكة الحديثة","المعاصر"} },
            new { key="geo_location",  labelAr="الموقع الجغرافي",       type="text",     scope="domain", required=false, domainId=(int?)4, typeId=(int?)null, sortOrder=11 },
            new { key="source_hist",   labelAr="المصدر",                type="text",     scope="domain", required=false, domainId=(int?)4, typeId=(int?)null, sortOrder=12 },
            new { key="digitization",  labelAr="حالة الرقمنة",          type="select",   scope="domain", required=false, domainId=(int?)4, typeId=(int?)null, sortOrder=13,
                  options=new[]{"لم يُرقَّم","قيد الرقمنة","مُرقَّم","منشور رقمياً"} },
            new { key="condition",     labelAr="حالة المادة الأصلية",   type="select",   scope="domain", required=false, domainId=(int?)4, typeId=(int?)null, sortOrder=14,
                  options=new[]{"ممتازة","جيدة","متوسطة","تحتاج ترميم"} },
            // ── RESEARCH (domain=5) ──
            new { key="author",        labelAr="المؤلف / الباحث",      type="text",     scope="domain", required=true,  domainId=(int?)5, typeId=(int?)null, sortOrder=10 },
            new { key="institution",   labelAr="المؤسسة / الجامعة",    type="text",     scope="domain", required=false, domainId=(int?)5, typeId=(int?)null, sortOrder=11 },
            new { key="pub_year",      labelAr="سنة النشر",            type="number",   scope="domain", required=false, domainId=(int?)5, typeId=(int?)null, sortOrder=12 },
            new { key="isbn_issn",     labelAr="ISBN / ISSN",          type="text",     scope="domain", required=false, domainId=(int?)5, typeId=(int?)null, sortOrder=13 },
            new { key="language_res",  labelAr="لغة الدراسة",          type="select",   scope="domain", required=false, domainId=(int?)5, typeId=(int?)null, sortOrder=14,
                  options=new[]{"العربية","الإنجليزية","الفرنسية","أخرى"} },
        };

        return new { domains, types, fields };
    }
}

public sealed record CreateRecordRequest(
    string TitleAr, int DomainId, int TypeId,
    string? TitleEn=null, string? Department=null, string? MetadataJson=null);

public sealed record UpdateRecordRequest(string? MetadataJson=null);
