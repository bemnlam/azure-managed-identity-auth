using System.IdentityModel.Tokens.Jwt;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Azure
builder.Services.AddSingleton<TokenCredential>(serviceProvider => {
    var webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();
    if(webHostEnvironment.IsDevelopment()) {
        return new EnvironmentCredential();   
    }
    var configuration = serviceProvider.GetService<IConfiguration>();
    var managedIdentity = configuration["ManagedIdentity"];
    return !string.IsNullOrEmpty(managedIdentity) ?
        new ManagedIdentityCredential(clientId: managedIdentity):
        new ManagedIdentityCredential();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "poc-caller v1");
        options.RoutePrefix = string.Empty; // Set Swagger UI to root
    });
// }

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforcast-with-token", async (IConfiguration configuration, string token) => {
    var requestUri = $"{configuration["CalleeApi"]}/weatherforecast";
    try{
        using var httpClient = new HttpClient();
        if(!string.IsNullOrEmpty(token)) {
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var data = await httpClient.GetFromJsonAsync<IEnumerable<WeatherForecast>>(requestUri);
        return new ApiResponse(data ?? [], requestUri, token, null);
    }
    catch(Exception ex) {
          return new ApiResponse([], requestUri, token, ex.Message);
    }
});

app.MapGet("/weatherforecast", async (IConfiguration configuration, TokenCredential credential, string? scope) =>
{
    var requestUri = $"{configuration["CalleeApi"]}/weatherforecast";
    AccessToken token = default;
    try{
        token = await TokenHelper.GetToken(configuration, credential, scope);

        using var httpClient = new HttpClient();
        if(!string.IsNullOrEmpty(token.Token)) {
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }

        var data = await httpClient.GetFromJsonAsync<IEnumerable<WeatherForecast>>(requestUri);
        return new ApiResponse(data ?? [], requestUri, token.Token, null);
    }
    catch(Exception ex) {
          return new ApiResponse([], requestUri, token.Token, ex.Message);
    }
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/token", async (IConfiguration configuration, TokenCredential credential, string? scope="api://dea507c6-c064-4d11-9311-9cd3ebb61804/.default") => 
{
    AccessToken token = default;
    try{
        token = await TokenHelper.GetToken(configuration, credential, scope);
        return new ApiResponse([], string.Empty, token.Token, string.Empty);
    } catch(Exception ex) {
        return new ApiResponse([], string.Empty, token.Token, ex.Message);
    }
})
.WithName("GetToken")
.WithOpenApi();

app.MapGet("/ping", () => {
    return new {status = "pong"};
}).WithName("GetPing")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
record ApiResponse(IEnumerable<WeatherForecast> data, string requestUri, string? token, string? ex) {
    public JwtSecurityToken? jwt => string.IsNullOrEmpty(token) ? default : new JwtSecurityTokenHandler().ReadJwtToken(token);
}

public static class TokenHelper
{
    public static async Task<AccessToken> GetToken(IConfiguration configuration, TokenCredential credential, string? scope)
    {
        return await credential.GetTokenAsync(new TokenRequestContext(scopes: new[] { scope ?? configuration["DefaultScope"] }), new CancellationTokenSource().Token);

    }
}