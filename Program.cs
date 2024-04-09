using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ScraperClient", client =>
{
    client.BaseAddress = new Uri("https://ocjene.skole.hr/login");
    client.DefaultRequestHeaders.Add("Accept", "application/json"); // If you expect JSON responses
    // Additional default headers can be added here
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new CookieContainer()
    };
});


// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();