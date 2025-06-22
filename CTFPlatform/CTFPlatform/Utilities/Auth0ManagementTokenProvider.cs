using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;

namespace CTFPlatform.Utilities;

public class Auth0ManagementTokenProvider(IConfiguration config, IHttpClientFactory httpClientFactory)
{
    private ManagementToken? _token;
    
    public async Task<ManagementToken> GetToken()
    {
        if (_token == null || _token.Expiry <= DateTimeOffset.Now)
            _token = await FetchNewToken();

        return _token;
    }
    
    private async Task<ManagementToken> FetchNewToken()
    {
        var form = new FormUrlEncodedContent( new List<KeyValuePair<string, string>>
        {
            new("client_id", config["Auth0:ClientId"]!),
            new("client_secret", config["Auth0:ClientSecret"]!),
            new("audience", $"https://{config["Auth0:Domain"]}/api/v2/"),
            new("grant_type", "client_credentials")
        });

        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync($"https://{config["Auth0:Domain"]}/oauth/token", form);
        var token = await response.Content.ReadFromJsonAsync<ManagementTokenResponse>();
        if (token == null)
            throw new ConfigurationErrorsException("Unable to obtain management token.");
            
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token.Token);
        var tokenS = jsonToken as JwtSecurityToken;
        
        var managementToken = new ManagementToken()
        {
            Token = token.Token,
            Expiry = tokenS?.ValidTo ?? DateTime.Now.AddHours(23),
        };
        return managementToken;
    }

    private class ManagementTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string Token { get; set; } = string.Empty;
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}
    
public class ManagementToken
{
    public string Token { get; set; }
    public DateTime Expiry { get; set; }
}