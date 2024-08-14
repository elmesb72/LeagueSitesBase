using Newtonsoft.Json.Linq;

public abstract class AuthProviderLogin(IQueryCollection qc, HttpClient hc, IConfiguration cfg, string cu) : IAuthProviderLogin
{

    public SocialUserProfile? Profile { get; set; }
    public Dictionary<string, string>? Errors { get; set; }

    protected string queryStringAuthCodeKey = "code";
    protected string accessTokenEndpointUrl = string.Empty;
    protected Dictionary<string, string> accessTokenEndpointParameters = [];
    protected HttpMethod accessTokenHttpMethod = HttpMethod.Get;
    protected string accessTokenKey = "access_token";
    protected string profileEndpointUrl = string.Empty;
    protected HttpMethod profileEndpointHttpMethod = HttpMethod.Get;
    protected string profileNameKey = "name";
    protected string profileEmailKey = "email";

    protected readonly IQueryCollection query = qc;
    protected readonly HttpClient client = hc;
    protected readonly IConfiguration configuration = cfg;
    protected readonly string callbackUrl = cu;

    protected virtual HttpRequestMessage GenerateTokenRequest() => new(accessTokenHttpMethod, accessTokenEndpointUrl + "?" + string.Join('&', accessTokenEndpointParameters.Select(p => p.Key + "=" + p.Value)));

    protected virtual HttpRequestMessage CallEndpointWithToken(JObject accessToken) => new(profileEndpointHttpMethod, profileEndpointUrl + accessToken[accessTokenKey]);

    protected async Task GetProfileData()
    {
        if (query.ContainsKey(queryStringAuthCodeKey))
        { // access granted
            Console.WriteLine(accessTokenEndpointUrl + "?" + string.Join('&', accessTokenEndpointParameters.Select(p => p.Key + "=" + p.Value)));
            // Get access token using endpoint
            var response = await client.SendAsync(GenerateTokenRequest());

            if (response.IsSuccessStatusCode)
            {
                // Parse token into object
                var accessToken = JObject.Parse(await response.Content.ReadAsStringAsync());

                // Get profile data using token
                var profileResponse = await client.SendAsync(CallEndpointWithToken(accessToken));

                if (profileResponse.IsSuccessStatusCode)
                {
                    var profile = JObject.Parse(await profileResponse.Content.ReadAsStringAsync());
                    Profile = new SocialUserProfile(
                        (string?)profile[profileNameKey] ?? string.Empty, 
                        (string?)profile[profileEmailKey] ?? string.Empty
                    );
                }
                else
                {
                    Errors = new Dictionary<string, string>() {
                        { "token", await profileResponse.Content.ReadAsStringAsync() }
                    };
                }
            }
            else
            {
                Errors = new Dictionary<string, string>() {
                    { "code", await response.Content.ReadAsStringAsync() }
                };
            }
        }
        else
        {
            Errors = new Dictionary<string, string>() {
                    { "user", string.Empty }
                };
        }
    }

    public abstract Task GetProfileDataAsync();

    public bool WasSuccessful()
    {
        return Errors == null && Profile != null;
    }

}
