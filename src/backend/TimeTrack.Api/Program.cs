using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using FluentValidation;
using Serilog;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Middleware;
using TimeTrack.Api.OpsMcp;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Extensions;
using TimeTrack.Backend.Infrastructure.Extensions;
using TimeTrack.Backend.AI.Extensions;
using TimeTrack.Backend.Infrastructure.Jobs.Configuration;
using TimeTrack.Backend.Infrastructure.Jobs.Dashboard;
using TimeTrack.Backend.Infrastructure.Persistence;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Kestrel limits
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 2 * 1024 * 1024; // 2MB
    });

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        if (context.HostingEnvironment.IsDevelopment())
        {
            configuration.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            configuration.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
        }
    });

    // Add services
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });
    builder.Services.AddEndpointsApiExplorer();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var frontendUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            policy.WithOrigins(
                    frontendUrl,
                    "http://localhost:5173",
                    "http://localhost:5174",
                    "http://localhost:3000",
                    "https://app.local",
                    "https://chronosx.cyriusx.com",
                    "https://chronosx-timetrack-web.gpoda0.easypanel.host",
                    "https://chronosx-dev-timetrack-web.gpoda0.easypanel.host"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // MCP (Ops / Maintenance read-only) - SysAdmin API key auth via middleware
    builder.Services.AddScoped<McpPlatformRequestContext>();
    builder.Services.AddSingleton<McpPerKeyRateLimiter>();
    builder.Services.AddSingleton<IOpsMcpAuditLogger, OpsMcpAuditLogger>();
    builder.Services.AddMcpServer()
        .WithHttpTransport(options =>
        {
            // Support GET + POST on the MCP endpoint (Streamable HTTP).
            options.Stateless = false;
        })
        .WithTools<OpsMcpTools>()
        .WithResources<OpsMcpResources>();

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("JWT Secret not configured");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TimeTrack",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TimeTrack.Api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        AuthorizationPolicies.Configure(options);
    });

    // Register authorization handler
    builder.Services.AddSingleton<IAuthorizationHandler, RoleAuthorizationHandler>();

    // Register application authorization service
    builder.Services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();

    // Rate Limiting
    // Disabled: the desktop + web UI make bursty report requests and this was
    // causing 429s/slow loads in real usage.
    // builder.Services.AddRateLimitingPolicies();

    // Application Layer (MediatR, FluentValidation)
    builder.Services.AddApplication();

    // Register MediatR handlers from the API assembly (Insights queries that
    // depend on Infrastructure/AI and therefore cannot live in Application).
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    });

    // Register FluentValidation validators from the API assembly
    builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

    builder.Services.AddMemoryCache();

    // Infrastructure Layer (Database, Health Checks, Services)
    builder.Services.AddInfrastructure(builder.Configuration);

    // AI Module (z.ai integration)
    builder.Services.AddAiModule(builder.Configuration);

    var app = builder.Build();

    // Optional database migration on startup (useful for EasyPanel deployments).
    // Enable with env var: `Database__MigrateOnStartup=true`
    if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TimeTrackDbContext>();

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                Log.Information("Applying {Count} pending EF migrations", pending.Count);
                await db.Database.MigrateAsync();
                Log.Information("EF migrations applied successfully");
            }
            else
            {
                Log.Information("No pending EF migrations");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply EF migrations on startup");
            throw;
        }
    }

    // Configure pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapScalarApiReference();
    }

    // CORS - Allow frontend to communicate with API
    app.UseCors();

    // Serilog request logging - capture all requests and their outcomes
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
        options.MessageTemplate = "HTTP {RequestMethod} {Path} responded {StatusCode} in {Elapsed:0.000} ms";
    });

    // Security headers (must be early in pipeline)
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Exception handling - must be early to catch all exceptions
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // Note: HTTPS redirection disabled for containerized environments (EasyPanel handles SSL termination)
    // app.UseHttpsRedirection();

    // Rate limiting disabled (see above)
    // app.UseRateLimiter();

    // MCP platform API key auth (only applies to /mcp)
    app.UseMiddleware<McpPlatformApiKeyMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<SubscriptionCheckMiddleware>();

    // Health check endpoints (must be after middleware to catch exceptions)

    // Simple liveness check - just verifies the app is running (no database dependency)
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow
            });
        }
    });

    // Full health check - includes database (for Docker HEALTHCHECK)
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        // Return 200 even if unhealthy (container stays running, logs show degraded status)
        ResultStatusCodes =
        {
            [HealthStatus.Unhealthy] = 200,
            [HealthStatus.Degraded] = 200,
            [HealthStatus.Healthy] = 200
        },
        ResponseWriter = async (context, report) =>
        {
            try
            {
                context.Response.ContentType = "application/json";

                var checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description ?? e.Value.Exception?.Message
                }).ToArray();

                var response = new
                {
                    status = report.Status.ToString(),
                    version = "1.0.0",
                    timestamp = DateTime.UtcNow,
                    checks
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Health check response writer failed");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new
                {
                    status = "Error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    });

    // Hangfire Dashboard - protected by Admin role
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthorizationFilter(app.Environment) },
        DashboardTitle = "TimeTrack Jobs Dashboard",
        StatsPollingInterval = 30000 // 30 seconds in milliseconds
    });

    // Configure recurring jobs after application starts
    HangfireConfiguration.ConfigureRecurringJobs();

    // MCP endpoint (Streamable HTTP)
    app.MapMcp("/mcp");

    app.MapControllers();

    Log.Information("Starting TimeTrack API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
