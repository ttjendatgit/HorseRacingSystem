using System.Text;
using System.Text.Json.Serialization;
using HorseRacing.Data;
using HorseRacing.Options;
using HorseRacing.Repositories;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

// Npgsql: treat Unspecified DateTime as UTC for PostgreSQL timestamptz compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
// Use Railway's DATABASE_URL env var if present, otherwise fall back to appsettings.json
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(dbUrl))
{
    try
    {
        var uri = new Uri(dbUrl);
        var userParts = uri.UserInfo.Split(':', 2);
        var user = userParts[0];
        var pass = userParts.Length > 1 ? userParts[1] : "";
        var db = uri.AbsolutePath.TrimStart('/');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={db};Username={user};Password={pass};SslMode=Require;TrustServerCertificate=true;";
    }
    catch (UriFormatException)
    {
        // Not a URI — treat as raw connection string (e.g. Host=...;Database=...)
        connectionString = dbUrl;
        if (!connectionString.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
            connectionString += ";SslMode=Require;TrustServerCertificate=true;";
    }
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning).Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var configuredOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            if (origin.StartsWith("http://localhost:") || origin.StartsWith("http://127.0.0.1:")) return true;
            if (origin.EndsWith(".vercel.app")) return true;
            foreach (var configuredOrigin in configuredOrigins)
            {
                if (string.Equals(origin, configuredOrigin, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOwnerRepository, OwnerRepository>();
builder.Services.AddScoped<IJockeyRepository, JockeyRepository>();
builder.Services.AddScoped<IHorseRepository, HorseRepository>();
builder.Services.AddScoped<IRaceRepository, RaceRepository>();
builder.Services.AddScoped<IRaceEntryRepository, RaceEntryRepository>();
builder.Services.AddScoped<IJockeyInvitationRepository, JockeyInvitationRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();
builder.Services.AddScoped<IRaceResultRepository, RaceResultRepository>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IRoundRepository, RoundRepository>();
builder.Services.AddScoped<IRefereeRepository, RefereeRepository>();
builder.Services.AddScoped<IRefereeAssignmentRepository, RefereeAssignmentRepository>();
builder.Services.AddScoped<IHealthCheckRepository, HealthCheckRepository>();
builder.Services.AddScoped<IViolationRecordRepository, ViolationRecordRepository>();
builder.Services.AddScoped<IRaceReportRepository, RaceReportRepository>();
builder.Services.AddScoped<IUserRegistrationRepository, UserRegistrationRepository>();
builder.Services.AddScoped<IRaceManagementRepository, RaceManagementRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IPrizeRepository, PrizeRepository>();
builder.Services.AddScoped<IProtestRepository, ProtestRepository>();
builder.Services.AddScoped<IRaceComplaintRepository, RaceComplaintRepository>();
builder.Services.AddScoped<IRaceComplaintEvidenceRepository, RaceComplaintEvidenceRepository>();
builder.Services.AddScoped<IHorseTransferRepository, HorseTransferRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IInjuryRecordRepository, InjuryRecordRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IHorseService, HorseService>();
builder.Services.AddScoped<IRaceEntryService, RaceEntryService>();
builder.Services.AddScoped<IJockeyService, JockeyService>();
builder.Services.AddScoped<IRaceService, RaceService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<IRaceManagementService, RaceManagementService>();
builder.Services.AddScoped<IRefereeService, RefereeService>();
builder.Services.AddScoped<IRefereeHealthCheckService, RefereeHealthCheckService>();
builder.Services.AddScoped<IViolationRecordService, ViolationRecordService>();
builder.Services.AddScoped<IRaceReportService, RaceReportService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ILiveResultService, LiveResultService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IPrizeService, PrizeService>();
builder.Services.AddScoped<IProtestService, ProtestService>();
builder.Services.AddScoped<IRaceComplaintService, RaceComplaintService>();
builder.Services.AddScoped<IHorseTransferService, HorseTransferService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IInjuryRecordService, InjuryRecordService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddScoped<ICloudStorageService, CloudinaryStorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => {
        errorApp.Run(async context => {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Internal server error.\"}");
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();
app.UseRateLimiter();
app.UseCors("Frontend");

app.UseStaticFiles(); // serve uploaded images from wwwroot

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var disableMoneyFeatures =
    builder.Configuration.GetValue<bool>("Features:DisableMoneyFeatures");

if (disableMoneyFeatures)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;

        var isDisabledFeature =
            path.StartsWithSegments("/api/predictions") ||
            path.StartsWithSegments("/api/sepay") ||
            path.StartsWithSegments("/api/wallet") ||
            path.StartsWithSegments("/api/withdrawal") ||
            path.StartsWithSegments("/api/admin/predictions");

        if (isDisabledFeature)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "This feature is disabled in this environment."
            });

            return;
        }

        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

// Database initialization is controlled by environment configuration.
// Keep AutoMigrateDatabase=false when using the shared Railway database.
var autoMigrateDatabase =
    builder.Configuration.GetValue<bool>("Features:AutoMigrateDatabase");

var seedDemoData =
    builder.Configuration.GetValue<bool>("Features:SeedDemoData");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (autoMigrateDatabase)
    {
        await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment() && seedDemoData)
    {
        await DemoSeeder.SeedAsync(app.Services);
        await DemoSeeder.SeedExtraAsync(app.Services);
    }
    else if (!app.Environment.IsDevelopment())
    {
        await DemoSeeder.EnsureAdminAsync(app.Services);
    }

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
    await DemoSeeder.SeedHoangTournamentsInternalAsync(db, logger);
}

app.Run();
