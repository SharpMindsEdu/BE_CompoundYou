using System.Text;
using Api.Middleware;
using Application.Authorization;
using Application.Extensions;
using Carter;
using Domain.Enums;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
        };

        document.SecurityRequirements ??= [];
        document.SecurityRequirements.Add(
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    }
                ] = Array.Empty<string>(),
            }
        );

        return Task.CompletedTask;
    });
});

builder
    .Services.AddCarter()
    .AddApplicationRegistration()
    .AddInfrastructureRegistration(builder.Configuration);
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 5_000_000;
});
builder.Services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler, EmployeeAccessHandler>();
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        Policies.PlatformAdmin,
        p => p.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement())
    )
    .AddPolicy(
        Policies.TenantAdmin,
        p =>
            p.RequireAuthenticatedUser()
                .AddRequirements(new TenantRoleRequirement(TenantRole.TenantAdmin))
    )
    .AddPolicy(
        Policies.Manager,
        p =>
            p.RequireAuthenticatedUser()
                .AddRequirements(
                    new TenantRoleRequirement(TenantRole.Manager, TenantRole.TenantAdmin)
                )
    )
    .AddPolicy(
        Policies.Employee,
        p =>
            p.RequireAuthenticatedUser()
                .AddRequirements(
                    new TenantRoleRequirement(
                        TenantRole.Employee,
                        TenantRole.Manager,
                        TenantRole.TenantAdmin
                    )
                )
    );
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8100", "https://app.aboat-entertainment.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder
    .Services.AddAuthentication(options =>
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "super-secret-key")
            ),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;
                if (
                    !string.IsNullOrWhiteSpace(accessToken)
                    && (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/matrixHub"))
                )
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

var app = builder.Build();

app.ExecuteMigrations();
var scalarDebugToken = app.ConfigureDebugScalarAuthorization();

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    if (!string.IsNullOrWhiteSpace(scalarDebugToken))
    {
        options.Authentication ??= new ScalarAuthenticationOptions();
        options.Authentication.PreferredSecurityScheme = "Bearer";
        options.WithHttpBearerAuthentication(httpBearer =>
        {
            httpBearer.Token = scalarDebugToken;
        });
    }
});

app.MapCarter();
app.MapHub<Api.Hubs.ChatHub>("/chatHub");
app.MapHub<Infrastructure.Hubs.MatrixHub>("/matrixHub");
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.Run();