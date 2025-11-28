using ModelContextProtocol.Server;
using System.ComponentModel;

namespace mcp_server_101;

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message)
    {
        var msg = string.IsNullOrEmpty(message) ? "" : $" {message}";
        return $"anonymous: hello{msg}";
    }

    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string EchoPost(string message)
    {
        return Echo(message);
    }
}
