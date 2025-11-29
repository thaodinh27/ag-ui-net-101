// Copyright (c) Microsoft. All rights reserved.
using agentic_agent_101;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using dotenv.net;

DotEnv.Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
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
// Map the AG-UI agent endpoint
app.MapAGUI("/agent", agent);

// Add a default route for the web server with a "Hello World" message
app.MapGet("/", () => "Hello World! Agentic Agent with AG-UI.");

await app.RunAsync();