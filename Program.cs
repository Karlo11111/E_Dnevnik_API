using E_Dnevnik_API.ScrapingServices;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDistributedMemoryCache(); // Adds a default in-memory implementation of IDistributedCache
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Use a short timeout for testing purposes
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Make the session cookie essential
});

// Existing HttpClient configuration for ScraperClient
builder.Services.AddHttpClient("ScraperClient", client =>
{
    client.BaseAddress = new Uri("https://ocjene.skole.hr/login");
    client.DefaultRequestHeaders.Add("Accept", "application/json"); // If you expect JSON responses
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = true,
    CookieContainer = new CookieContainer()
});

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
app.UseSession(); // Add this before UseRouting or MapControllers
app.MapControllers();
app.Run();
