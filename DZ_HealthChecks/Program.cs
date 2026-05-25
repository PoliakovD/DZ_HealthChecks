using System.Text;
using DZ_HealthChecks.Data;
using DZ_HealthChecks.HealthChecks;
using DZ_HealthChecks.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;

namespace DZ_HealthChecks;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Database
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

        // Controllers & OpenAPI
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        // JWT Authentication
        var jwtSecret = builder.Configuration["Jwt:Secret"]!;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        builder.Services.AddAuthorization();

        // Services
        builder.Services.AddScoped<TokenService>();

        // MinIO client
        var minioSection = builder.Configuration.GetSection("MinIO");
        var minioBuilder = new MinioClient()
            .WithEndpoint(minioSection["Endpoint"])
            .WithCredentials(minioSection["AccessKey"], minioSection["SecretKey"]);

        if (bool.Parse(minioSection["UseSSL"] ?? "false"))
            minioBuilder = minioBuilder.WithSSL();

        builder.Services.AddSingleton<IMinioClient>(minioBuilder.Build());

        // HealthChecks
        var connectionString = builder.Configuration.GetConnectionString("Postgres")!;
        builder.Services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionString,
                name: "Postgres HealthCheck",
                tags: new[] { "db", "ready" })
            .AddCheck<MinioHealthCheck>(
                "MinIO HealthCheck",
                tags: new[] { "ready", "minio" });

        var app = builder.Build();

        // Auto-create database tables
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        app.Run();
    }
}
