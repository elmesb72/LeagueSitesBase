using Microsoft.Extensions.Primitives;

public class FacebookLogin : AuthProviderLogin
{

    // https://developers.facebook.com/apps/515903335765782/dashboard/

    public FacebookLogin(IQueryCollection qc, HttpClient hc, IConfiguration cfg, string cu) : base(qc, hc, cfg, cu) { }

    public override async Task GetProfileDataAsync()
    {
        var clientId = configuration["Authentication:Facebook:ClientId"];
        var clientSecret = configuration["Authentication:Facebook:ClientSecret"];
        if (clientId is null || clientSecret is null)
        {
            throw new Exception("Required 'ClientID' and/or 'ClientSecret' properties are missing for the provider in appsettings.json");
        }
        var code = query["code"];
        if (StringValues.IsNullOrEmpty(code)) {
            throw new Exception("Required 'code' parameter is missing from querystring");
        }
        accessTokenEndpointUrl = "https://graph.facebook.com/v6.0/oauth/access_token";
        accessTokenEndpointParameters.Add("client_id", clientId);
        accessTokenEndpointParameters.Add("redirect_uri", callbackUrl + "/Facebook");
        accessTokenEndpointParameters.Add("client_secret", clientSecret);
        accessTokenEndpointParameters.Add("code", code!);
        profileEndpointUrl = "https://graph.facebook.com/v6.0/me?fields=name,email&access_token=";
        profileEndpointHttpMethod = HttpMethod.Post;
        await GetProfileData();
    }

}