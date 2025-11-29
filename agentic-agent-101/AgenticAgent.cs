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
    public static async Task<AIAgent> CreateAgent()
    {
        var endpoint = envVars["AZURE_OPENAI_ENDPOINT_URL"];
        var key = envVars["AZURE_OPENAI_KEY"];
        var deploymentName = "gpt-5-chat";
        ChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key))
            .GetChatClient(deploymentName);


        var tools = await CollectTools();
        AIAgent agent = chatClient.AsIChatClient().CreateAIAgent(
            name: "AGUIAssistant",
            instructions: "You are a helpful assistant.",
            tools: tools);

        var middleAgent = agent.AsBuilder()
                //.Use(CustomAgentRunMiddleware)
                //.Use(CustomFunctionCallingMiddleware)
                .Build();


        return agent;
    }

    //public static 


    public static async Task<List<AITool>> CollectTools()
    {
        var httpClientTransportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(envVars["WEBAPP_MCP_SERVER_URL"]),
            Name = "MCP Client",
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>() {
                    {"Authorization", envVars["WEBAPP_MCP_BEARER_TOKEN"] }
                }
        };
        List<AITool> tools = new List<AITool>();
        McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(httpClientTransportOptions));
        var externalTools = await mcpClient.ListToolsAsync();
        tools.AddRange(externalTools);

        // Azure Functions: MCP server

        //var httpClientTransportOptions2 = new HttpClientTransportOptions
        //{
        //    Endpoint = new Uri("https://func-api-d52wwmub64tae.azurewebsites.net/runtime/webhooks/mcp"),
        //    Name = "MCP Client",
        //    TransportMode = HttpTransportMode.StreamableHttp,
        //    AdditionalHeaders = new Dictionary<string, string>
        //    {
        //        {
        //            "x-functions-key", "REDACTED"
        //        }
        //    }
        //};

        //var mcpClient2 = await McpClient.CreateAsync(new HttpClientTransport(httpClientTransportOptions2));
        //var externalTools2 = await mcpClient2.ListToolsAsync();
        //tools.AddRange(externalTools2);

        var httpClientTransportOptionsGitHub = new HttpClientTransportOptions
        {
            Endpoint = new Uri(envVars["GITHUB_MCP_SERVER_URL"]),
            Name = "GitHub MCP",
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>()
            {
                { "Authorization", envVars["GITHUB_PAT_TOKEN"] }
            }
        };        

        McpClient githubMcpClient = await McpClient.CreateAsync(new HttpClientTransport(httpClientTransportOptionsGitHub));
        var externalToolsGithub = await githubMcpClient.ListToolsAsync();
        tools.AddRange(externalToolsGithub);

        return tools;
    }


}

