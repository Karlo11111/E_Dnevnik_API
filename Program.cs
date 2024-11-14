using E_Dnevnik_API.ScrapingServices;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 3333 for all network interfaces
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5168); // Bind to all network interfaces on port 3333
});

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Existing HttpClient configuration for ScraperClient
builder.Services.AddHttpClient("ScraperClient", client =>
{
    client.BaseAddress = new Uri("https://ocjene.skole.hr/login");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
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
app.UseSession();
app.MapControllers();
app.Run();
