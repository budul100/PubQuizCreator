using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Data;
using PubQuizCreator.Services;
using QuestPDF.Infrastructure;

internal class Program
{
    #region Private Methods

    private static async Task<IResult> CreateExportAsync(Guid id, QuizService quizService,
        ExportService exportService, CancellationToken cancellationToken)
    {
        var quiz = await quizService.GetDetailAsync(
            id: id,
            ct: cancellationToken);

        if (quiz == default) return Results.NotFound();

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
                var questions = await exportService.ExportAsync(
                    round: round,
                    isAnswers: false,
                    ct: cancellationToken);

                using (var questionStream = zip.CreateEntry(
                    entryName: $"quiz_{quiz.Date:yyyy-MM-dd}_r{round.Position:D1}a_questions.pptx",
                    compressionLevel: CompressionLevel.Fastest).Open())
                {
                    questionStream.Write(questions);
                }

                var answers = await exportService.ExportAsync(
                    round: round,
                    isAnswers: true,
                    ct: cancellationToken);

                using (var answerStream = zip.CreateEntry(
                    entryName: $"quiz_{quiz.Date:yyyy-MM-dd}_r{round.Position:D1}b_answers.pptx",
                    compressionLevel: CompressionLevel.Fastest).Open())
                {
                    answerStream.Write(answers);
                }
            }
        }

        zipStream.Position = 0;

        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}_slides.zip".Replace(" ", "_");

        var result = Results.File(
            fileStream: zipStream,
            contentType: "application/zip",
            fileDownloadName: filename);

        return result;
    }

    private static async Task<IResult> CreatePrintAsync(Guid id, QuizService quizService,
        PrintService printService, CancellationToken cancellationToken)
    {
        var quiz = await quizService.GetDetailAsync(
            id: id,
            ct: cancellationToken);

        if (quiz == default) return Results.NotFound();

        var contents = printService.Print(quiz);

        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}_questions.pdf";

        var result = Results.File(
            fileContents: contents,
            contentType: "application/pdf",
            fileDownloadName: filename);

        return result;
    }

    private static async Task<IResult> LogOutAsync(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var overridePath = SettingsService.GetOverridePath(builder.Configuration);

        builder.Configuration.AddJsonFile(
            path: overridePath,
            optional: true,
            reloadOnChange: true);

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
        builder.Services
            .AddScoped<ExportService>();

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

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
            });

        builder.Services.AddAuthorization();

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

        app.UseAuthentication();
        app.UseAuthorization();

        var settingsService = app.Services.GetRequiredService<SettingsService>();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(settingsService.MediaPath),
            RequestPath = "/media"
        });

        app.MapBlazorHub();

        app.MapGet(
            pattern: "/logout",
            handler: LogOutAsync).AllowAnonymous();

        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pdf",
            handler: (Guid id, QuizService qs, PrintService ps, CancellationToken ct) => CreatePrintAsync(id, qs, ps, ct));
        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pptx",
            handler: (Guid id, QuizService qs, ExportService es, CancellationToken ct) => CreateExportAsync(id, qs, es, ct));

        app.MapFallbackToPage("/_Host");

        app.Run();
    }

    #endregion Private Methods
}