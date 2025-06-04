var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "poc-callee v1");
    c.RoutePrefix = string.Empty;
});

app.MapGet("/ping", () => Results.Ok($"pong! {DateTime.UtcNow:o}"));

app.Run();