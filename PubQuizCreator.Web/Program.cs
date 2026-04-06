using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Data;
using PubQuizCreator.Services;
using QuestPDF.Infrastructure;

internal class Program
{
    #region Private Methods

    private static void Main(string[] args)
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
            .AddScoped<DashboardService>();
        builder.Services
            .AddScoped<MediaService>();

        builder.Services
            .AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(
                connectionString: builder.Configuration.GetConnectionString("Default"),
                npgsqlOptionsAction: o => o.UseVector()));

        builder.Services
            .AddRazorPages();
        builder.Services
            .AddServerSideBlazor();

        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 20 * 1024 * 1024; // 20 MB
        });

        var uriString = builder.Configuration["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException("Ollama:BaseUrl is not configured.");

        builder.Services.AddHttpClient<IEmbeddingService, OllamaService>(client =>
        {
            client.BaseAddress = new Uri(uriString);
            client.Timeout = TimeSpan.FromSeconds(Constants.OllamaEmbeddingTimeoutSeconds);
        });

        builder.Services.AddHttpClient("OllamaHealth", client =>
        {
            client.BaseAddress = new Uri(uriString);
            client.Timeout = TimeSpan.FromSeconds(Constants.OllamaHealthTimeoutSeconds);
        });

        QuestPDF.Settings.License = LicenseType.Community;

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        var mediaPath = MediaService.GetStoragePath(builder.Configuration);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaPath),
            RequestPath = "/media"
        });

        app.MapBlazorHub();
        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pdf",
            handler: (Guid id, QuizService qs, IConfiguration cfg) => PrintAsync(id, qs, cfg));
        app.MapFallbackToPage("/_Host");

        app.Run();
    }

    private static async Task<IResult> PrintAsync(Guid id, QuizService quizService, IConfiguration cfg)
    {
        var quiz = await quizService.GetDetailAsync(id);
        if (quiz == null) return Results.NotFound();

        var fontSizeDefault = cfg.GetValue(
            key: "Print:FontSizeDefault",
            defaultValue: Constants.FontSizeDefault);
        var fontSizeHeader = cfg.GetValue(
            key: "Print:FontSizeHeader",
            defaultValue: Constants.FontSizeHeader);

        var bytes = PrintService.ExportQuiz(
            quiz: quiz,
            fontSizeDefault: fontSizeDefault,
            fontSizeHeader: fontSizeHeader);

        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}.pdf";
        return Results.File(bytes, "application/pdf", filename);
    }

    #endregion Private Methods
}