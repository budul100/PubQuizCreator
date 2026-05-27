using System.IO.Compression;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Data;
using PubQuizCreator.Services;
using PubQuizCreator.Services.StateService;
using PubQuizCreator.Web.Helpers;
using QuestPDF.Infrastructure;

internal class Program
{
    #region Private Methods

    private static async Task<IResult> CreateJsonAsync(Guid id, string? rounds, QuizService quizService,
        CancellationToken cancellationToken)
    {
        var quiz = await quizService.GetDetailAsync(quizId: id, ct: cancellationToken);
        if (quiz == default) return Results.NotFound();

        var roundIds = ParseGuids(rounds);

        var selectedRounds = quiz.Rounds
            .Where(r => r.Slots.Count > 0
                && (roundIds.Count == 0 || roundIds.Contains(r.Id)))
            .OrderBy(r => r.Position)
            .ToArray();

        var jsonBytes = quiz.CreateJson(selectedRounds);
        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}_data.json";

        return Results.File(
            fileContents: jsonBytes,
            contentType: "application/json",
            fileDownloadName: filename);
    }

    private static async Task<IResult> CreatePptxAsync(Guid id, string? rounds, string? template,
        QuizService quizService, ExportService exportService, SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var quiz = await quizService.GetDetailAsync(quizId: id, ct: cancellationToken);
        if (quiz == default) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(template))
            return Results.BadRequest("Query parameter 'template' is required.");

        var templatePath = settingsService.GetPptxTemplatePath(template);
        if (templatePath == null)
            return Results.NotFound($"Template '{template}' not found.");

        var roundIds = ParseGuids(rounds);

        var selectedRounds = quiz.Rounds
            .Where(r => r.Slots.Count > 0
                && (roundIds.Count == 0 || roundIds.Contains(r.Id)))
            .OrderBy(r => r.Position)
            .ToArray();

        if (selectedRounds.Length == 0)
            return Results.BadRequest("No rounds with slots found for the given selection.");

        var date = quiz.Date;

        // Single round → direct .pptx file
        if (selectedRounds.Length == 1)
        {
            var round = selectedRounds[0];
            var pptx = await exportService.ExportAsync(round, templatePath, cancellationToken);
            var filename = $"quiz_{date:yyyy-MM-dd}_r{round.Position:D1}.pptx";

            return Results.File(
                fileContents: pptx,
                contentType: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                fileDownloadName: filename);
        }

        // Multiple rounds → ZIP
        var zipStream = new MemoryStream();

        using (var zip = new ZipArchive(
            stream: zipStream,
            mode: ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach (var round in selectedRounds)
            {
                var pptx = await exportService.ExportAsync(round, templatePath, cancellationToken);

                using var entry = zip.CreateEntry(
                    entryName: $"quiz_{date:yyyy-MM-dd}_r{round.Position:D1}.pptx",
                    compressionLevel: CompressionLevel.Fastest).Open();

                entry.Write(pptx);
            }
        }

        zipStream.Position = 0;

        var zipFilename = $"quiz_{date:yyyy-MM-dd}_slides.zip";

        return Results.File(
            fileStream: zipStream,
            contentType: "application/zip",
            fileDownloadName: zipFilename);
    }

    private static async Task<IResult> CreatePrintAsync(Guid id, string? rounds, QuizService quizService,
        PrintService printService, CancellationToken cancellationToken)
    {
        var quiz = await quizService.GetDetailAsync(quizId: id, ct: cancellationToken);
        if (quiz == default) return Results.NotFound();

        var roundIds = ParseGuids(rounds);

        // Filter rounds if selection provided; keep original order
        if (roundIds.Count > 0)
        {
            quiz.Rounds = quiz.Rounds
                .Where(r => roundIds.Contains(r.Id)).ToList();
        }

        var contents = printService.Print(quiz);
        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}_questions.pdf";

        return Results.File(
            fileContents: contents,
            contentType: "application/pdf",
            fileDownloadName: filename);
    }

    private static async Task<IResult> LogOutAsync(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var overridePath = SettingsService.GetPathOverride(builder.Configuration);

        builder.Configuration.AddJsonFile(
            path: overridePath,
            optional: true,
            reloadOnChange: true);

        builder.Services
            .AddSingleton<SettingsService>();
        builder.Services
            .AddSingleton<ToastService>();

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

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        var dataSource = dataSourceBuilder.Build();

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseNpgsql(dataSource, o => o.UseVector()));

        builder.Services
            .AddRazorPages();

        builder.Services.AddServerSideBlazor(options =>
        {
            options.DetailedErrors = builder.Environment.IsDevelopment();
            options.DisconnectedCircuitMaxRetained = 100;
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
            options.MaxBufferedUnacknowledgedRenderBatches = 10;
        });

        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = Constants.MaxUploadSizeBytes;
            options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
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

        if (!builder.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        if (!builder.Environment.IsDevelopment()
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT")))
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        var settingsService = app.Services.GetRequiredService<SettingsService>();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(settingsService.GetPathMedia()),
            RequestPath = "/media"
        });

        app.MapBlazorHub();

        app.MapGet(
            pattern: "/logout",
            handler: LogOutAsync).AllowAnonymous();

        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pdf",
            handler: (Guid id, string? rounds, QuizService qs, PrintService ps, CancellationToken ct)
                => CreatePrintAsync(id, rounds, qs, ps, ct));

        app.MapGet(
            pattern: "/export/quiz/{id:guid}/json",
            handler: (Guid id, string? rounds, QuizService qs, CancellationToken ct)
                => CreateJsonAsync(id, rounds, qs, ct));

        app.MapGet(
            pattern: "/export/quiz/{id:guid}/pptx",
            handler: (Guid id, string? rounds, string? template, QuizService qs, ExportService es, SettingsService sc, CancellationToken ct)
                => CreatePptxAsync(id, rounds, template, qs, es, sc, ct));

        app.MapFallbackToPage("/_Host");

        app.Run();
    }

    private static HashSet<Guid> ParseGuids(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToHashSet();
    }

    #endregion Private Methods
}