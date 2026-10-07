using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Commerce.Api.Business;
using Commerce.Api.Data;
using Commerce.Api.Options;
using Commerce.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Commerce.Api.Extensions;

/// <summary>Registro de servicios, para mantener Program.cs corto y legible.</summary>
public static class stServiceCollectionExtensions
{
    public static IServiceCollection AddCommerceServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<bcJwtOptions>().Bind(config.GetSection(bcJwtOptions.SectionName));
        services.AddOptions<bcSeedOptions>().Bind(config.GetSection(bcSeedOptions.SectionName));
        services.AddOptions<bcCsvOptions>()
            .Bind(config.GetSection(bcCsvOptions.SectionName))
            .Validate(o => !string.IsNullOrEmpty(o.Delimiter)
                           && !string.IsNullOrWhiteSpace(o.DateFormat)
                           && o.BatchSize is > 0 and <= 50_000
                           && o.ChannelCapacity > 0,
                "Configuración 'Csv' inválida.")
            .ValidateOnStart();

        var connectionString = config.GetConnectionString("CommerceDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'CommerceDb'.");
        services.AddDbContext<bcCommerceDbContext>(o =>
            o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<bcCsvReader>();
        services.AddSingleton<bcCommerceRepository>();
        services.AddSingleton<bcTokenService>();
        services.AddScoped<bcCommerceService>();
        services.AddScoped<bcAuthService>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(bcJwtOptions.SectionName).Get<bcJwtOptions>() ?? new bcJwtOptions();
        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException(
                "Jwt:Key debe tener al menos 32 bytes. Configúrela con user-secrets o variables de entorno.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>Limita el login y el refresh a 5 intentos por minuto y por IP (fuerza bruta).</summary>
    public static IServiceCollection AddCommerceRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });
        return services;
    }

    public static IServiceCollection AddCommerceCors(this IServiceCollection services, IConfiguration config)
    {
        var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy("Angular", policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Commerce API",
                Version = "v1",
                Description = "Carga, validación y cuarentena de comercios (reto i-Route).",
            });

            // OperationId idéntico al nombre de la acción (EpXxx): NSwag lo usa para nombrar los métodos del cliente.
            options.CustomOperationIds(e => e.ActionDescriptor.RouteValues["action"]);

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Pegue el access token (sin la palabra Bearer).",
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                    },
                    Array.Empty<string>()
                },
            });
        });
        return services;
    }
}
