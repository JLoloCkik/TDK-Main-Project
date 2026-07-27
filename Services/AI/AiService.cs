using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
/// Gemini API kapcsolatot, meglévő nézetek kontextus-elemzését és C# UI kódgenerálást végző szolgáltatás.
/// </summary>
public class AiService : IAiService
{
    private readonly HttpClient _httpClient;

    // Statikusan tároljuk a legutóbb működő modellt, hogy a következő kérésnél azonnal ezzel indítsunk
    private static string? _preferredVersion;
    private static string? _preferredModel;

    public AiService()
    {
        _httpClient = new HttpClient();

        try
        {
            DotNetEnv.Env.Load();
        }
        catch
        {
            // Csendben figyelmen kívül hagyjuk, ha nincs .env fájl
        }
    }

    private string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (string.IsNullOrEmpty(key))
        {
            try
            {
                key = DotNetEnv.Env.GetString("GEMINI_API_KEY")
                      ?? DotNetEnv.Env.GetString("GOOGLE_API_KEY");
            }
            catch
            {
                // DotNetEnv nincs konfigurálva
            }
        }

        return key ?? string.Empty;
    }

    public async Task<string> GenerateFeatureAsync(string prompt)
    {
        var response = await GenerateFeatureAsync(prompt, Role.Student, null);
        return response.SourceCode ?? string.Empty;
    }

    public async Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception(
                "Nem található Gemini API kulcs! Kérjük, futtassa az alkalmazást beállított környezeti változóval:\n" +
                "export GEMINI_API_KEY=\"a_te_kulcsod_itt\" && dotnet run");
        }

        var defaultMatrix = new[]
        {
            new { Version = "v1beta", Model = "gemini-2.5-pro" },
            new { Version = "v1beta", Model = "gemini-2.5-flash" },
            new { Version = "v1beta", Model = "gemini-3.5-flash" }
        };

        var fallbackMatrix = new List<dynamic>();

        if (!string.IsNullOrEmpty(_preferredVersion) && !string.IsNullOrEmpty(_preferredModel))
        {
            fallbackMatrix.Add(new { Version = _preferredVersion, Model = _preferredModel });
        }

        foreach (var item in defaultMatrix)
        {
            if (item.Version != _preferredVersion || item.Model != _preferredModel)
            {
                fallbackMatrix.Add(item);
            }
        }

        Exception? lastException = null;

        foreach (var attempt in fallbackMatrix)
        {
            int maxRetries = 3;
            int delayMs = 1000;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    if (retry > 0)
                    {
                        Console.WriteLine(
                            $"[AI Kapcsolat] Újrapróbálkozás ({retry}/{maxRetries - 1}) {attempt.Version} - {attempt.Model}...");
                    }
                    else
                    {
                        Console.WriteLine($"[AI Kapcsolat] Megkísérlés: {attempt.Version} - {attempt.Model}...");
                    }

                    var response = await CallGeminiApiInternalAsync(prompt, role, history, apiKey, (string)attempt.Version, (string)attempt.Model);
                    
                    _preferredVersion = attempt.Version;
                    _preferredModel = attempt.Model;

                    Console.WriteLine($"[AI Kapcsolat] SIKERES! Használt végpont: {attempt.Version}/{attempt.Model}");
                    return response;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine(
                        $"[AI Kapcsolat - Próbálkozás sikertelen ({attempt.Version}/{attempt.Model})]: {ex.Message}");

                    if (ex.Message.Contains("403") || ex.Message.Contains("API_KEY_INVALID"))
                    {
                        throw new Exception(
                            $"Érvénytelen Gemini API kulcs! Ellenőrizze a beállításokat. Részletek: {ex.Message}");
                    }

                    if (ex.Message.Contains("404") || ex.Message.Contains("400"))
                    {
                        break;
                    }

                    if (retry < maxRetries - 1 && (ex.Message.Contains("503") || ex.Message.Contains("429") ||
                                                   ex.Message.Contains("Hálózati hiba") || ex.Message.Contains("MAX_TOKENS")))
                    {
                        Console.WriteLine(
                            $"[AI Kapcsolat] Átmeneti hiba észlelve. Várakozás {delayMs} ms-ig az újrapróbálkozás előtt...");
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        throw new Exception(
            $"Nem sikerült elérni a Gemini API-t egyik konfigurációval sem. Legutolsó hiba:\n{lastException?.Message}");
    }

    private string GetExistingViewsContext()
    {
        try
        {
            var evolDir = PathHelper.GetEvolViewsDirectory();
            if (!Directory.Exists(evolDir)) return "Nincsenek meglévő AI nézetek.";

            var files = Directory.GetFiles(evolDir, "*.cs");
            if (files.Length == 0) return "Nincsenek meglévő AI nézetek.";

            var sb = new StringBuilder();
            sb.AppendLine("AZ ALÁBBI AI ÁLTAL LÉTREHOZOTT NÉZETEK LÉTEZNEK A RENDSZERBEN:");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var content = File.ReadAllText(file);
                sb.AppendLine($"- Fájl: {fileName}");
                if (content.Length > 800)
                {
                    sb.AppendLine($"  Kód részlet: {content.Substring(0, 800)}...\n");
                }
                else
                {
                    sb.AppendLine($"  Kód: {content}\n");
                }
            }
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<AiEvolveResponse> CallGeminiApiInternalAsync(string prompt, Role role, string? history,
        string apiKey, string apiVersion, string modelName)
    {
        var url =
            $"https://generativelanguage.googleapis.com/{apiVersion}/models/{modelName}:generateContent?key={apiKey}";

        var existingViewsContext = GetExistingViewsContext();

        var systemInstruction =
            $@"You are the automated C# and Avalonia UI compiler-agent for ""EvolKréta"", a self-evolving educational system.
Your objective is to generate, MODIFY, or DELETE safe, compile-safe, and strictly role-appropriate C# code for dynamic views.

ACTIVE USER ROLE: {role}

{existingViewsContext}

INSTRUCTIONS FOR MODIFYING OR DELETING EXISTING VIEWS:
- If the prompt asks to EDIT/MODIFY an existing view (e.g., ""Módosítsd a Sportnap nézetet""), output the UPDATED full C# source code keeping the SAME class name base and structure.
- If the prompt asks to DELETE/REMOVE an existing view (e.g., ""Töröld a Sportnap nézetet""), return JSON with:
  {{
    ""action"": ""DELETE"",
    ""viewName"": ""ClassNameToDelete""
  }}

=========================================
AVAILABLE CLASSES AND INTERFACES (CRITICAL FOR C# COMPILATION):
=========================================
- `Role` Enum: Student, Teacher, Director.
- `User` Class: int Id, string Name, Role Role, string ClassName. (WARNING: NO 'Email' property exists in User class!).
- `Grade` Class: int Id, int StudentId, int SubjectId, int Value, DateTime Date, User? Student, Subject? Subject. (WARNING: NO 'Weight' property exists in Grade class!).
- `Subject` Class: int Id, string Name.
- `Lesson` Class: int Id, string ClassName, string SubjectName, string Room, DateTime Date.
- `IStudentContext` Interface: List<Grade> GetMyGrades(); User? GetMyProfile(); List<Lesson> GetMyLessons();
- `ITeacherContext` Interface: List<User> GetMyClassStudents(); void AddGrade(int studentId, Grade grade); List<Subject> GetAllSubjects(); List<string> GetAllClasses(); List<Lesson> GetLessons(); void AddLesson(Lesson lesson);
- `IDirectorContext` Interface: List<User> GetAllUsers(); void CreateUser(User newUser); void DeleteUser(int userId); List<string> GetAllClasses(); void AssignClassToStudent(int studentId, string className);

=========================================
ROLE-BASED ACCESS CONTROL (RBAC) MATRIX:
=========================================
1. ROLE: Director (Igazgató) - Global access to school functions and administration.
2. ROLE: Teacher (Tanár) - Strictly restricted to class and subject teaching. CANNOT access IDirectorContext.
3. ROLE: Student (Diák) - Read-only personal student data. CANNOT access ITeacherContext or IDirectorContext.

=========================================
CRITICAL C# & AVALONIA COMPILATION RULES:
=========================================
1. NO TOP-LEVEL STATEMENTS: Everything inside class declarations.
2. NO 'Student' TYPE: Use 'User' where 'user.Role == Role.Student'.
3. NO 'Weight' PROPERTY on Grade.
4. NO DATAGRID or FuncDataTemplate. For ListBox/ComboBox, set `ItemsSource = myCollection.Select(u => $""{{u.Name}} ({{u.Role}})"").ToList()`.
5. AVALONIA CONTROL ITEMS PROPERTY IS READ-ONLY: Use `comboBox.ItemsSource = ...` or `listBox.ItemsSource = ...`.
6. TEXTBOX PLACEHOLDER: Use `TextBox.PlaceholderText` instead of `TextBox.Watermark`.
7. NO 'Panel.Child': `StackPanel` does NOT have `.Child`! Use `.Children.Add(...)`. Only `Border` has `.Child`.
8. GRID POSITIONING: Use static method `Grid.SetColumn(control, col)` and `Grid.SetRow(control, row)`.
9. PARAMETERLESS CONSTRUCTOR: Always provide public parameterless constructor `public MyView() {{ }}` alongside context-injecting constructor.
10. REQUIRED IMPORTS:
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using Avalonia;
   using Avalonia.Controls;
   using Avalonia.Controls.Primitives;
   using Avalonia.Layout;
   using Avalonia.Media;
   using Kreta.Core;
   using Kreta.Contexts;
11. UNIQUE CLASS NAME: Inherit `UserControl` and implement `IEvolView`.
12. IEvolView INTERFACE:
   - `public string Name => ""A funkció magyar neve"";`
   - `public string Description => ""Rövid magyar leírás"";`
   - `public Control CreateView()`
13. KEEP C# SHORT & SIMPLE (Under 100 lines) so it easily fits within response token limits!

=========================================
SECURITY GUARDRAILS:
=========================================
- If a user requests a feature that violates their role permissions, return JSON with:
  {{
    ""action"": ""REJECT"",
    ""viewName"": ""HibaView"",
    ""description"": ""Hozzáférés megtagadva""
  }}
{(string.IsNullOrWhiteSpace(history) ? "" : $@"

=========================================
SELF-HEALING: AZ ELŐZŐ PRÓBÁLKOZÁSOD HIBÁS VOLT!
=========================================
A fordítási hiba:
{history}

Ez alapján generálj EGY EGYSZERŰBB, RÖVIDEBB C# kódot, ami ezt kiküszöbli.")}
";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"Role: {role}. Prompt: {prompt}" } } }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                maxOutputTokens = 8192,
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        viewName = new { type = "STRING" },
                        description = new { type = "STRING" },
                        sourceCode = new { type = "STRING" },
                        testCode = new { type = "STRING" },
                        action = new { type = "STRING" },
                        handlerMethod = new { type = "STRING" }
                    },
                    required = new[] { "viewName", "description" }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            throw new Exception($"Hálózati hiba a Google API hívása közben: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"Szerver hiba! Státusz kód: {(int)response.StatusCode} ({response.ReasonPhrase}). Válasz: {errorBody}");
        }

        var responseString = await response.Content.ReadAsStringAsync();

        try
        {
            using var doc = JsonDocument.Parse(responseString);
            var candidate = doc.RootElement.GetProperty("candidates")[0];

            string? finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

            var partsElement = candidate.GetProperty("content").GetProperty("parts");
            var responseText = partsElement.GetArrayLength() > 0
                ? partsElement[0].GetProperty("text").GetString()
                : null;

            if (string.IsNullOrEmpty(responseText))
            {
                if (finishReason == "MAX_TOKENS")
                {
                    throw new Exception(
                        "A modell válasza megszakadt, mielőtt befejezte volna a kódot (token-limit). " +
                        "Kérj egy egyszerűbb / kisebb funkciót, vagy próbáld újra.");
                }

                throw new Exception($"Az AI üres választ adott vissza (finishReason: {finishReason ?? "ismeretlen"}).");
            }

            var cleanedJson = responseText.Trim();
            if (cleanedJson.StartsWith("```json"))
            {
                cleanedJson = cleanedJson.Substring(7);
                if (cleanedJson.EndsWith("```"))
                {
                    cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
                }
            }

            cleanedJson = cleanedJson.Trim();

            using var parsedResponse = JsonDocument.Parse(cleanedJson);
            var root = parsedResponse.RootElement;

            if (root.TryGetProperty("action", out var actionProp) && actionProp.GetString() == "REJECT")
            {
                Console.WriteLine(
                    "[RBAC Guardrail] Az AI elutasította a generálási kérést biztonsági szabályzat megsértése miatt.");

                return new AiEvolveResponse
                {
                    ViewName = "Hozzáférés Megtagadva",
                    Description = "Szerepkör-alapú elutasítás.",
                    Action = "REJECT",
                    SourceCode = string.Empty,
                    TestCode = string.Empty
                };
            }

            var result = JsonSerializer.Deserialize<AiEvolveResponse>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Nem sikerült deszerializálni a generált választ.");

            if (!string.IsNullOrEmpty(result.SourceCode))
            {
                result.SourceCode = result.SourceCode
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\\"", "\"");
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Hiba a válasz JSON feldolgozása közben: {ex.Message}. Nyers válasz: {responseString}");
        }
    }
}