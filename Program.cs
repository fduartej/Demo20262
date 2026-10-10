using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.OpenApi;
using _20262.Data;
using _20262.Integrations;
using _20262.Integrations.Algolia;
using _20262.Models.Entities;
using _20262.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Algolia search integration.
builder.Services.Configure<AlgoliaOptions>(builder.Configuration.GetSection(AlgoliaOptions.SectionName));
builder.Services.AddScoped<IAlgoliaSearchService, AlgoliaSearchService>();
builder.Services.AddScoped<IAlgoliaIndexService, AlgoliaIndexService>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mundo Mascota API",
        Version = "v1",
        Description = "API pública del catálogo de productos. Las operaciones de escritura (POST, PUT, DELETE) requieren autenticación mediante la cookie de sesión de Identity."
    });
});

// Caché distribuida (Redis) para el catálogo de productos.
builder.Services.Configure<CatalogCacheOptions>(builder.Configuration.GetSection(CatalogCacheOptions.SectionName));
builder.Services.Configure<PieSocketOptions>(builder.Configuration.GetSection(PieSocketOptions.SectionName));

// Análisis de sentimiento de los mensajes de contacto (ML.NET): singleton porque el modelo se entrena una vez.
builder.Services.Configure<SentimentOptions>(builder.Configuration.GetSection(SentimentOptions.SectionName));
builder.Services.AddSingleton<ISentimentAnalysisService, SentimentAnalysisService>();

// Recomendación de productos por factorización de matrices (ML.NET): singleton porque el modelo se entrena una vez.
builder.Services.Configure<RecommendationOptions>(builder.Configuration.GetSection(RecommendationOptions.SectionName));
builder.Services.AddSingleton<IRecommendationService, RecommendationService>();

builder.Services.AddHttpClient();
builder.Services.AddScoped<PieSocketService>();

// Cola de pedidos (CloudAMQP): publicador + consumidor desacoplado.
builder.Services.Configure<CloudAmqpOptions>(builder.Configuration.GetSection(CloudAmqpOptions.SectionName));
builder.Services.AddSingleton<IOrdenPublisher, OrdenPublisher>();
builder.Services.AddHostedService<ConsumidorOrdenesService>();

var redisConfig = builder.Configuration.GetSection("Redis");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfig["ConnectionString"];
    options.InstanceName = redisConfig["InstanceName"];
});

builder.Services.AddScoped<ProductoService>();

// Integración con la API pública de dummyjson (listas de tareas).
builder.Services.AddHttpClient<ITodoClient, TodoClient>(client =>
{
    client.BaseAddress = new Uri("https://dummyjson.com/");
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
        // El proyecto no usa migraciones EF: el esquema se ajusta con DDL idempotente al arrancar.
        // Sin esta excepción, Migrate() aborta el arranque cuando el modelo cambia sin migración nueva.
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Crea/aplica la base de datos pets.db (code first) con las migraciones si no existen.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Asegura la tabla de pedidos registrados (DDL idempotente; el proyecto no usa migraciones EF).
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS t_pedidos_registrados (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            OrdenId TEXT NOT NULL,
            ProductoId INTEGER NOT NULL,
            NombreProducto TEXT NOT NULL,
            Precio TEXT NOT NULL,
            Cantidad INTEGER NOT NULL,
            Total TEXT NOT NULL,
            CreadoEn TEXT NOT NULL,
            RegistradoEn TEXT NOT NULL,
            Estado TEXT NOT NULL
        );
        """);

    // Agrega las columnas de sentimiento a t_contactos (SQLite no soporta ADD COLUMN IF NOT EXISTS).
    var columnasContacto = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var conexion = db.Database.GetDbConnection();
    var conexionPrevia = conexion.State == ConnectionState.Open;
    if (!conexionPrevia)
    {
        await conexion.OpenAsync();
    }

    await using (var comando = conexion.CreateCommand())
    {
        comando.CommandText = "PRAGMA table_info(t_contactos);";
        await using var reader = await comando.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columnasContacto.Add(reader.GetString(1));
        }
    }

    if (!conexionPrevia)
    {
        await conexion.CloseAsync();
    }

    if (!columnasContacto.Contains(nameof(Contacto.Sentimiento)))
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE t_contactos ADD COLUMN Sentimiento TEXT NULL;");
    }

    if (!columnasContacto.Contains(nameof(Contacto.ProbabilidadSentimiento)))
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE t_contactos ADD COLUMN ProbabilidadSentimiento REAL NULL;");
    }

    // Crea el usuario administrador por defecto si no existe.
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string adminEmail = "admin@mundomascota.com";
    const string adminPassword = "Admin123";

    var admin = await userManager.FindByEmailAsync(adminEmail);
    if (admin == null)
    {
        admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail };
        await userManager.CreateAsync(admin, adminPassword);
    }

    // Asegura el rol "Admin" y lo asigna al administrador.
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
    if (!await userManager.IsInRoleAsync(admin, "Admin"))
    {
        await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Precarga el catálogo en Redis en el arranque (si WarmupOnStartup está activo).
    var productoService = scope.ServiceProvider.GetRequiredService<ProductoService>();
    await productoService.PrecalentarCacheAsync();

    // Carga (o entrena la primera vez) el modelo de análisis de sentimiento de ML.NET.
    var sentimentService = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();
    if (sentimentService.EstaHabilitado)
    {
        await sentimentService.InicializarAsync();
    }

    // Carga (o entrena la primera vez) el modelo de recomendación de productos de ML.NET.
    var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
    if (recommendationService.EstaHabilitado)
    {
        await recommendationService.InicializarAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mundo Mascota API v1"));
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
