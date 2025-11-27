var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport() // With streamable HTTP
    .WithToolsFromAssembly(); // Add all classes marked with [McpServerToolType]


var app = builder.Build();


app.MapMcp("/mcp");

app.MapGet("/", () => "Hello World!");

app.Run();
