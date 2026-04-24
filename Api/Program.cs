using System.Text;
using Application.Extensions;
using Api.Services;
using Carter;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddSingleton<ITradingTickerSubscriptionRegistry, TradingTickerSubscriptionRegistry>();
builder.Services.AddHostedService<TradingLiveTelemetryBroadcastService>();
builder.Services.AddHostedService<TradingTickerUpdateBroadcastService>();
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8100")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        policy.WithOrigins("https://app.aboat-entertainment.com/")
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
                    && (
                        path.StartsWithSegments("/chatHub")
                        || path.StartsWithSegments(Api.Hubs.TradingHub.HubRoute)
                    )
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
app.MapHub<Api.Hubs.TradingHub>(Api.Hubs.TradingHub.HubRoute);
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Run();
