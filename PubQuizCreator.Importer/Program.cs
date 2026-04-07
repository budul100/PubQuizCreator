using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Data;
using PubQuizCreator.Services;

internal partial class Program
{
    #region Private Fields

    private const int SkipRows = 3;
    private const string SkipSheet = "Übersicht";

    #endregion Private Fields

    #region Private Methods

    [GeneratedRegex(@" {2,}")]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketRegex();

    private static int Error(string msg)
    {
        Console.Error.WriteLine(msg);
        return 1;
    }

    private static async Task<int> ImportExcelAsync(string path, IServiceScope scope)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        var questionService = scope.ServiceProvider.GetRequiredService<QuestionService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();

        var allCategories = await categoryService.GetAllAsync();

        using var workbook = new XLWorkbook(path);

        var totalRows = workbook.Worksheets
            .Where(s => !s.Name.Equals(SkipSheet, StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.RowsUsed().Skip(SkipRows).Count());

        Console.WriteLine($"Total rows to process: {totalRows}");
        var processed = 0;

        var totalImported = 0;
        var totalSkipped = 0;

        foreach (var sheet in workbook.Worksheets)
        {
            if (sheet.Name.Equals(SkipSheet, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  Skipping sheet: {sheet.Name}");
                continue;
            }

            var categoryName = sheet.Name.Trim();
            Console.WriteLine($"\nSheet: {categoryName}");

            var category = allCategories
                .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.CurrentCultureIgnoreCase));

            if (category == null)
            {
                category = await categoryService.CreateAsync(categoryName, "#95a5a6");
                Console.WriteLine($"  Created category: {categoryName}");
            }

            var rows = sheet.RowsUsed().Skip(SkipRows); // skip header row

            foreach (var row in rows)
            {
                var textLong = row.Cell("B").GetString().Trim();
                var textShort = row.Cell("C").GetString().Trim();
                var answer = row.Cell("D").GetString().Trim();
                var mediaCode = row.Cell("E").GetString().Trim().ToUpperInvariant();

                // Skip empty rows
                if (string.IsNullOrEmpty(textLong) && string.IsNullOrEmpty(answer))
                {
                    totalSkipped++;
                    continue;
                }

                // Fallback: use long text as short if short is empty
                if (string.IsNullOrEmpty(textShort))
                    textShort = textLong;

                // Parse media type
                var mediaType = mediaCode switch
                {
                    "B" => MediaType.Image,
                    "M" => MediaType.Audio,
                    "V" => MediaType.Video,
                    _ => MediaType.None
                };

                // Extract filename from [brackets] in long text
                string? mediaFile = null;
                var bracketMatch = BracketRegex().Match(textLong);
                if (bracketMatch.Success)
                    mediaFile = bracketMatch.Groups[1].Value.Trim();

                var question = new Question
                {
                    TextShort = textShort,
                    TextLong = textLong,
                    Answer = answer,
                    CategoryId = category.Id,
                    MediaType = mediaType,
                    MediaFile = mediaFile,
                    WasUsed = true,
                };

                try
                {
                    await questionService.CreateAsync(question);
                    Console.WriteLine($"  + [{mediaType,-5}] {textShort[..Math.Min(60, textShort.Length)]}");
                    totalImported++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ERROR on row {row.RowNumber()}: {ex.Message}");
                    totalSkipped++;
                }

                processed++;
                Console.Write($"\r  Progress: {processed}/{totalRows} ({100 * processed / totalRows}%)   ");
            }
        }

        Console.WriteLine($"\nDone. Imported: {totalImported}, Skipped/Errors: {totalSkipped}");
        return 0;
    }

    private static async Task<int> ImportIdeasAsync(string path, string? categoryName, IServiceScope scope)
    {
        var ideaService = scope.ServiceProvider.GetRequiredService<IdeaService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();

        Category? category = null;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var allCategories = await categoryService.GetAllAsync();
            category = allCategories
                .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.CurrentCultureIgnoreCase));

            if (category == null)
            {
                category = await categoryService.CreateAsync(categoryName.Trim(), "#95a5a6");
                Console.WriteLine($"Created category: {categoryName}");
            }
        }

        // Parse blocks separated by blank lines
        var raw = await File.ReadAllTextAsync(path);
        var blocks = raw
            .Replace("\r\n", "\n")   // <-- normalize line endings first
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => BlockRegex().Replace(b.Replace("\n", " "), " ").Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        Console.WriteLine($"Found {blocks.Count} ideas in file.");

        var imported = 0;
        var skipped = 0;

        foreach (var text in blocks)
        {
            try
            {
                await ideaService.CreateAsync(text, category?.Id);
                Console.WriteLine($"  + {text[..Math.Min(80, text.Length)]}");
                imported++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR: {ex.Message}");
                skipped++;
            }
        }

        Console.WriteLine($"\nDone. Imported: {imported}, Skipped/Errors: {skipped}");
        return 0;
    }

    private static async Task<int> ImportUnusableAsync(string path, IServiceScope scope)
    {
        var questionService = scope.ServiceProvider.GetRequiredService<QuestionService>();

        var raw = await File.ReadAllTextAsync(path);
        var blocks = raw
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => BlockRegex().Replace(b.Replace("\n", " "), " ").Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        Console.WriteLine($"Found {blocks.Count} unusable questions in file.");

        var imported = 0;
        var skipped = 0;

        foreach (var block in blocks)
        {
            var (textShort, answer) = SplitQuestionAnswer(block);

            var question = new Question
            {
                TextShort = textShort,
                TextLong = "",
                Answer = answer,
                IsUnusable = true,
                WasUsed = true,
                CategoryId = null,
            };

            try
            {
                await questionService.CreateAsync(question);
                Console.WriteLine($"  + {textShort[..Math.Min(80, textShort.Length)]}");
                imported++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR: {ex.Message}");
                skipped++;
            }
        }

        Console.WriteLine($"\nDone. Imported: {imported}, Skipped/Errors: {skipped}");
        return 0;
    }

    private static async Task<int> Main(string[] args)
    {
        // --- Setup (shared) ---
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddScoped<QuestionService>();
        services.AddScoped<IdeaService>();
        services.AddScoped<CategoryService>();

        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Default"), n => n.UseVector()));

        var ollamaUrl = config["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException("Ollama:BaseUrl missing.");

        services.AddHttpClient<IEmbeddingService, OllamaService>(
            client => client.BaseAddress = new Uri(ollamaUrl));

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        // --reembed needs no file path
        if (args.Contains("--reembed"))
            return await ReEmbedAsync(scope);

        // All other modes require a file path
        var filePath = args.ElementAtOrDefault(0);
        if (filePath == null || !File.Exists(filePath))
            return Error($"File not found: {filePath ?? "(none)"}");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var isUnusable = args.Contains("--unusable");

        return ext switch
        {
            ".xlsx" => await ImportExcelAsync(filePath, scope),
            ".txt" when isUnusable => await ImportUnusableAsync(filePath, scope),
            ".txt" => await ImportIdeasAsync(filePath, args.ElementAtOrDefault(1), scope),
            _ => Error($"Unsupported file type: {ext}")
        };
    }

    private static async Task<int> ReEmbedAsync(IServiceScope scope)
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        await using var db = await dbFactory.CreateDbContextAsync();

        var questions = await db.Questions
            .Where(q => q.Embedding == null)
            .ToListAsync();

        Console.WriteLine($"Found {questions.Count} questions without embedding.");

        var ok = 0;
        var failed = 0;
        var pendingSave = 0;

        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            var text = QuestionService.BuildEmbeddingInput(question);

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"  SKIP (no text): {question.Id}");
                failed++;
                continue;
            }

            try
            {
                var vector = await embeddingService.GetEmbeddingAsync(text);
                question.Embedding = new Pgvector.Vector(vector);
                ok++;
                pendingSave++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR (embedding) [{question.Id}]: {ex.Message}");
                failed++;
            }

            // Save batch
            if (pendingSave >= Constants.EmbeddingReEmbedSize)
            {
                try
                {
                    await db.SaveChangesAsync();
                    pendingSave = 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ERROR (save batch): {ex.Message}");
                }
            }

            Console.Write($"\r  Progress: {i + 1}/{questions.Count} — OK: {ok}, Failed: {failed}   ");
        }

        // Save remaining
        if (pendingSave > 0)
        {
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR (save final batch): {ex.Message}");
            }
        }

        Console.WriteLine($"\nDone. OK: {ok}, Failed: {failed}");
        return failed > 0 ? 1 : 0;
    }

    private static (string textShort, string answer) SplitQuestionAnswer(string block)
    {
        // "Question = Answer"
        var eqIdx = block.IndexOf(" = ", StringComparison.Ordinal);
        if (eqIdx > 0)
            return (block[..eqIdx].Trim(), block[(eqIdx + 3)..].Trim());

        // "Question? Answer"
        var qIdx = block.LastIndexOf('?');
        if (qIdx >= 0 && qIdx < block.Length - 2)
        {
            var after = block[(qIdx + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(after))
                return (block[..(qIdx + 1)].Trim(), after);
        }

        // "Question. Answer" — only if sentence before dot is long enough
        var dotIdx = block.IndexOf(". ", StringComparison.Ordinal);
        if (dotIdx > 15)
            return (block[..(dotIdx + 1)].Trim(), block[(dotIdx + 2)..].Trim());

        // No split found — whole block is the question, answer empty
        return (block, "");
    }

    #endregion Private Methods
}