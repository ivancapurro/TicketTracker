using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using TicketTracker.Api.Data;
using TicketTracker.Api.Errors;
using TicketTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState,
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.",
                detail: "The request is invalid. See the errors for details.",
                instance: context.HttpContext.Request.Path);

            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" },
            };
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticket Tracker API",
        Version = "v1",
        Description = "Baseline Jira-style ticket tracker for coding-agent benchmarks.",
    });
    options.SupportNonNullableReferenceTypes();
});

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    };
});

builder.Services.AddDbContext<TicketTrackerDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var environment = services.GetRequiredService<IHostEnvironment>();
    var connectionString = configuration.GetConnectionString("TicketTracker")
        ?? throw new InvalidOperationException("Connection string 'TicketTracker' is not configured.");

    options.UseSqlite(SqlitePaths.Prepare(connectionString, environment.ContentRootPath));
});

builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var http = statusCodeContext.HttpContext;
    if (http.Response.HasStarted)
    {
        return;
    }

    var factory = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
    var problem = factory.CreateProblemDetails(http, statusCode: http.Response.StatusCode, instance: http.Request.Path);
    var writer = http.RequestServices.GetRequiredService<IProblemDetailsService>();
    await writer.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = http,
        ProblemDetails = problem,
    });
});

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticket Tracker API v1");
        options.DocumentTitle = "Ticket Tracker API";
    });

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketTrackerDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, includeSampleData: app.Environment.IsDevelopment());
}

app.Logger.LogInformation("Ticket Tracker API ready");
app.Run();

public partial class Program;
