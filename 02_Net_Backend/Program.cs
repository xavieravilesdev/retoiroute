using System.Text.Json.Serialization;
using Commerce.Api.Business;
using Commerce.Api.Extensions;
using Commerce.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Enums como strings (requisito de la guía para la generación del cliente con NSwag)
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCommerceServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCommerceRateLimiting();
builder.Services.AddCommerceCors(builder.Configuration);
builder.Services.AddSwaggerWithJwt();
builder.Services.AddExceptionHandler<bcGlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// Cabeceras básicas de seguridad (OWASP)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await stDbSeeder.SeedAdminAsync(app.Services);

app.Run();
