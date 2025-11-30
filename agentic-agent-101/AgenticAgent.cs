using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenAI.Chat;
using dotenv.net;


namespace agentic_agent_101;

public static class AgenticAgent
{
    private static readonly Dictionary<string, string> envVars = new Dictionary<string, string>(DotEnv.Read());
    
    public static async Task<AIAgent> CreateAgent(
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null,
        IChatClient? chatClient = null)
    {
        // Use provided chatClient or create a new one (fallback for backward compatibility)
        if (chatClient == null)
        {
            var endpoint = envVars["AZURE_OPENAI_ENDPOINT_URL"];
            var key = envVars["AZURE_OPENAI_KEY"];
            var deploymentName = "gpt-5-chat";
            chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key))
                .GetChatClient(deploymentName)
                .AsIChatClient();
        }

        // Collect dynamic MCP tools that will use the current user's token from httpContextAccessor
        var tools = await CollectDynamicTools(httpContextAccessor);
        
        AIAgent agent = chatClient.CreateAIAgent(
            name: "AGUIAssistant",
            instructions: "You are a helpful assistant.",
            tools: tools);

        // Apply function-calling middleware to log tool invocations
        var middlewareAgent = agent.AsBuilder()
                .Use((agentParam, context, next, cancellationToken) => 
                    CustomFunctionCallingMiddleware(agentParam, context, next, httpContextAccessor, cancellationToken))
                .Build();

        return middlewareAgent;
    }

    private static async ValueTask<object?> CustomFunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var functionName = context.Function.Name;
        Console.WriteLine($"[Middleware] Invoking tool: {functionName}");

        // Extract user's bearer token from current HTTP context (if available)
        var rawToken = httpContextAccessor?.HttpContext?.User?.FindFirst("raw_token")?.Value;
        
        if (!string.IsNullOrEmpty(rawToken))
        {
            Console.WriteLine($"[Middleware] User token present for: {functionName}");
        }
        else
        {
            Console.WriteLine($"[Middleware] No user token for: {functionName}");
        }

        var result = await next(context, cancellationToken);
        Console.WriteLine($"[Middleware] Tool completed: {functionName}");
        return result;
    }

    /// <summary>
    /// Creates dynamic MCP tool wrappers that use the current user's bearer token on each invocation
    /// </summary>
    public static async Task<List<AITool>> CollectDynamicTools(Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor)
    {
        List<AITool> tools = new List<AITool>();

        // Get tool schemas from MCP server (using default token just for listing)
        var tempMcpClient = await CreateMcpClient(envVars["WEBAPP_MCP_SERVER_URL"], envVars["WEBAPP_MCP_BEARER_TOKEN"]);
        var mcpToolSchemas = await tempMcpClient.ListToolsAsync();

        // Create wrapper tools that will use current user's token when invoked
        foreach (var toolSchema in mcpToolSchemas)
        {
            // Create a dynamic wrapper function that recreates MCP client with user token
            var wrappedTool = AIFunctionFactory.Create(
                async (IDictionary<string, object?> arguments) =>
                {
                    // Get current user's token from HttpContext
                    var userToken = httpContextAccessor?.HttpContext?.User?.FindFirst("raw_token")?.Value;
                    var authToken = !string.IsNullOrEmpty(userToken) 
                        ? $"Bearer {userToken}" 
                        : envVars["WEBAPP_MCP_BEARER_TOKEN"];

                    Console.WriteLine($"[Dynamic Tool] Creating MCP client for {toolSchema.Name} with user token");

                    // Create fresh MCP client with current user's authorization token
                    var mcpClient = await CreateMcpClient(envVars["WEBAPP_MCP_SERVER_URL"], authToken);
                    
                    // Invoke the actual MCP tool
                    var readOnlyArgs = arguments as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(arguments);
                    var result = await mcpClient.CallToolAsync(toolSchema.Name, readOnlyArgs);
                    return result;
                },
                name: toolSchema.Name,
                description: toolSchema.Description
            );

            tools.Add(wrappedTool);
        }

        // Add GitHub MCP tools (using PAT token, not user token)
        var githubMcpClient = await CreateMcpClient(
            envVars["GITHUB_MCP_SERVER_URL"], 
            envVars["GITHUB_PAT_TOKEN"]);
        var githubTools = await githubMcpClient.ListToolsAsync();
        tools.AddRange(githubTools);

        return tools;
    }

    /// <summary>
    /// Helper to create MCP client with specified authorization token
    /// </summary>
    private static async Task<McpClient> CreateMcpClient(string endpoint, string authToken)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint),
            Name = "MCP Client",
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>()
            {
                {"Authorization", authToken }
            }
        };
        return await McpClient.CreateAsync(new HttpClientTransport(options));
    }
}
