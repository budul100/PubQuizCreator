using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Data;
using PubQuizCreator.Services;
using QuestPDF.Infrastructure;
using System.IO.Compression;

internal class Program
{
    #region Private Methods

    private static async Task<IResult> CreateExportAsync(Guid id, QuizService quizService,
        SettingsService settingsService)
    {
        var quiz = await quizService.GetDetailAsync(id);
        if (quiz == null) return Results.NotFound();

        var questionsPath = settingsService.GetTemplatePath("Questions");
        var answersPath = settingsService.GetTemplatePath("Answers");

        var zipStream = new MemoryStream();

        using (var zip = new ZipArchive(
            stream: zipStream,
            mode: ZipArchiveMode.Create,
            leaveOpen: true))
        {
            var rounds = quiz.Rounds
                .Where(r => r.Slots.Count > 0)
                .OrderBy(r => r.Position).ToArray();

            foreach (var round in rounds)
            {
                var slots = round.Slots
                    .Where(s => s.Question != null)
                    .OrderBy(s => s.Position).ToList();

                if (slots.Count == 0) continue;

                var questions = slots.Select((s, i) => new Dictionary<string, string>
                {
                    ["Number"] = $"Frage {i + 1}",
                    ["Question"] = s.Question!.TextShort
                });

                var questionSlides = ExportService.Export(questions, questionsPath, 1);

                using (var questionStream = zip.CreateEntry(
                    $"Round{round.Position:D2}_Questions.pptx",
                    CompressionLevel.Fastest).Open())
                {
                    questionStream.Write(questionSlides);
                }

                var answers = slots.Select((s, i) => new Dictionary<string, string>
                {
                    ["Number"] = $"Frage {i + 1}",
                    ["Question"] = s.Question!.TextShort,
                    ["Answer"] = s.Question!.Answer
                });

                var answerSlides = ExportService.Export(answers, answersPath, 1);

                using (var answerStream = zip.CreateEntry(
                    $"Round{round.Position:D2}_Answers.pptx",
                    CompressionLevel.Fastest).Open())
                {
                    answerStream.Write(answerSlides);
                }
            }
        }

        zipStream.Position = 0;

        var filename = $"{quiz.Title}_{quiz.Date:yyyy-MM-dd}.zip".Replace(" ", "_");

        var result = Results.File(
            fileStream: zipStream,
            contentType: "application/zip",
            fileDownloadName: filename);

        return result;
    }

    private static async Task<IResult> CreatePrintAsync(Guid id, QuizService quizService,
        PrintService printService)
    {
        var quiz = await quizService.GetDetailAsync(id);
        if (quiz == null) return Results.NotFound();

        var bytes = printService.Print(quiz);

        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}.pdf";

        var result = Results.File(
            fileContents: bytes,
            contentType: "application/pdf",
            fileDownloadName: filename);

        return result;
    }

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddSingleton<SettingsService>();
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
            .AddScoped<MediaService>();
        builder.Services
            .AddScoped<StateService>();
        builder.Services
            .AddScoped<DashboardService>();
        builder.Services
            .AddScoped<PrintService>();

        builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(
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

        var settingsService = app.Services.GetRequiredService<SettingsService>();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(settingsService.MediaPath),
            RequestPath = "/media"
        });

        app.MapBlazorHub();

        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pdf",
            handler: (Guid id, QuizService qs, PrintService ps) => CreatePrintAsync(id, qs, ps));
        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pptx",
            handler: (Guid id, QuizService qs, SettingsService ss) => CreateExportAsync(id, qs, ss));

        app.MapFallbackToPage("/_Host");

        app.Run();
    }

    #endregion Private Methods
}