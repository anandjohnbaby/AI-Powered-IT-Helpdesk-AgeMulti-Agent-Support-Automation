namespace Helpdesk.Application.Common;

public class LlmOptions
{
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";
    public string Model { get; set; } = "llama-3.3-70b-versatile";
    public string ApiKey { get; set; } = "";
}

public class HelpdeskOptions
{
    public int ApprovalExpiryMinutes { get; set; } = 30;
    public string AccountConsoleUrl { get; set; } = "http://localhost:8080/realms/helpdesk/account";
}

public class JwtOptions
{
    public string Issuer { get; set; } = "helpdesk-api";
    public string Audience { get; set; } = "helpdesk-app";
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}
