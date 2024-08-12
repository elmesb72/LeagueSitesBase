using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

public class MicrosoftLogin : AuthProviderLogin
{

    // https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/Overview/appId/87bddcc6-b0da-40f0-b738-65e17287e89d/isMSAApp/true

    public MicrosoftLogin(IQueryCollection qc, HttpClient hc, IConfiguration cfg, string cu) : base(qc, hc, cfg, cu) { }

    public override async Task GetProfileDataAsync()
    {
        var clientId = configuration["Authentication:Microsoft:ClientId"];
        var clientSecret = configuration["Authentication:Microsoft:ClientSecret"];
        if (clientId is null || clientSecret is null)
        {
            throw new Exception("Required 'ClientID' and/or 'ClientSecret' properties are missing for the provider in appsettings.json");
        }
        var code = query["code"];
        if (StringValues.IsNullOrEmpty(code)) {
            throw new Exception("Required 'code' parameter is missing from querystring");
        }
        accessTokenEndpointUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        accessTokenEndpointParameters.Add("client_id", clientId);
        accessTokenEndpointParameters.Add("code", code!);
        accessTokenEndpointParameters.Add("redirect_uri", callbackUrl + "/Microsoft");
        accessTokenEndpointParameters.Add("grant_type", "authorization_code");
        accessTokenEndpointParameters.Add("client_secret", clientSecret);
        accessTokenHttpMethod = HttpMethod.Post;
        profileEndpointUrl = "https://graph.microsoft.com/v1.0/me/";
        profileNameKey = "displayName";
        profileEmailKey = "userPrincipalName";
        await GetProfileData();
    }

    protected override HttpRequestMessage GenerateTokenRequest()
    {
        return new HttpRequestMessage(accessTokenHttpMethod, accessTokenEndpointUrl)
        {
            Content = new FormUrlEncodedContent(accessTokenEndpointParameters)
        };
    }

    protected override HttpRequestMessage CallEndpointWithToken(JObject accessToken)
    {
        var m = new HttpRequestMessage(profileEndpointHttpMethod, profileEndpointUrl);
        m.Headers.Add("Authorization", "Bearer " + accessToken[accessTokenKey]);
        return m;
    }

}