using CMS.API.Data;
using CMS.API.Middleware;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "LocalhostCors";

// Register Dapper type handlers for date/time-only columns (used by later features).
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

builder.Services.AddControllers();

// Exposes the current request (and its JWT claims) to cross-cutting services like RowAuditWriter.
builder.Services.AddHttpContextAccessor();

// Swagger / OpenAPI (Swashbuckle).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CMS API", Version = "v1" });
});

// CORS: allow the Angular dev server and any localhost origin.
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.SetIsOriginAllowed(origin =>
                {
                    if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return uri.IsLoopback; // localhost / 127.0.0.1 on any port
                    }
                    return false;
                })
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Data access.
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<IRowAuditRepository, RowAuditRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ICoursePdfRepository, CoursePdfRepository>();

// Cross-cutting audit writer; repositories will call it after Insert/Update/Delete.
builder.Services.AddScoped<IRowAuditWriter, RowAuditWriter>();

// Login: mints signed JWT access tokens (stateless — no per-request state).
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// JWT bearer authentication. The validation key is resolved at runtime from
// SysConfig['appConfig'].symmetricSecurityKey — the same key the AuthController signs with.
builder.Services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Require an authenticated user for EVERY endpoint by default (fallback policy). AuthController
// opts back out with [AllowAnonymous], so login stays reachable without a token.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// First in the pipeline: catch any unhandled exception and return a safe 500.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger UI at /swagger — development only. In production the full API surface
// (routes, verbs, DTO shapes) must not be exposed to anonymous callers.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
    });
}
else
{
    // Enforce TLS outside development so bearer tokens / passwords never travel cleartext.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration/unit test project can reference the entry point assembly.</summary>
public partial class Program { }
