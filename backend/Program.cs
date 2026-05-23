using Financeiro.Api.Repositories;
using FinanceiroApi.Data;
using FinanceiroApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 2. Configuração do Banco de Dados (MySQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 3. Autenticação Firebase
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/controle-financeiro-81997";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/controle-financeiro-81997",
            ValidateAudience = true,
            ValidAudience = "controle-financeiro-81997",
            ValidateLifetime = true
        };
    });

// 4. Registro dos Serviços
builder.Services.AddScoped<FinanceiroRepository>();
builder.Services.AddScoped<ImportacaoNotaService>();

// 5. Controllers e JSON (AJUSTADO PARA ELIMINAR O ERRO 500)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Resolve o loop infinito entre Lançamento -> Categoria -> Lançamento
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        // Opcional: Faz com que Enums apareçam como Texto no JSON (ex: "Receita" em vez de 0)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // Garante que o JSON não venha com nomes de propriedades bagunçados
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- PIPELINE ---

// CORS primeiro para evitar bloqueios de pre-flight do navegador
app.UseCors("AllowAngular");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em desenvolvimento local, se não tiver certificado SSL, pode comentar esta linha:
// app.UseHttpsRedirection(); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();