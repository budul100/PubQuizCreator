using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;
using PubQuizCreator.Services;

internal class Program
{
    #region Private Methods

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddScoped<QuizService>();
        builder.Services
            .AddScoped<CategoryService>();
        builder.Services
            .AddScoped<TemplateService>();
        builder.Services
            .AddScoped<QuestionService>();
        builder.Services
            .AddScoped<IdeaService>();
        builder.Services
            .AddScoped<StateService>();
        builder.Services
            .AddScoped<PrintService>();

        builder.Services
            .AddRazorPages();
        builder.Services
            .AddServerSideBlazor();

        builder.Services
            .AddDbContext<AppDbContext>(options => options.UseNpgsql(
                connectionString: builder.Configuration.GetConnectionString("Default"),
                npgsqlOptionsAction: o => o.UseVector()));

        var uriString = builder.Configuration["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException("Ollama:BaseUrl is not configured.");

        builder.Services.AddHttpClient<IEmbeddingService, OllamaService>(client =>
        {
            client.BaseAddress = new Uri(uriString);
            client.Timeout = TimeSpan.FromSeconds(QuizConstants.OllamaEmbeddingTimeoutSeconds);
        });

        builder.Services.AddHttpClient("OllamaHealth", client =>
        {
            client.BaseAddress = new Uri(uriString);
            client.Timeout = TimeSpan.FromSeconds(QuizConstants.OllamaHealthTimeoutSeconds);
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.MapBlazorHub();
        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pdf",
            handler: (Guid id, QuizService qs, PrintService ps) => PrintAsync(
                id: id,
                quizService: qs,
                pdfExport: ps));
        app.MapFallbackToPage("/_Host");

        await SeedCategories(app);

        app.Run();
    }

    private static async Task<IResult> PrintAsync(Guid id, QuizService quizService, PrintService pdfExport)
    {
        var quiz = await quizService.GetDetailAsync(id);
        if (quiz == null) return Results.NotFound();

        var bytes = pdfExport.ExportQuiz(quiz);
        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}.pdf";
        return Results.File(bytes, "application/pdf", filename);
    }

    private static async Task SeedCategories(WebApplication app)
    {
        // Seed default categories if none exist

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (!db.Categories.Any())
        {
            var defaults = new[]
            {
                ("Biologie | Medizin",      "#c0392b"),
                ("Celebrity",               "#e91e63"),
                ("Essen | Gastronomie",     "#f39c12"),
                ("Film | Fernsehen",        "#e74c3c"),
                ("Fußball",                 "#1abc9c"),
                ("Games | Media",           "#3498db"),
                ("Geographie",              "#27ae60"),
                ("Geschichte | Religion",   "#d35400"),
                ("Kunst | Druck",           "#8e44ad"),
                ("Mode | Möbel",            "#e67e22"),
                ("Musik",                   "#9b59b6"),
                ("Politik | Wirtschaft",    "#2980b9"),
                ("Sonstiges",               "#95a5a6"),
                ("Sport",                   "#16a085"),
                ("Wissenschaft | Technik",  "#2c3e50"),
                ("Open Air",                "#f1c40f"),
            };

            foreach (var (name, color) in defaults)
                db.Categories.Add(new Category { Name = name, ColorHex = color });

            await db.SaveChangesAsync();
        }
    }

    #endregion Private Methods
}