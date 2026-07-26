using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using meesuanruam_service.DTO;
using meesuanruam_service.services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<OrgUnitService>();

builder.Services.AddApiVersioning(options =>
{
    // Specify default API version
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("x-api-version")
    );
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "API v1", Version = "v1" });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//connection database
var connection = String.Empty;
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
    connection = builder.Configuration.GetConnectionString("DOCKER_SQL_CONNECTIONSTRING");
}
else
{
    connection = Environment.GetEnvironmentVariable("DOCKER_SQL_CONNECTIONSTRING");
}
builder.Services.AddDbContext<meeDB>(options =>
    options.UseSqlServer(connection));

// CORS: อนุญาตเฉพาะโดเมนของ อปท. ที่อยู่ในตาราง ORG_UNIT
// ห้ามใช้ AllowAnyOrigin() เพราะ endpoint สาธารณะใช้ Origin header หา อปท. เจ้าของข้อมูล
// เซ็ตถูกเติมหลัง builder.Build() แต่ delegate ถูกเรียกตอนมี request จึงทันเสมอ
var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowOrgUnitDomains",
        policy => policy.SetIsOriginAllowed(origin => allowedOrigins.Contains(origin.Trim().TrimEnd('/')))
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});


//JWT Authentication 
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

var app = builder.Build();

// โหลดโดเมนของทุก อปท. ครั้งเดียวตอน startup — ORG_UNIT เป็น single source of truth
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<meeDB>();
    foreach (var domain in db.org_unit.Select(o => o.domain_name).ToList())
    {
        allowedOrigins.Add(domain.Trim().TrimEnd('/'));
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseSwagger();
//app.UseSwaggerUI();

app.UseCors("AllowOrgUnitDomains");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
