using Microsoft.Extensions.Primitives;

public class GoogleLogin : AuthProviderLogin
{

    // https://console.developers.google.com/apis/credentials?project=church-league-fastball

    public GoogleLogin(IQueryCollection qc, HttpClient hc, IConfiguration cfg, string cu) : base(qc, hc, cfg, cu) { }

    public override async Task GetProfileDataAsync()
    {
        var clientId = configuration["Authentication:Google:ClientId"];
        var clientSecret = configuration["Authentication:Google:ClientSecret"];
        if (clientId is null || clientSecret is null)
        {
            throw new Exception("Required 'ClientID' and/or 'ClientSecret' properties are missing for the provider in appsettings.json");
        }
        var code = query["code"];
        if (StringValues.IsNullOrEmpty(code)) {
            throw new Exception("Required 'code' parameter is missing from querystring");
        }

        accessTokenEndpointUrl = "https://oauth2.googleapis.com/token";
        accessTokenEndpointParameters.Add("client_id", clientId);
        accessTokenEndpointParameters.Add("client_secret", clientSecret);
        accessTokenEndpointParameters.Add("code", code!);
        accessTokenEndpointParameters.Add("grant_type", "authorization_code");
        accessTokenEndpointParameters.Add("redirect_uri", callbackUrl + "/Google");
        accessTokenHttpMethod = HttpMethod.Post;
        profileEndpointUrl = "https://www.googleapis.com/userinfo/v2/me?access_token=";
        await GetProfileData();
    }

}