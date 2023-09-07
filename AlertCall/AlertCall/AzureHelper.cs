using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace AlertCall;

public class AzureHelper
{
    private readonly string subscriptionId;
    private readonly string clientId;
    private readonly string secret;
    private readonly string resource;
    private readonly HttpClient client;

    public AzureHelper(string subscriptionId, string clientId, string secret, string resource)
    {
        this.subscriptionId = subscriptionId;
        this.clientId = clientId;
        this.secret = secret;
        this.resource = resource;
        client = new HttpClient();
    }

    public async Task<bool> FireAlertAsync(string accessToken, string alertRuleName, string resourceGroup,
        string vmName)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var alertRuleUrl =
            $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/metricAlerts/{alertRuleName}?api-version=2018-03-01";

        var alertRuleBody = $@"{{""properties"": {{""description"": ""Alert on CPU percentage"",""severity"": 3,
""enabled"": true,
""scopes"": [
""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{{vmName}}""
],
""evaluationFrequency"": ""PT1M"",
""windowSize"": ""PT5M"",
""criteria"": {{
""odata.type"": ""Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria"",
""allOf"": [
{{
""name"": ""High CPU percentage"",
""metricName"": ""Percentage CPU"",
""dimensions"": [],
""operator"": ""GreaterThan"",
""threshold"": 20,
""timeAggregation"": ""Average""
}}
]
}},
""actions"": [
{{
""actionGroupId"": ""/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/microsoft.insights/actionGroups/ag-email-admin""
}}
]
}}
}}";

        var content = new StringContent(alertRuleBody, Encoding.UTF8, "application/json");

        var response = await client.PutAsync(alertRuleUrl, content);

        if (response.IsSuccessStatusCode) return true;

        Console.WriteLine("Alert rule creation failed.");
        Console.WriteLine(response.ReasonPhrase);

        return false;
    }

    public async Task<string> GetTokenAsync()
    {
        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "resource", resource },
            { "client_secret", secret },
            { "redirect_uri", "https://localhost" }
        };

        var content = new FormUrlEncodedContent(formData);

        client.BaseAddress = new Uri("https://login.microsoftonline.com/");
        var responseMessage = await client.PostAsync($"{subscriptionId}/oauth2/token", content);
        if (!responseMessage.IsSuccessStatusCode) return string.Empty;

        var result = await responseMessage.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(result)) throw new Exception("Token was not received");

        var token = JsonConvert.DeserializeObject<TokenData>(result);
        if (token == null) throw new Exception("Returned data not in correct format");
        return token.AccessToken;
    }
}