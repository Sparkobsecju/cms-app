using CMS.API.Data;
using CMS.API.Repositories;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "LocalhostCors";

// Register Dapper type handlers for date/time-only columns (used by later features).
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

builder.Services.AddControllers();

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
builder.Services.AddScoped<ILookupRepository, LookupRepository>();

var app = builder.Build();

// Swagger UI at /swagger.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
});

app.UseCors(CorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration/unit test project can reference the entry point assembly.</summary>
public partial class Program { }
