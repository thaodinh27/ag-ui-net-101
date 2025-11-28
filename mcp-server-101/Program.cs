using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add controllers so our EchoTool controller endpoints are reachable
builder.Services.AddControllers();

// Enable permissive CORS for testing (allow any origin/header/method)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});


// Register JWT Bearer authentication with your token's issuer and audience
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    // No Authority for PoC to avoid OIDC discovery; token will be read from Authorization header
    options.RequireHttpsMetadata = false; // Allow HTTP for local development
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
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
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
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
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
           // WARNING: The following disables ALL token validations and is ONLY for local testing / PoC.
           ValidateIssuerSigningKey = false,
           ValidateIssuer = false,
           ValidateAudience = false,
           ValidateLifetime = false,
           // Provide a signature validator that returns the token as a JsonWebToken
           // This avoids the signature validation error by returning the expected type for the validator.
           SignatureValidator = (token, parameters) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token),
    };
});

var mcpBuilder = builder.Services.AddMcpServer()
    .WithHttpTransport() // With streamable HTTP
    .WithToolsFromAssembly(); // Add all classes marked with [McpServerToolType]

// Configure MCP to use ASP.NET authorization filters so tools with [Authorize] are respected
// mcpBuilder.AddAuthorizationFilters();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Use the permissive CORS policy for testing
app.UseCors("AllowAll");

app.MapMcp("/mcp").RequireAuthorization();

app.MapGet("/", () => "Hello World!");

app.Run();
