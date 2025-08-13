var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Add these three lines ↓↓↓
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<
    BuildAndBuy.Web.Services.Abstractions.IAiService,
    BuildAndBuy.Web.Services.Implementations.GeminiAiService>();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
