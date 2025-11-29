// Copyright (c) Microsoft. All rights reserved.
using agentic_agent_101;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using dotenv.net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

DotEnv.Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // permissive for local PoC
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                Console.WriteLine($"JwtBearer: Message received. Authorization header present: {ctx.Request.Headers.ContainsKey("Authorization")}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"JwtBearer: Authentication failed: {ctx.Exception?.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                Console.WriteLine($"JwtBearer: Token validated. Principal name: {ctx.Principal?.Identity?.Name}");
                try
                {
                    var authHeader = ctx.Request?.Headers["Authorization"].ToString();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = authHeader.Substring("Bearer ".Length).Trim();
                        if (ctx.Principal?.Identity is System.Security.Claims.ClaimsIdentity ci)
                        {
                            ci.AddClaim(new System.Security.Claims.Claim("raw_token", raw));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"JwtBearer: Failed to attach raw token claim: {ex.Message}");
                }
                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // WARNING: permissive settings for local PoC only
            ValidateIssuerSigningKey = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            SignatureValidator = (token, parameters) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAGUI();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();


var agent = await AgenticAgent.CreateAgent();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map the AG-UI agent endpoint and require authentication
var agentEndpoint = app.MapAGUI("/agent", agent);
agentEndpoint.RequireAuthorization();

// Add a default route for the web server with a "Hello World" message
app.MapGet("/", () => "Hello World! Agentic Agent with AG-UI.");

await app.RunAsync();