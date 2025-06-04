using Azure.Core;
using Azure.Identity;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#region Create Azure Token Credential
using var listener = Azure.Core.Diagnostics.AzureEventSourceListener.CreateConsoleLogger();
builder.Services.AddSingleton<TokenCredential>(serviceProvider => {
    var currentEnv = serviceProvider.GetService<IWebHostEnvironment>();
    if (currentEnv?.IsDevelopment() == true)
    {
        // Adjust the sequence or add other credentials as needed
        return new ChainedTokenCredential(
            // new ManagedIdentityCredential(),    // Azure Virtual Desktop
            // new VisualStudioCredential(),       // Microsoft account signed in Visual Studio
            new AzureCliCredential()            // Mac user with Azure CLI signed in
        );
    }
    else
    {
        // Managed Identity for Azure environments
        if (string.IsNullOrEmpty(builder.Configuration["ManagedIdentityObjectId"]))
        {
            // system assigned managed identity
            return new ManagedIdentityCredential();
        }
        else
        {
            // user assigned managed identity
            var mi = ManagedIdentityId.FromUserAssignedObjectId(builder.Configuration["ManagedIdentityObjectId"]);
            return new ManagedIdentityCredential(mi);
        }
    }
});
#endregion

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options => {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "poc-caller v1");
    options.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();

app.MapGet("access-token", async (IConfiguration configuration, TokenCredential credential, string? scope=null) =>
{
    try
    {
        AccessToken _token = await TokenHelper.GetToken(configuration, credential, $"{scope ?? configuration["CalleeAppRegistrationId"]}");
        return Results.Ok(new
        {
            token = _token.Token,
            decoded = new JwtSecurityTokenHandler().ReadJwtToken(_token.Token)
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
              detail: ex.Message,
              statusCode: 500,
              title: "Fail to get access token",
              type: ""
          );
    }
})
.WithName("GetAccessToken")
.WithOpenApi();

app.MapGet("/remote-ping", async (IConfiguration configuration, TokenCredential credential, string? token=null, string? scope=null) => {
    try{
        if (string.IsNullOrEmpty(token))
        {
            AccessToken _token = await TokenHelper.GetToken(configuration, credential, $"{scope ?? configuration["CalleeAppRegistrationId"]}");
            token = _token.Token;
        }
        
        using var httpClient = new HttpClient()
        {
            BaseAddress = new Uri(configuration["CalleeApi"]!)
        };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var data = await httpClient.GetStringAsync("ping");
        return Results.Ok(new
        {
            data,
            token
        });
    }
    catch(Exception ex) {
          return Results.Problem(
              detail: ex.Message,
              statusCode: 500,
              title: "Fetching pinging Callee",
              type: ""
          );
    }
})
.WithName("RemotePing")
.WithOpenApi();

app.Run();

public static class TokenHelper
{
    public static async Task<AccessToken> GetToken(IConfiguration configuration, TokenCredential credential, string scope)
    {
        return await credential.GetTokenAsync(new TokenRequestContext(scopes: new[] { scope }), new CancellationTokenSource().Token);
    }
}