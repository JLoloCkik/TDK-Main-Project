using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetEnv;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
///     Service performing Gemini API connection, Formal Prompt Refiner pre-filtering, Prompt-to-Code Conformance
///     verification,
///     context analysis of existing views, and C# UI code generation.
/// </summary>
public class AiService : IAiService
{
    private static string? _preferredVersion;
    private static string? _preferredModel;
    private readonly IPromptConformanceVerifier _conformanceVerifier;
    private readonly HttpClient _httpClient;
    private readonly IPromptRefinerService _promptRefiner;

    public AiService()
    {
        _httpClient = new HttpClient();
        _promptRefiner = new PromptRefinerService(_httpClient);
        _conformanceVerifier = new PromptConformanceVerifier();

        try
        {
            Env.Load();
        }
        catch
        {
            // Silently ignore if no .env file
        }
    }

    public async Task<string> GenerateFeatureAsync(string prompt)
    {
        var response = await GenerateFeatureAsync(prompt, Role.Student);
        return response.SourceCode ?? string.Empty;
    }

    public async Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null,
        string? targetViewFilePath = null)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception(
                "Nem található Gemini API kulcs! Kérjük, futtassa az alkalmazást beállított környezeti változóval:\n" +
                "export GEMINI_API_KEY=\"a_te_kulcsod_itt\" && dotnet run");

        // STEP 1: Formal Prompt Refiner preprocessing
        var formalSpec = await _promptRefiner.RefinePromptAsync(prompt, role, apiKey);

        // STEP 2: Check for early RBAC rejection
        if (formalSpec.IsRoleViolating)
        {
            Console.WriteLine($"[RBAC Guardrail] Elutasítva: {formalSpec.ViolationReason}");
            return new AiEvolveResponse
            {
                ViewName = "Hozzáférés Megtagadva",
                Description = formalSpec.ViolationReason,
                Action = "REJECT",
                SourceCode = string.Empty,
                TestCode = string.Empty
            };
        }

        var defaultMatrix = new[]
        {
            new { Version = "v1beta", Model = "gemini-2.5-pro" },
            new { Version = "v1beta", Model = "gemini-2.5-flash" },
            new { Version = "v1beta", Model = "gemini-2.0-flash" }
        };

        var fallbackMatrix = new List<dynamic>();

        if (!string.IsNullOrEmpty(_preferredVersion) && !string.IsNullOrEmpty(_preferredModel))
            fallbackMatrix.Add(new { Version = _preferredVersion, Model = _preferredModel });

        foreach (var item in defaultMatrix)
            if (item.Version != _preferredVersion || item.Model != _preferredModel)
                fallbackMatrix.Add(item);

        Exception? lastException = null;

        foreach (var attempt in fallbackMatrix)
        {
            var maxRetries = 3;
            var delayMs = 1000;

            for (var retry = 0; retry < maxRetries; retry++)
                try
                {
                    if (retry > 0)
                        Console.WriteLine(
                            $"[AI Kapcsolat] Újrapróbálkozás ({retry}/{maxRetries - 1}) {attempt.Version} - {attempt.Model}...");
                    else
                        Console.WriteLine($"[AI Kapcsolat] Megkísérlés: {attempt.Version} - {attempt.Model}...");

                    var response = await CallGeminiApiInternalAsync(prompt, role, formalSpec, history, apiKey,
                        (string)attempt.Version, (string)attempt.Model, targetViewFilePath);

                    _preferredVersion = attempt.Version;
                    _preferredModel = attempt.Model;

                    Console.WriteLine($"[AI Kapcsolat] SIKERES! Használt végpont: {attempt.Version}/{attempt.Model}");

                    // STEP 3: Prompt-to-Code Conformance Verification (AST-based)
                    if (!string.IsNullOrEmpty(response.SourceCode) && response.Action != "DELETE" &&
                        response.Action != "REJECT")
                        _conformanceVerifier.VerifyConformance(response.SourceCode, formalSpec);

                    return response;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine(
                        $"[AI Kapcsolat - Próbálkozás sikertelen ({attempt.Version}/{attempt.Model})]: {ex.Message}");

                    if (ex.Message.Contains("403") || ex.Message.Contains("API_KEY_INVALID"))
                        throw new Exception(
                            $"Érvénytelen Gemini API kulcs! Ellenőrizze a beállításokat. Részletek: {ex.Message}");

                    if (ex.Message.Contains("404") || ex.Message.Contains("400")) break;

                    if (retry < maxRetries - 1 && (ex.Message.Contains("503") || ex.Message.Contains("429") ||
                                                   ex.Message.Contains("Hálózati hiba") ||
                                                   ex.Message.Contains("MAX_TOKENS")))
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

        throw new Exception(
            $"Nem sikerült elérni a Gemini API-t egyik konfigurációval sem. Legutolsó hiba:\n{lastException?.Message}");
    }

    private string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (string.IsNullOrEmpty(key))
            try
            {
                key = Env.GetString("GEMINI_API_KEY")
                      ?? Env.GetString("GOOGLE_API_KEY");
            }
            catch
            {
                // DotNetEnv not configured
            }

        return key ?? string.Empty;
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
                    sb.AppendLine($"  Kód részlet: {content.Substring(0, 800)}...\n");
                else
                    sb.AppendLine($"  Kód: {content}\n");
            }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     If the user SELECTED an already existing, previously generated view on the UI (e.g. clicked
    ///     on "Grade Calculator" in the sidebar), and then asks for a modification, this method is called
    ///     to inject its ENTIRE (untruncated) source code into the system prompt, with a clear MODIFY
    ///     instruction. This replaces the previous, unreliable behavior where the model had to GUESS
    ///     (based on name, from free text) which of the truncated code views listed by
    ///     GetExistingViewsContext() the user wanted to modify.
    /// </summary>
    private string GetTargetViewContext(string? targetViewFilePath)
    {
        if (string.IsNullOrWhiteSpace(targetViewFilePath) || !File.Exists(targetViewFilePath)) return string.Empty;

        try
        {
            var fileName = Path.GetFileName(targetViewFilePath);
            var className = Path.GetFileNameWithoutExtension(targetViewFilePath);
            var fullSource = File.ReadAllText(targetViewFilePath);

            return $@"
=========================================
A FELHASZNÁLÓ ÁLTAL JELENLEG KIJELÖLT / MEGNYITOTT NÉZET — EZT MÓDOSÍTSD KÖZVETLENÜL, NE TALÁLJ KI ÚJAT!
=========================================
A felhasználó ÉPPEN EZT a nézetet nyitotta meg/jelölte ki az oldalsávban ({fileName}), amikor elküldte a kérést.
Ha a kérés ennek a nézetnek a javítására, módosítására, bővítésére vagy hibaelhárítására vonatkozik
(pl. ""javítsd ki, mert nem működik"", ""adj hozzá egy törlés gombot"", ""ezt nem tudom kimenteni""), akkor:
1. NE hozz létre egy új, más nevű osztályt - MINDIG KÖZVETLENÜL EZT MÓDOSÍTSD.
2. Az `action` mező LEGYEN ""MODIFY"", a `viewName` mező PONTOSAN ez legyen: ""{className}"".
3. A `sourceCode` mezőben a TELJES, JAVÍTOTT forráskódot add vissza - az alábbi TELJES (nem csonkolt),
   jelenleg futó forráskódból indulj ki, és CSAK azt változtasd meg, amit a kérés ténylegesen igényel.
   Ne írd át feleslegesen a jól működő részeket, és ne veszítsd el a meglévő funkciókat.

--- TELJES JELENLEGI FORRÁSKÓD ({fileName}) ---
{fullSource}
--- FORRÁSKÓD VÉGE ---
";
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<AiEvolveResponse> CallGeminiApiInternalAsync(string prompt, Role role,
        FormalPromptSpecification spec, string? history, string apiKey, string apiVersion, string modelName,
        string? targetViewFilePath = null)
    {
        var url =
            $"https://generativelanguage.googleapis.com/{apiVersion}/models/{modelName}:generateContent?key={apiKey}";

        var existingViewsContext = GetExistingViewsContext();
        var targetViewContext = GetTargetViewContext(targetViewFilePath);

        var systemInstruction =
            $@"You are the automated C# and Avalonia UI compiler-agent for ""EvolKréta"", a self-evolving educational system.
Your objective is to generate, MODIFY, or DELETE safe, compile-safe, and strictly role-appropriate C# code for dynamic views.

ACTIVE USER ROLE: {role}
{targetViewContext}
=========================================
FORMAL SPECIFICATION CONTRACT (MUST BE STRICTLY FOLLOWED):
=========================================
- Target Role: {spec.TargetRole}
- Normalized Intent: {spec.NormalizedIntent}
- Required Features: {string.Join(", ", spec.RequestedFeatures)}
- Mandatory Context Methods: {string.Join(", ", spec.RequiredContextMethods)}
- Prohibited Methods/Actions: {string.Join(", ", spec.ForbiddenContextMethods)}

SPECIFICATION CONFORMANCE RULES:
1. Implement EXACTLY the required features in C#. Do NOT add unrequested buttons, unrequested context calls, or extra fields.
2. Do NOT omit any requested features.
3. If a method is in Prohibited Methods, you MUST NOT call it under any circumstances.

{existingViewsContext}

INSTRUCTIONS FOR MODIFYING OR DELETING EXISTING VIEWS:
- If a ""KIJELÖLT / MEGNYITOTT NÉZET"" block is present above, that is your primary source of truth for MODIFY requests - use IT, not a guess from the list below.
- Otherwise, if the prompt asks to EDIT/MODIFY an existing view by name (e.g., ""Módosítsd a Sportnap nézetet""), output the UPDATED full C# source code keeping the SAME class name base and structure.
- If the prompt asks to DELETE/REMOVE an existing view (e.g., ""Töröld a Sportnap nézetet""), return JSON with:
  {{
    ""action"": ""DELETE"",
    ""viewName"": ""ClassNameToDelete""
  }}

=========================================
AVAILABLE CLASSES AND INTERFACES (CRITICAL FOR C# COMPILATION):
=========================================
- `Role` Enum: Student, Teacher, Director. (Namespace: Kreta.Core)
- `User` Class: int Id, string Name, Role Role, string ClassName. (Namespace: Kreta.Core)
- `Grade` Class: int Id, int StudentId, int SubjectId, int Value, DateTime Date, User? Student, Subject? Subject. (Namespace: Kreta.Core)
- `Subject` Class: int Id, string Name. (Namespace: Kreta.Core)
- `Lesson` Class: int Id, string ClassName, string SubjectName, string Room, DateTime Date. (Namespace: Kreta.Core)
- `IEvolView` Interface: MUST BE IMPLEMENTED BY ALL GENERATED VIEWS! (Namespace: Kreta.Core)
- `GenericRecord` Class: int Id, string EntityType, Role CreatedByRole, int? CreatedByUserId, DateTime CreatedAt, Dictionary<string,string> Data. (Namespace: Kreta.Core)
- `IStudentContext` Interface: List<Grade> GetMyGrades(); User? GetMyProfile(); List<Lesson> GetMyLessons(); List<GenericRecord> QueryEntities(string entityType); GenericRecord? GetEntity(string entityType, int id); (Namespace: Kreta.Contexts)
- `ITeacherContext` Interface: List<User> GetMyClassStudents(); void AddGrade(int studentId, Grade grade); List<Subject> GetAllSubjects(); List<string> GetAllClasses(); List<Lesson> GetLessons(); void AddLesson(Lesson lesson); List<GenericRecord> QueryEntities(string entityType); GenericRecord? GetEntity(string entityType, int id); int SaveEntity(string entityType, Dictionary<string,string> data, int? id = null); void DeleteEntity(string entityType, int id); (Namespace: Kreta.Contexts)
- `IDirectorContext` Interface: List<User> GetAllUsers(); void CreateUser(User newUser); void DeleteUser(int userId); List<string> GetAllClasses(); void AssignClassToStudent(int studentId, string className); List<GenericRecord> QueryEntities(string entityType); GenericRecord? GetEntity(string entityType, int id); int SaveEntity(string entityType, Dictionary<string,string> data, int? id = null); void DeleteEntity(string entityType, int id); (Namespace: Kreta.Contexts)

=========================================
GENERIC ENTITY STORE — MANDATORY FOR ANY NEW CONCEPT NOT IN THE FIXED CLASSES ABOVE:
=========================================
This system CANNOT add a new C# domain class or a new Context method for every new feature the user might ask for (e.g. ""notice board"", ""NoticeMessage"", ""event calendar"", ""poll"", ""message wall"", etc.).
Those hundreds of possible future concepts MUST be built entirely on top of the ALREADY EXISTING `GenericRecord` class and the `QueryEntities` / `GetEntity` / `SaveEntity` / `DeleteEntity` methods above, using an `entityType` string that YOU choose.
1. NEVER invent a new C# class to hold the feature's data (e.g. do NOT write `public class NoticeMessage`). It does not exist anywhere else in the project and WILL NOT COMPILE.
2. NEVER invent a new Context method (e.g. do NOT call `GetNotices()` or `AddNotice(...)`). Only the methods listed above exist on `IStudentContext` / `ITeacherContext` / `IDirectorContext`.
3. Pick one short, consistent PascalCase `entityType` string for the feature (e.g. ""NoticeMessage"") and reuse it for every record of that feature.
4. Store the feature's actual fields (title, body, date, target class, etc.) as string key-value pairs inside the `Data` Dictionary<string, string> of `GenericRecord`.
5. `IStudentContext` only exposes `QueryEntities`/`GetEntity` (READ-ONLY). `SaveEntity`/`DeleteEntity` do NOT exist on it — a Student-role view can only display data, never create/edit/delete it.
6. `ITeacherContext` and `IDirectorContext` expose full read+write (`QueryEntities`, `GetEntity`, `SaveEntity`, `DeleteEntity`) — use these to let Teacher/Director views create, edit, or remove records.

=========================================
CRITICAL C# STRING & VARIABLE SYNTAX RULES:
=========================================
1. ASCII VARIABLE NAMES ONLY: Use 'atlag', 'ujJegy', 'tantargy', 'atlagText' instead of 'átlag', 'újJegy', 'tantárgy'! NEVER use Hungarian accented characters (á, é, í, ó, ö, ő, ú, ü, ű) in C# variable names, fields, method names, or labels!
2. SINGLE-LINE STRINGS ONLY: Every UI text string literal must be strictly on a single line (e.g., ""Varhato atlag: "" + atlag.ToString(""F2"")). Never split string literals across multiple lines without '+' concatenation.
3. EXPLICIT RETURN STATEMENT: The `CreateView()` method MUST end with an explicit return statement (e.g. `return mainStackPanel;` or `return mainBorder;`) on ALL code paths!
4. INTERFACE TO IMPLEMENT: You MUST implement `IEvolView` ONLY. Do NOT use non-existent interfaces like `IView`, `IAiView`, `IEvolutionView`!
5. NO NON-EXISTENT NAMESPACES: Do NOT use `Kreta.Core.Interfaces`, `Kreta.Contexts.Interfaces`. `System.Globalization` (e.g. `CultureInfo.InvariantCulture`) IS allowed and recommended for locale-safe number/date formatting.
6. MANDATORY USINGS AT THE TOP OF THE C# FILE:
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
(Add `using System.Globalization;` too if you use CultureInfo/number formatting.)
7. NO DATAGRID or FuncDataTemplate. For ListBox/ComboBox, set `ItemsSource = myCollection.Select(u => $""{{u.Name}} ({{u.Role}})"").ToList()`.
8. AVALONIA CONTROL ITEMS PROPERTY IS READ-ONLY: Use `comboBox.ItemsSource = ...` or `listBox.ItemsSource = ...`.
9. TEXTBOX PLACEHOLDER: Use `TextBox.PlaceholderText` instead of `TextBox.Watermark`.
10. NO 'Panel.Child': `StackPanel` does NOT have `.Child`! Use `.Children.Add(...)`. Only `Border` has `.Child`.
11. GRID POSITIONING: Use static method `Grid.SetColumn(control, col)` and `Grid.SetRow(control, row)`.
12. PROPERTY OVERRIDE (CS0108): Always write `public new string Name => ""...""` to explicitly hide inherited StyledElement.Name and eliminate CS0108 warnings.
13. NULLABLE CONTROL FIELDS (CS8618): Declare private UI fields as nullable (e.g. `private ListBox? _listBox;`) or initialize them at declaration (e.g. `private ListBox _listBox = new();`) to avoid CS8618 warnings.
14. NO REUSED CONTROL INSTANCES: Instantiate all UI Controls directly INSIDE the `CreateView()` method so every call builds a fresh UI tree without ""already has a visual parent"" errors.
15. CONSTRUCTORS & DATA FETCHING:
- Always provide a context-injecting constructor `public MyView(IStudentContext context)` (or ITeacherContext / IDirectorContext).
- Always provide a public parameterless constructor `public MyView() {{ }}`.
16. IEvolView INTERFACE MEMBERS:
- `public new string Name => ""A funkció magyar neve"";`
- `public string Description => ""Rövid magyar leírás"";`
- `public Control CreateView()`
17. KEEP C# SHORT & SIMPLE (Under 100 lines) so it easily fits within response token limits!

=========================================
SECURITY GUARDRAILS:
=========================================
- If a user requests a feature that violates their role permissions, return JSON with:
{{
    ""action"": ""REJECT"",
    ""viewName"": ""HibaView"",
    ""description"": ""Hozzáférés megtagadva""
}}
{(string.IsNullOrWhiteSpace(history)
    ? ""
    : $@"

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
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = $"Role: {role}. Normalized Spec: {spec.NormalizedIntent}. Original Prompt: {prompt}"
                        }
                    }
                }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                // FIX: 8192 was too low - gemini-2.5-pro "thinking" tokens
                // consumed the full quota alone, so the response was truncated (MAX_TOKENS)
                // before the actual JSON/C# code started. A higher limit prevents this.
                maxOutputTokens = 32768,
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

            var finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

            // FIX: For MAX_TOKENS (when response cuts off during thinking phase),
            // the "content" object has no "parts" field - using TryGetProperty safely handles this
            // to proceed directly to the clear MAX_TOKENS error block below.
            string? responseText = null;
            if (candidate.TryGetProperty("content", out var contentEl) &&
                contentEl.TryGetProperty("parts", out var partsElement) &&
                partsElement.ValueKind == JsonValueKind.Array &&
                partsElement.GetArrayLength() > 0 &&
                partsElement[0].TryGetProperty("text", out var textEl))
                responseText = textEl.GetString();

            if (string.IsNullOrEmpty(responseText))
            {
                if (finishReason == "MAX_TOKENS")
                    // Message INTENTIONALLY contains "MAX_TOKENS" so caller's GenerateFeatureAsync
                    // retry logic knows whether to attempt a retry (requesting shorter code)
                    // instead of giving up immediately.
                    throw new Exception(
                        "A modell válasza megszakadt, mielőtt befejezte volna a kódot (finishReason: MAX_TOKENS). " +
                        "Kérj egy egyszerűbb / kisebb funkciót, vagy próbáld újra.");

                throw new Exception($"Az AI üres választ adott vissza (finishReason: {finishReason ?? "ismeretlen"}).");
            }

            var cleanedJson = responseText.Trim();
            if (cleanedJson.StartsWith("```json"))
            {
                cleanedJson = cleanedJson.Substring(7);
                if (cleanedJson.EndsWith("```")) cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
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
                result.SourceCode = result.SourceCode.Replace("\r\n", "\n").Replace("\r", "\n");

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Hiba a válasz JSON feldolgozása közben: {ex.Message}. Nyers válasz: {responseString}");
        }
    }
}