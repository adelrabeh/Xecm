using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/taxonomy")]
[Authorize]
[Produces("application/json")]
public sealed class TaxonomyController : ControllerBase
{
    private readonly EcmDbContext _db;
    public TaxonomyController(EcmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? domain, CancellationToken ct)
    {
        // If DB empty, return built-in taxonomy
        var count = await _db.TaxonomyCategories.CountAsync(ct);
        if (count == 0) return Ok(ApiResponse<object>.Ok(GetBuiltInTaxonomy()));

        var query = _db.TaxonomyCategories.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(domain)) query = query.Where(c => c.Domain == domain);
        return Ok(ApiResponse<object>.Ok(await query.OrderBy(c => c.Level).ThenBy(c => c.SortOrder).ToListAsync(ct)));
    }

    [HttpGet("tree")]
    public IActionResult GetTree() => Ok(ApiResponse<object>.Ok(GetBuiltInTaxonomy()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var cat = TaxonomyCategory.Create(req.Code, req.NameAr, req.NameEn ?? req.NameAr,
            req.Domain, userId, req.ParentId, req.Level);
        _db.TaxonomyCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { cat.CategoryId, cat.Code, cat.NameAr }));
    }

    // ─── Built-in DMS Taxonomy (Darah ECM) ────────────────────────────────────
    private static object GetBuiltInTaxonomy() => new
    {
        domains = new[]
        {
            new {
                code = "ADMIN", nameAr = "الوثائق الإدارية", nameEn = "Administrative",
                icon = "🏛️", level = 1,
                categories = new[]
                {
                    new { code="STR", nameAr="الاستراتيجية والتخطيط", icon="🎯",
                        types = new[]{ "استراتيجيات","مؤشرات الأداء","مبادرات","خطط تشغيلية" } },
                    new { code="GOV", nameAr="الحوكمة والامتثال", icon="⚖️",
                        types = new[]{ "السياسات والإجراءات","التدقيق الداخلي","إدارة المخاطر","الشؤون القانونية" } },
                    new { code="TEC", nameAr="التقنية والرقمنة", icon="💻",
                        types = new[]{ "نظام المعلومات","البنية التحتية","الأمن السيبراني","التحول الرقمي" } },
                    new { code="HR",  nameAr="الموارد البشرية", icon="👥",
                        types = new[]{ "التوظيف","التدريب والتطوير","الهيكل التنظيمي","تقييم الأداء" } },
                    new { code="FIN", nameAr="المالية والمشتريات", icon="💰",
                        types = new[]{ "الميزانيات","التقارير المالية","العقود","إدارة الموردين" } },
                    new { code="PRJ", nameAr="المشاريع والبرامج", icon="📊",
                        types = new[]{ "ملفات المشاريع","طلبات التغيير","مخاطر المشاريع" } },
                    new { code="OPS", nameAr="العمليات والخدمات", icon="⚙️",
                        types = new[]{ "العمليات التشغيلية","تقارير الخدمة","اتفاقيات مستوى الخدمة" } },
                    new { code="COM", nameAr="الاتصال والإعلام", icon="📢",
                        types = new[]{ "المراسلات","التقارير الإعلامية","الفعاليات والمؤتمرات" } },
                }
            },
            new {
                code = "HIST", nameAr = "الوثائق التاريخية والأرشيفية", nameEn = "Historical",
                icon = "📜", level = 1,
                categories = new[]
                {
                    new { code="SAH", nameAr="التاريخ السعودي", icon="🏺",
                        types = new[]{ "الدولة السعودية الأولى","الدولة السعودية الثانية","المملكة الحديثة","التاريخ المعاصر" } },
                    new { code="POL", nameAr="التاريخ السياسي", icon="🏛️",
                        types = new[]{ "أنظمة الحكم","المراسيم الملكية","الشخصيات السياسية","العلاقات الدولية" } },
                    new { code="SOC", nameAr="التاريخ الاجتماعي والثقافي", icon="🕌",
                        types = new[]{ "التراث والعادات","الحياة اليومية","الهوية الثقافية","التعليم التاريخي" } },
                    new { code="ECO", nameAr="التاريخ الاقتصادي", icon="📈",
                        types = new[]{ "التجارة والأسواق","اقتصاد النفط","التطور الاقتصادي" } },
                    new { code="GEO", nameAr="الجغرافيا والدراسات الإقليمية", icon="🗺️",
                        types = new[]{ "مناطق المملكة","المدن والمستوطنات","الجغرافيا التاريخية" } },
                    new { code="BIO", nameAr="التراجم والشخصيات", icon="👤",
                        types = new[]{ "ملوك المملكة","الشخصيات التاريخية","العلماء والمفكرون","الأدباء والمؤرخون" } },
                    new { code="MAN", nameAr="المخطوطات والأرشيف", icon="📋",
                        types = new[]{ "الوثائق التاريخية","الرسائل والمراسلات","المخطوطات","السجلات الحكومية" } },
                    new { code="HER", nameAr="التراث والآثار", icon="🏰",
                        types = new[]{ "المواقع الأثرية","المتاحف","التراث العمراني" } },
                    new { code="MED", nameAr="الأرشيف البصري والإعلامي", icon="📷",
                        types = new[]{ "الصور التاريخية","الفيديو","الخرائط والمصورات" } },
                }
            },
            new {
                code = "PUB", nameAr = "المنشورات والأبحاث", nameEn = "Publications",
                icon = "📚", level = 1,
                categories = new[]
                {
                    new { code="JRN", nameAr="محتوى مجلة الدارة", icon="📰",
                        types = new[]{ "مقالات المجلة","أعداد المجلة","المؤلفون" } },
                    new { code="RES", nameAr="الأبحاث والدراسات", icon="🔬",
                        types = new[]{ "أوراق بحثية","دراسات","تقارير أكاديمية","رسائل علمية" } },
                    new { code="REP", nameAr="التقارير المؤسسية", icon="📑",
                        types = new[]{ "التقارير السنوية","تقارير الإنجاز","الإحصاءات" } },
                }
            }
        }
    };
}

public sealed record CreateCategoryRequest(
    string Code, string NameAr, string Domain,
    string? NameEn = null, int? ParentId = null, int Level = 2);
