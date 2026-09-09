using System.IO.Compression;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;
using PubQuizCreator.Services.App;
using PubQuizCreator.Services.Content;
using PubQuizCreator.Services.Data;
using PubQuizCreator.Services.Export;
using PubQuizCreator.Web.Helpers;
using QuestPDF.Infrastructure;

internal class Program
{
    #region Private Methods

    private static async Task<IResult> CreateJsonAsync(Guid quizId, string? query, QuizService quizService,
        CancellationToken ct)
    {
        var quiz = await quizService.GetDetailAsync(
            quizId: quizId,
            ct: ct);
        if (quiz == default) return Results.NotFound();

        var rounds = GetRounds(
            rounds: quiz.Rounds,
            query: query);

        var jsonBytes = quiz.CreateJson(rounds);
        var filename = $"quiz_{quiz.Date:yyyy-MM-dd}_data.json";

        return Results.File(
            fileContents: jsonBytes,
            contentType: "application/json",
            fileDownloadName: filename);
    }

    private static async Task<IResult> CreatePptxAsync(Guid quizId, string? query, string? template,
        QuizService quizService, FileService exportService, SettingsService settingsService,
        CancellationToken ct)
    {
        var quiz = await quizService.GetDetailAsync(quizId: quizId, ct: ct);
        if (quiz == default)
            return Results.NotFound();

        var rounds = GetRounds(rounds: quiz.Rounds, query: query).ToList();
        if (rounds.Count == 0)
            return Results.BadRequest("No rounds with slots found for the given selection.");

        if (string.IsNullOrWhiteSpace(template))
            return Results.BadRequest("Query parameter 'template' is required.");

        var templatePath = settingsService.GetPptxTemplatePath(template);
        if (templatePath == null)
            return Results.NotFound($"Template '{template}' not found.");

        var date = quiz.Date;
        var templateName = Path.GetFileNameWithoutExtension(templatePath).ToLower();

        if (rounds.Count == 1)
        {
            var round = rounds[0];
            var pptx = await exportService.ExportAsync(
                round: round,
                templatePath: templatePath,
                ct: ct);

            var filename = $"{date:yyyy-MM-dd}_r{round.Position:D1}_{templateName}.pptx";

            return Results.File(
                fileContents: pptx,
                contentType: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                fileDownloadName: filename);
        }
        else
        {
            byte[] zipBytes;

            using (var zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    foreach (var round in rounds)
                    {
                        var pptx = await exportService.ExportAsync(
                            round: round,
                            templatePath: templatePath,
                            ct: ct);

                        var entryName = $"{date:yyyy-MM-dd}_r{round.Position:D1}_{templateName}.pptx";

                        using var entryStream = zip.CreateEntry(
                            entryName: entryName,
                            compressionLevel: CompressionLevel.Fastest).Open();

                        entryStream.Write(pptx);
                    }
                }

                zipBytes = zipStream.ToArray();
            }

            var zipFilename = $"{date:yyyy-MM-dd}_{templateName}.zip";

            return Results.File(
                fileContents: zipBytes,
                contentType: "application/zip",
                fileDownloadName: zipFilename);
        }
    }

    private static async Task<IResult> CreatePrintAsync(Guid quizId, string? query, QuizService quizService,
        PrintService printService, CancellationToken ct)
    {
        var quiz = await quizService.GetDetailAsync(
            quizId: quizId,
            ct: ct);
        if (quiz == default) return Results.NotFound();

        var roundIds = GetGuids(query);

        if (roundIds.Count > 0)
        {
            quiz.Rounds = quiz.Rounds
                .Where(r => roundIds.Contains(r.Id)).ToList();
        }

        var contents = printService.Print(quiz);
        var filename = $"{quiz.Date:yyyy-MM-dd}_quiz.pdf";

        return Results.File(
            fileContents: contents,
            contentType: "application/pdf",
            fileDownloadName: filename);
    }

    private static IResult DownloadMedia(string fileName, SettingsService settingsService)
    {
        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(settingsService.GetPathMedia(), safeFileName);

        if (!File.Exists(filePath))
        {
            return Results.NotFound();
        }

        return Results.File(
            path: filePath,
            fileDownloadName: safeFileName);
    }

    private static HashSet<Guid> GetGuids(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        return query
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToHashSet();
    }

    private static IEnumerable<Round> GetRounds(IEnumerable<Round> rounds, string? query)
    {
        var roundIds = GetGuids(query);

        return rounds
            .Where(r => r.Slots.Count > 0
                && (roundIds.Count == 0 || roundIds.Contains(r.Id)))
            .OrderBy(r => r.Position).sToArray();
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
            .AddScoped<PrintService>();
        builder.Services
            .AddScoped<FileService>();

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
            pattern: "/export/quiz/{quizId:guid}/pdf",
            handler: CreatePrintAsync);

        app.MapGet(
            pattern: "/export/quiz/{quizId:guid}/json",
            handler: CreateJsonAsync);

        app.MapGet(
            pattern: "/export/quiz/{quizId:guid}/pptx",
            handler: CreatePptxAsync);

        app.MapGet(
            pattern: "/media/download/{fileName}",
            handler: DownloadMedia).RequireAuthorization();

        app.MapFallbackToPage("/_Host");

        app.Run();
    }

    #endregion Private Methods
}