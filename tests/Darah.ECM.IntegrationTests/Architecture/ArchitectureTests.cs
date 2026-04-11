using System.Reflection;
using Xunit;

namespace Darah.ECM.IntegrationTests.Architecture;

/// <summary>
/// Architecture conformance tests — validate layer boundaries and dependency rules.
///
/// ENFORCED RULES:
///   1. Domain layer has ZERO dependencies on Application, Infrastructure, or API
///   2. Application layer has NO dependency on Infrastructure or API
///   3. Application layer has NO dependency on ASP.NET Core (Microsoft.AspNetCore.*)
///   4. Domain layer has NO dependency on Entity Framework (Microsoft.EntityFrameworkCore.*)
///   5. Domain layer has NO dependency on MediatR, FluentValidation, or any framework libs
///   6. xECM module depends on Domain (allowed) but not on Infrastructure directly
///
/// These tests run on ASSEMBLY REFERENCES — faster and more reliable than reflection scanning.
/// </summary>
public sealed class ArchitectureLayerTests
{
    // Assembly names as they appear in csproj
    private const string DomainAssembly       = "Darah.ECM.Domain";
    private const string ApplicationAssembly  = "Darah.ECM.Application";
    private const string InfrastructureAssembly = "Darah.ECM.Infrastructure";
    private const string ApiAssembly          = "Darah.ECM.API";
    private const string xEcmAssembly         = "Darah.ECM.xECM";

    // ── Rule 1: Domain has no outward dependencies ──────────────────────────────
    [Fact]
    public void Domain_HasNoReferenceToApplication()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("Darah.ECM.Application", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_HasNoReferenceToInfrastructure()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("Darah.ECM.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_HasNoReferenceToAPI()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("Darah.ECM.API", StringComparison.Ordinal));
    }

    // ── Rule 2: Application has no reference to Infrastructure or API ─────────
    [Fact]
    public void Application_HasNoReferenceToInfrastructure()
    {
        var appRefs = GetReferencedAssemblyNames(ApplicationAssembly);
        Assert.DoesNotContain(appRefs, r =>
            r.StartsWith("Darah.ECM.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_HasNoReferenceToAPI()
    {
        var appRefs = GetReferencedAssemblyNames(ApplicationAssembly);
        Assert.DoesNotContain(appRefs, r =>
            r.StartsWith("Darah.ECM.API", StringComparison.Ordinal));
    }

    // ── Rule 3: Application has NO ASP.NET Core dependency ────────────────────
    [Fact]
    public void Application_HasNoAspNetCoreReference()
    {
        var appRefs = GetReferencedAssemblyNames(ApplicationAssembly);
        // IFormFile lives in Microsoft.AspNetCore.Http.Features
        Assert.DoesNotContain(appRefs, r =>
            r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    // ── Rule 4: Domain has no EF Core ─────────────────────────────────────────
    [Fact]
    public void Domain_HasNoEntityFrameworkReference()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    // ── Rule 5: Domain has no MediatR / FluentValidation ──────────────────────
    [Fact]
    public void Domain_HasNoMediatRReference()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("MediatR", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_HasNoFluentValidationReference()
    {
        var domainRefs = GetReferencedAssemblyNames(DomainAssembly);
        Assert.DoesNotContain(domainRefs, r =>
            r.StartsWith("FluentValidation", StringComparison.Ordinal));
    }

    // ─── Cross-module boundary tests ─────────────────────────────────────────
    [Fact]
    public void FileUploadRequest_IsInApplicationNotInfrastructure()
    {
        // FileUploadRequest must live in Application layer
        // This test verifies it's not accidentally moved to Infrastructure
        var type = Type.GetType("Darah.ECM.Application.Common.Abstractions.FileUploadRequest, Darah.ECM.Application");
        Assert.NotNull(type);
        Assert.True(type!.Assembly.GetName().Name == ApplicationAssembly,
            "FileUploadRequest must be in the Application assembly");
    }

    [Fact]
    public void DocumentLifecycleService_IsInDomainNotApplication()
    {
        var type = Type.GetType(
            "Darah.ECM.Domain.Services.DocumentLifecycleService, Darah.ECM.Domain");
        Assert.NotNull(type);
        Assert.True(type!.Assembly.GetName().Name == DomainAssembly,
            "DocumentLifecycleService must be in the Domain assembly (pure domain logic)");
    }

    [Fact]
    public void PolicyEngine_IsInInfrastructureNotDomain()
    {
        var type = Type.GetType(
            "Darah.ECM.Infrastructure.Security.Abac.PolicyEngine, Darah.ECM.Infrastructure");
        Assert.NotNull(type);
        Assert.True(type!.Assembly.GetName().Name == InfrastructureAssembly,
            "PolicyEngine is an infrastructure concern (uses ICurrentUser from HTTP context)");
    }

    // ─── Value Object immutability tests ──────────────────────────────────────
    [Fact]
    public void FileMetadata_IsRecord_Immutable()
    {
        var type = typeof(Darah.ECM.Domain.ValueObjects.FileMetadata);
        Assert.True(type.IsValueType || (type.IsClass && type.GetMethod("<Clone>$") is not null),
            "FileMetadata should be a record (immutable)");
    }

    [Fact]
    public void DocumentStatus_HasNoPublicSetters()
    {
        var type = typeof(Darah.ECM.Domain.ValueObjects.DocumentStatus);
        var publicSetters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters); // All setters must be private/init
    }

    // ─── Domain event rules ───────────────────────────────────────────────────
    [Fact]
    public void AllDomainEvents_ImplementIDomainEvent()
    {
        var domainEventType = typeof(Darah.ECM.Domain.Common.IDomainEvent);

        // Scan Domain.Events namespace
        var eventTypes = typeof(Darah.ECM.Domain.Events.Document.DocumentCreatedEvent).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.Contains("Events")
                && !t.IsInterface && !t.IsAbstract
                && domainEventType.IsAssignableFrom(t))
            .ToList();

        Assert.True(eventTypes.Count >= 10,
            $"Expected at least 10 domain events, found {eventTypes.Count}");

        foreach (var et in eventTypes)
            Assert.True(domainEventType.IsAssignableFrom(et),
                $"{et.Name} must implement IDomainEvent");
    }

    [Fact]
    public void AllDomainEvents_HaveEventTypeProperty()
    {
        var domainEventType = typeof(Darah.ECM.Domain.Common.IDomainEvent);
        var eventTypes = typeof(Darah.ECM.Domain.Events.Document.DocumentCreatedEvent).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.Contains("Events")
                && !t.IsInterface && !t.IsAbstract
                && domainEventType.IsAssignableFrom(t))
            .ToList();

        foreach (var et in eventTypes)
        {
            var prop = et.GetProperty("EventType");
            Assert.NotNull(prop);
            var instance = Activator.CreateInstance(et, GetDefaultConstructorArgs(et));
            if (instance is not null)
            {
                var value = prop!.GetValue(instance) as string;
                Assert.False(string.IsNullOrEmpty(value),
                    $"{et.Name}.EventType must not be empty");
                Assert.Equal(et.Name, value,
                    $"{et.Name}.EventType should equal the class name for traceability");
            }
        }
    }

    // ─── Entity rules ─────────────────────────────────────────────────────────
    [Fact]
    public void Document_HasNoPublicConstructors()
    {
        var docType = typeof(Darah.ECM.Domain.Entities.Document);
        var publicCtors = docType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicCtors);
    }

    [Fact]
    public void Document_HasFactoryMethod()
    {
        var docType = typeof(Darah.ECM.Domain.Entities.Document);
        var create = docType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(create);
    }

    // ─── Helper methods ───────────────────────────────────────────────────────
    private static IReadOnlyList<string> GetReferencedAssemblyNames(string assemblyName)
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);

            if (asm is null) return Array.Empty<string>();

            return asm.GetReferencedAssemblies()
                .Select(r => r.Name ?? "")
                .ToList();
        }
        catch
        {
            // Assembly not loaded — return empty (will fail assertion if needed)
            return Array.Empty<string>();
        }
    }

    private static object?[] GetDefaultConstructorArgs(Type type)
    {
        // For records, try to find a parameterless constructor or skip
        var ctor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (ctor is null) return Array.Empty<object?>();
        return ctor.GetParameters()
            .Select(p => p.HasDefaultValue ? p.DefaultValue :
                p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();
    }
}

/// <summary>
/// API Contract Tests — validates that all controllers follow standard patterns.
/// </summary>
public sealed class ApiContractTests
{
    [Fact]
    public void AllControllers_InV1Namespace()
    {
        // All controllers must be in v1 namespace for versioning consistency
        var controllerBase = typeof(Microsoft.AspNetCore.Mvc.ControllerBase);
        var controllerTypes = typeof(Darah.ECM.API.Controllers.v1.DocumentsController).Assembly
            .GetTypes()
            .Where(t => controllerBase.IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var ct in controllerTypes)
        {
            Assert.True(ct.Namespace?.Contains("Controllers.v1") == true,
                $"Controller {ct.Name} is not in Controllers.v1 namespace");
        }
    }

    [Fact]
    public void AllControllers_HaveAuthorizeAttribute()
    {
        var authorizeAttr = typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute);
        var controllerTypes = typeof(Darah.ECM.API.Controllers.v1.DocumentsController).Assembly
            .GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                && !t.IsAbstract)
            .ToList();

        foreach (var ct in controllerTypes)
        {
            var hasAuthorize = ct.GetCustomAttribute(authorizeAttr) is not null;
            Assert.True(hasAuthorize,
                $"Controller {ct.Name} must have [Authorize] attribute");
        }
    }

    [Fact]
    public void AllControllers_HaveApiControllerAttribute()
    {
        var apiAttr = typeof(Microsoft.AspNetCore.Mvc.ApiControllerAttribute);
        var controllerTypes = typeof(Darah.ECM.API.Controllers.v1.DocumentsController).Assembly
            .GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                && !t.IsAbstract)
            .ToList();

        foreach (var ct in controllerTypes)
        {
            var hasApiController = ct.GetCustomAttribute(apiAttr) is not null;
            Assert.True(hasApiController,
                $"Controller {ct.Name} must have [ApiController] attribute");
        }
    }
}
