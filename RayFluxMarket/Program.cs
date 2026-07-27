using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RayFluxMarket.Data;
using RayFluxMarket.Services;
using Serilog;
using Stripe;
using System.Text;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Подключаем Serilog, заставляя его читать настройки из appsettings.json
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Эта строка магическим образом решает проблему циклов
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;

        // По желанию: сделаем JSON красивым (с отступами)
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Настраиваем политику CORS, разрешая доступ для будущих фронтенд-приложений
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // 3000 - стандартный порт React, 5173 - стандартный порт Vite (Vue/React)
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()  // Разрешаем любые заголовки (включая Authorization с токеном)
              .AllowAnyMethod(); // Разрешаем любые методы (GET, POST, PUT, DELETE)
    });
});
builder.Services.AddMemoryCache(); // Включаем поддержку кэширования в оперативной памяти

// Регистрация всех валидаторов, которые есть в нашей сборке
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Включение автоматической валидации (если данные кривые, сервер сам вернет 400 Bad Request)
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAuthorization(); // Включаем поддержку авторизации
builder.Services.AddExceptionHandler<RayFluxMarket.Infrastructure.GlobalExceptionHandler>();// Подключаем глобальный обработчик ошибок
builder.Services.AddProblemDetails();// Подключаем поддержку ProblemDetails для более красивых ошибок

// Подключаем Stripe, считывая секретный ключ из appsettings.json
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"]; 


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
// 1. Добавляем сервисы для генерации Swagger

//builder.Services.AddSwaggerGen();
// Заменяем обычный builder.Services.AddSwaggerGen(); на этот блок:
// Добавляем сервисы для генерации Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RayFluxMarket API", Version = "v1" });

    // 1. Описываем, как именно Swagger должен передавать токен
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Введите токен в формате: Bearer {твой_токен}",
        Name = "Authorization", // Заголовок, в который уйдет токен
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Делаем так, чтобы защита требовалась для всех эндпоинтов
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

//builder.Services.AddOpenApi(); тоже самое, что и builder.Services.AddSwaggerGen();, но с более удобным синтаксисом. Но походу нахерена не нужен, так как мы уже подключили SwaggerGen выше. Поэтому закомментируем его.

// Подключаем базу данных PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFileService, RayFluxMarket.Services.FileService>();// Добавляем сервис для работы с файлами (загрузка изображений и т.д.)
builder.Services.AddScoped<IEmailService, EmailService>();// Добавляем сервис для отправки email3
builder.Services.AddScoped<IPaymentService, PaymentService>();// Добавляем сервис для работы с платежами (Stripe)

var app = builder.Build();

// --- БЛОК АВТОМАТИЧЕСКОГО ЗАПОЛНЕНИЯ БАЗЫ (SEEDING) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // Вызываем наш метод
        DbSeeder.Seed(context);
    }
    catch (Exception ex)
    {
        // Если что-то пойдет не так (например, сервер БД выключен), запишем это в наш Serilog
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Произошла ошибка во время Seeding базы данных.");
    }
}
app.UseSerilogRequestLogging();

app.UseExceptionHandler(); // Включает глобальный перехватчик ошибок

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // Без параметров он ищет страницу по адресу /swagger/index.html
    //app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Открывает папку wwwroot для веба
// Применяем политику CORS
app.UseCors("AllowFrontend");// Включаем поддержку аутентификации и авторизации
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
