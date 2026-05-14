namespace TimeTrack.Api.OpsMcp;

public interface IOpsMcpAuditLogger
{
    void LogToolCall(Guid platformApiKeyId, string toolName, object? parameters);
}

public sealed class OpsMcpAuditLogger : IOpsMcpAuditLogger
{
    private readonly ILogger<OpsMcpAuditLogger> _logger;

    public OpsMcpAuditLogger(ILogger<OpsMcpAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogToolCall(Guid platformApiKeyId, string toolName, object? parameters)
    {
        _logger.LogInformation(
            "MCP tool call: {ToolName} key={PlatformApiKeyId} params={Params}",
            toolName,
            platformApiKeyId,
            parameters);
    }
}

