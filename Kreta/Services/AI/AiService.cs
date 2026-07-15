using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

public class AiService : IAiService
{
    private readonly HttpClient _httpClient;

    public AiService()
    {
        _httpClient = new HttpClient();

        // DotNetEnv betöltése a helyi .env fájlból, ha létezik
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
        // 1. Környezeti változók ellenőrzése
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        // 2. Ha ott nincs, .env fájlból olvassuk be
        if (string.IsNullOrEmpty(key))
        {
            try
            {
                key = DotNetEnv.Env.GetString("GEMINI_API_KEY")
                      ?? DotNetEnv.Env.GetString("GOOGLE_API_KEY");
            }
            catch
            {
                // DotNetEnv nincs konfigurálva vagy üres
            }
        }

        return key ?? string.Empty;
    }

    public async Task<string> GenerateFeatureAsync(string prompt)
    {
        var response = await GenerateFeatureAsync(prompt, Role.Student, null);
        return response.SourceCode;
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

        // Csak v1beta végpontokat használunk, mivel a strukturált JSON és a rendszer-szabályzat csak itt támogatott megbízhatóan.
        // Bevezettük a rendkívül stabil gemini-2.0-flash és gemini-1.5-pro modelleket is.
        var fallbackMatrix = new[]
        {
            new { Version = "v1beta", Model = "gemini-2.5-flash" },
            new { Version = "v1beta", Model = "gemini-2.0-flash" },
            new { Version = "v1beta", Model = "gemini-1.5-pro" },
            new { Version = "v1beta", Model = "gemini-2.5-pro" }
        };

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

                    var response = await CallGeminiApiInternalAsync(prompt, role, history, apiKey, attempt.Version,
                        attempt.Model);
                    Console.WriteLine($"[AI Kapcsolat] SIKERES! Használt végpont: {attempt.Version}/{attempt.Model}");
                    return response;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine(
                        $"[AI Kapcsolat - Próbálkozás sikertelen ({attempt.Version}/{attempt.Model})]: {ex.Message}");

                    // Ha hitelesítési hiba van (403), felesleges próbálkozni a többi modellel is
                    if (ex.Message.Contains("403") || ex.Message.Contains("API_KEY_INVALID"))
                    {
                        throw new Exception(
                            $"Érvénytelen Gemini API kulcs! Ellenőrizze a beállításokat. Részletek: {ex.Message}");
                    }

                    // Ha nem található a modell (404) vagy hibás a kérés (400), azonnal lépünk a következő modellre
                    if (ex.Message.Contains("404") || ex.Message.Contains("400"))
                    {
                        break;
                    }

                    // Átmeneti szerverhibák (503 túlterheltség, 429 rátakorlát) esetén exponenciális várakozás után újrapróbáljuk
                    if (retry < maxRetries - 1 && (ex.Message.Contains("503") || ex.Message.Contains("429") ||
                                                   ex.Message.Contains("Hálózati hiba")))
                    {
                        Console.WriteLine(
                            $"[AI Kapcsolat] Átmeneti hiba észlelve. Várakozás {delayMs} ms-ig az újrapróbálkozás előtt...");
                        await Task.Delay(delayMs);
                        delayMs *= 2; // Exponenciális növekedés a várakozási időben
                    }
                    else
                    {
                        // Ha elfogytak a próbálkozások ennél a modellnél, továbblépünk a következőre a mátrixban
                        break;
                    }
                }
            }
        }

        throw new Exception(
            $"Nem sikerült elérni a Gemini API-t egyik konfigurációval sem. Legutolsó hiba:\n{lastException?.Message}");
    }

    private async Task<AiEvolveResponse> CallGeminiApiInternalAsync(string prompt, Role role, string? history,
        string apiKey, string apiVersion, string modelName)
    {
        var url =
            $"https://generativelanguage.googleapis.com/{apiVersion}/models/{modelName}:generateContent?key={apiKey}";

        // A továbbfejlesztett, szigorított, hibamentes generálást kikényszerítő rendszer-szabályzat
        var systemInstruction =
            $@"You are the automated C# and Avalonia UI compiler-agent for ""EvolKréta"", a self-evolving educational system.
Your objective is to generate safe, compile-safe, and strictly role-appropriate C# code for a dynamic view.

THE ACTIVE USER ROLE IS PROVIDED DYNAMICALLY. YOU MUST COMPLY WITH THE RBAC MATRIX BELOW:

=========================================
AVAILABLE CLASSES AND INTERFACES (CRITICAL FOR C# COMPILATION):
=========================================
- `Role` Enum: Student, Teacher, Director.
- `User` Class: int Id, string Name, Role Role, string ClassName. (WARNING: NO 'Email' property exists in the User class. Never try to access user.Email!).
- `Grade` Class: int Id, int StudentId, int SubjectId, int Value, DateTime Date, User? Student, Subject? Subject. (WARNING: NO 'Weight' property exists in the Grade class. Never try to access grade.Weight!).
- `Subject` Class: int Id, string Name.
- `Lesson` Class: int Id, string ClassName, string SubjectName, string Room, DateTime Date.
- `IStudentContext` Interface:
    * List<Grade> GetMyGrades();
    * User? GetMyProfile();
    * List<Lesson> GetMyLessons();
- `ITeacherContext` Interface:
    * List<User> GetMyClassStudents();
    * void AddGrade(int studentId, Grade grade);
    * List<Subject> GetAllSubjects();
    * List<string> GetAllClasses();
    * List<Lesson> GetLessons();
    * void AddLesson(Lesson lesson);
- `IDirectorContext` Interface:
    * List<User> GetAllUsers();
    * void CreateUser(User newUser);
    * void DeleteUser(int userId);
    * List<string> GetAllClasses();
    * void AssignClassToStudent(int studentId, string className);

=========================================
ROLE-BASED ACCESS CONTROL (RBAC) MATRIX:
=========================================

1. ROLE: Director (Igazgató)
   - Access Scope: Unrestricted global school administration and metadata.
   - Authorized Actions:
     * Can create school-wide events, tournaments (e.g., ""Sakk verseny"", ""Iskolai Sportnap"").
     * Can create entirely new database tables, global tabs, classes, and subjects.
     * Can manage users, assign teachers to classes, and alter school-wide parameters.
   - Code Constraints: Can access and inject 'IDirectorContext', 'ITeacherContext', and 'IStudentContext'.

2. ROLE: Teacher (Tanár)
   - Access Scope: Restricted strictly to classrooms and subjects they actively teach.
   - Authorized Actions:
     * Can write/modify grades, log attendance, and assign homework for their students.
     * Can request class-level statistics, custom grading curves, or classroom-specific quizzes.
   - Critical Limitations:
     * CANNOT modify global school parameters, cannot create school-wide events/tabs, cannot delete users, cannot alter database schemas.
   - Code Constraints: Can ONLY reference 'ITeacherContext' and 'IStudentContext'.
     * ANY reference to 'IDirectorContext' is a critical security breach.

3. ROLE: Student (Diák)
   - Access Scope: Read-only personal student data and advanced individual analytics.
   - Authorized Actions:
     * Can query own grades, calculate GPA, and view study schedules.
     * Can request personalized analytical widgets (e.g., ""How many grades do I need to reach an average of 5.0?"").
   - Critical Limitations:
     * CANNOT write grades, cannot modify any database records, cannot access other students' profiles or grades, cannot create school or class-level events.
   - Code Constraints: Can ONLY reference 'IStudentContext'.
     * ANY reference to 'ITeacherContext' or 'IDirectorContext' is a critical security breach.

=========================================
CRITICAL C# COMPILATION RULES (VIOLATION WILL BREAK THE BUILD):
=========================================
1. NO TOP-LEVEL STATEMENTS: Do NOT write code outside of class structures. Absolutely everything must reside within standard class declarations, properties, constructors, or methods. No loose statements in the file!
2. NO 'Student' TYPE: There is NO 'Student' class. Students are represented by the 'User' class where 'user.Role == Role.Student'.
3. NO 'Weight' PROPERTY: Do NOT reference 'grade.Weight' or 'Weight' on the Grade class. It does not exist and will break compilation!
4. ABSOLUTELY NO DATAGRID: The Avalonia DataGrid NuGet package is NOT referenced in this project. You must NEVER use `DataGrid`, `DataGridTextColumn`, `DataGridLength`, `DataGridHeadersVisibility`, or any other DataGrid classes. If you need to display tables, build them manually using standard controls like `Grid` (with RowDefinitions/ColumnDefinitions), `Border`, `ListBox`, or nested `StackPanel` layouts.
5. NO MVVM: Write all interactive logic, events, and styling directly in C# Code-Behind inside your View class.
6. NO CYRILLIC OR ALIEN CHARACTERS: All code characters must be standard ASCII/Latin characters. Do NOT use Cyrillic, Russian, or other non-Latin character sets under any circumstances!
7. NAMESPACE AND IMPORTS: You must always declare a namespace (e.g., `namespace Kreta.Dynamic;`) and include these exact imports at the top:
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using Avalonia;
   using Avalonia.Controls;
   using Avalonia.Layout;
   using Avalonia.Media;
   using Kreta.Core;
   using Kreta.Contexts;
8. UNIQUE CLASS NAME: The class name must be completely unique (e.g., `MyFeature_UniqueString`). It must inherit from `UserControl` and implement `IEvolView`.
9. IEvolView INTERFACE MEMBERS (MUST BE IMPLEMENTED EXACTLY):
   - `public string Name => ""A funkció magyar neve"";`
   - `public string Description => ""A funkció rövid magyar leírása"";` (MANDATORY property, do not omit!)
   - `public Control CreateView()` (THE RETURN TYPE MUST BE EXACTLY `Control`. Inside, return a control (like a Panel, Grid or Border) that is or inherits from `Control`.)

=========================================
C# CLASS STRUCTURE BONES TEMPLATE:
=========================================
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

namespace Kreta.Dynamic;

public class MyUniqueFeatureView : UserControl, IEvolView
{{
    public string Name => ""A funkció magyar neve"";
    public string Description => ""Rövid magyar leírás"";
    
    private readonly IStudentContext _context;

    public MyUniqueFeatureView(IStudentContext context)
    {{
        _context = context;
    }}

    public Control CreateView()
    {{
        var panel = new StackPanel {{ Spacing = 10, Margin = new Thickness(10) }};
        panel.Children.Add(new TextBlock {{ Text = ""Szia Világ!"", FontSize = 16 }});
        return panel;
    }}
}}

=========================================
SECURITY ENFORCEMENT & SHIELD (GUARDRAILS):
=========================================
- If a user requests a feature that violates their role permissions (e.g., a Student requests to write grades, create a tournament, or modify other users), you MUST reject the request.
- To reject a request, return a JSON object with this exact error schema:
  {{
    ""action"": ""REJECT"",
    ""target"": ""HibaView"",
    ""label"": ""Hiba"",
    ""handlerName"": ""HibaView"",
    ""handlerMethod"": ""using Avalonia.Controls;\nnamespace Kreta.Dynamic;\npublic class HibaView : UserControl, IEvolView {{\n    public string Name => \""Hiba\"";\n    public string Description => \""Hozzáférés megtagadva\"";\n    public Control CreateView() => new Label {{ Content = \""Hiba\"" }};\n}}"",
    ""runtimeScript"": ""DisplayText = \""Hiba: Nincs jogosultsága ehhez a művelethez!\"";""
  }}
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
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        viewName = new { type = "STRING" },
                        description = new { type = "STRING" },
                        sourceCode = new { type = "STRING" },
                        testCode = new { type = "STRING" },

                        // Opcionális mezők az RBAC elutasítás (REJECT) támogatásához
                        action = new { type = "STRING" },
                        handlerMethod = new { type = "STRING" }
                    },
                    required = new[] { "viewName", "description", "sourceCode" }
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
            var responseText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(responseText))
                throw new Exception("Az AI üres választ adott vissza.");

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

            // Ha az AI elutasította a kérést az RBAC szabályzat megsértése miatt
            if (root.TryGetProperty("action", out var actionProp) && actionProp.GetString() == "REJECT")
            {
                Console.WriteLine(
                    "[RBAC Guardrail] Az AI elutasította a generálási kérést biztonsági szabályzat megsértése miatt.");

                // Generálunk egy gyönyörű és biztonságosan leforduló Avalonia UI Hiba panelt
                var safeHibaCode = @"using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

namespace Kreta.Dynamic;

public class HibaView : UserControl, IEvolView
{
    public string Name => ""Hozzáférés Megtagadva"";
    public string Description => ""Hozzáférés-korlátozás hiba."";

    public Control CreateView()
    {
        var panel = new StackPanel
        {
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = ""❌ Hozzáférés megtagadva!"",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse(""#C0392B"")),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = ""Nincs jogosultsága a kért funkció végrehajtásához (RBAC hiba)."",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse(""#7B241C"")),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(""#FDEDEC"")),
            BorderBrush = new SolidColorBrush(Color.Parse(""#F5B7B1"")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(24),
            Margin = new Avalonia.Thickness(16),
            Child = panel
        };
    }
}";
                return new AiEvolveResponse
                {
                    ViewName = "Hozzáférés Megtagadva",
                    Description = "Szerepkör-alapú elutasítás.",
                    SourceCode = safeHibaCode,
                    TestCode = ""
                };
            }

            // Normál visszatérési adatok beolvasása
            var result = JsonSerializer.Deserialize<AiEvolveResponse>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Nem sikerült deszerializálni a generált választ.");

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Hiba a válasz JSON feldolgozása közben: {ex.Message}. Nyers válasz: {responseString}");
        }
    }
}