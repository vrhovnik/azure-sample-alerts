using System.Text.Json.Serialization;

namespace AlertCall;

public class TokenData
{
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
    [JsonPropertyName("expires_in")]
    public string Expire { get; set; }
    [JsonPropertyName("expires_on")]
    public string ExpiresOn { get; set; }
    [JsonPropertyName("resource")]
    public string Resource { get; set; }
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}

