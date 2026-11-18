using Financeiro.Api.Repositories;
using FinanceiroApi.Data;
using FinanceiroApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

Environment.SetEnvironmentVariable("TZ", "America/Sao_Paulo");

var builder = WebApplication.CreateBuilder(args);

// Configuração explícita do Kestrel para rodar estritamente em HTTP na porta 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("MinhaPoliticaCors", policy =>
    {
        policy.WithOrigins(
                 "https://comprasinteligente.com.br",
        "https://www.comprasinteligente.com.br",
        "http://localhost",
        "http://localhost:80",
        "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Connection String tratada e blindada para evitar conflitos de driver com o Dapper/MySql.Data
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connectionString = rawConnectionString;

if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
{
    connectionString = connectionString.TrimEnd(';') + ";SslMode=Preferred;";
}


// Configuração do EF Core com MySQL 8.0
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 0)),
        mysql =>
        {
            mysql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));


// Fábrica de contextos para operações transacionais independentes
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 0)),
        mysql =>
        {
            mysql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }),
    ServiceLifetime.Scoped);

// Autenticação Firebase
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/controle-financeiro-81997";
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var uid = context.Principal?.FindFirst("sub")?.Value
                    ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst("user_id")?.Value;

                if (string.IsNullOrWhiteSpace(uid))
                    context.Fail("UID do Firebase ausente no token.");

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/controle-financeiro-81997",
            ValidateAudience = true,
            ValidAudience = "controle-financeiro-81997",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<FinanceiroRepository>();
builder.Services.AddScoped<ImportacaoNotaService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Financeiro.Api", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Informe o token JWT: Bearer {seu token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Descomente apenas se for usar HTTPS no Docker
app.UseRouting();
app.UseCors("MinhaPoliticaCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Bloco de inicialização e validação do banco de dados
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    try
//    {
//        Console.WriteLine("Aguardando banco de dados...");
//        Thread.Sleep(8000); // Espera o MySQL estabilizar no container

//        var context = services.GetRequiredService<AppDbContext>();

//        if (context.Database.EnsureCreated())
//        {
//            Console.WriteLine("Banco de dados criado com sucesso!");
//        }
//        else
//        {
//            Console.WriteLine("Banco de dados já existente, validando tabelas...");
//        }
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine("ERRO CRÍTICO AO INICIAR BANCO:");
//        Console.WriteLine(ex.Message);
//        if (ex.InnerException != null) Console.WriteLine(ex.InnerException.Message);
//    }
//}

app.Run();