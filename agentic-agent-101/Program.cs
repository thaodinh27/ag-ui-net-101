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
using OpenTelemetry;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Azure.Identity;

DotEnv.Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var envVars = new Dictionary<string, string>(DotEnv.Read());
var endpoint = envVars["AZURE_OPENAI_ENDPOINT"];
var deploymentName = envVars["AZURE_OPENAI_DEPLOYMENT_NAME"];

// Configure OpenTelemetry with console and optionally Azure Monitor exporters
var appInsightsConnectionString = envVars["APPLICATIONINSIGHTS_CONNECTION_STRING"];

var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .AddSource("AGUIAssistant.Telemetry")
    .AddSource("Microsoft.Agents.AI*")
    .AddConsoleExporter();

if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    tracerProviderBuilder.AddAzureMonitorTraceExporter(options => 
        options.ConnectionString = appInsightsConnectionString);
}

var tracerProvider = tracerProviderBuilder.Build();
builder.Services.AddSingleton(tracerProvider);

builder.Services.AddHttpClient().AddLogging();
builder.Services.AddHttpContextAccessor(); // For middleware to access current user

// Configure and register ChatClient as a service for DevUI and agent creation

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "AGUIAssistant.Telemetry", configure: (cfg) => cfg.EnableSensitiveData = true)
    .Build();

builder.Services.AddChatClient(chatClient);

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

// Register the agent using AddAIAgent with factory delegate for DevUI and AG-UI integration
builder.AddAIAgent("AGUIAssistant", (sp, name) =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var chatClient = sp.GetRequiredService<IChatClient>();
    var agent = AgenticAgent.CreateAgent(httpContextAccessor, chatClient).GetAwaiter().GetResult();
    return new OpenTelemetryAgent(agent, sourceName: "AGUIAssistant.Telemetry") { EnableSensitiveData = true };
});

builder.Services.AddAGUI();

// Add DevUI support services
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Get the registered agent from the service provider
var agent = app.Services.GetRequiredKeyedService<AIAgent>("AGUIAssistant");

// Map the AG-UI agent endpoint and require authentication
var agentEndpoint = app.MapAGUI("/agent", agent);
agentEndpoint.RequireAuthorization();

// Map DevUI endpoints for interactive testing and debugging
app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (app.Environment.IsDevelopment())
{
    app.MapDevUI();
    Console.WriteLine("DevUI is available at: http://localhost:5274/devui");
}

// Add a default route for the web server with a "Hello World" message
app.MapGet("/", () => "Hello World! Agentic Agent with AG-UI.");

await app.RunAsync();