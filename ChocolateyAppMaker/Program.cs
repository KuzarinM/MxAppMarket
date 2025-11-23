using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Managers.Implementations;
using ChocolateyAppMaker.Managers.Interfaces;
using ChocolateyAppMaker.Repositories.Implementations;
using ChocolateyAppMaker.Repositories.Interfaces;
using ChocolateyAppMaker.Services.Background;
using ChocolateyAppMaker.Services.Implementations;
using ChocolateyAppMaker.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders; // Нужно для StaticFileOptions

var builder = WebApplication.CreateBuilder(args);

// --- 1. НАСТРОЙКА ПУТЕЙ (Самое важное для Docker) ---
var appDataPath = Environment.GetEnvironmentVariable("APP_DATA_PATH")
                  ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");

// ВАЖНО: Добавляем "packages" в физический путь
var imagesPath = Path.Combine(appDataPath, "images", "packages");

Directory.CreateDirectory(appDataPath);
Directory.CreateDirectory(imagesPath); // Создаст App_Data/images/packages

// Обновляем конфиг, чтобы ImageDownloaderService тоже сохранял сразу в эту папку
builder.Configuration["AppConfig:AppDataPath"] = appDataPath;
builder.Configuration["AppConfig:ImagesPhysicalPath"] = imagesPath;

// --- 2. БД (Кладем рядом с картинками) ---
var dbPath = Path.Combine(appDataPath, "packages.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ... (Identity и Authorization оставляем как были) ...
builder.Services.AddRazorPages();
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});
builder.Services.AddAuthorization(options => {
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Репозитории
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IInstallerRepository, InstallerRepository>();
builder.Services.AddScoped<ITagsRepository, TagsRepository>();

// Сервисы
builder.Services.AddHttpClient<IChocoMetadataService, MetadataAggregatorService>();
builder.Services.AddSingleton<IScanManager, ScanManager>();
builder.Services.AddHostedService<ScanBackgroundService>();
builder.Services.AddScoped<IScannerService, ScannerService>();
// BuilderService если нужен, тоже можно настроить на appDataPath
builder.Services.AddScoped<IChocolateyBuilderService, ChocolateyBuilderService>();
builder.Services.AddHttpClient<IImageDownloaderService, ImageDownloaderService>();
builder.Services.AddHttpClient<ITranslationService, TranslationService>();

var app = builder.Build();

// Инициализация БД
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    // Важно: EnsureCreated создаст файл packages.db в нашей папке
    context.Database.EnsureCreated();
    await IdentitySeedData.Initialize(services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// --- 3. РАЗДАЧА СТАТИКИ ---
// Стандартная папка wwwroot (для css/js)
app.UseStaticFiles();

// ДОПОЛНИТЕЛЬНАЯ папка для картинок (из нашего Volume)
app.UseStaticFiles(new StaticFileOptions
{
    // Теперь FileProvider смотрит прямо в App_Data/images/packages
    FileProvider = new PhysicalFileProvider(imagesPath),

    // А браузер запрашивает /images/packages/...
    // Сервер отрежет "/images/packages", получит "/logitechhub/..."
    // И найдет его внутри папки imagesPath
    RequestPath = "/images/packages"
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();