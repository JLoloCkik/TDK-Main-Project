using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

public class AiService : IAiService
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = ""; // Runtime injektálja

    public AiService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null)
    {
        int delay = 1000;
        Exception? lastException = null;

        for (int i = 0; i < 5; i++)
        {
            try
            {
                return await CallGeminiApiInternalAsync(prompt, role, history);
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        throw new Exception($"Nem sikerült elérni a Gemini API-t 5 kísérlet után sem. Részletek: {lastException?.Message}");
    }

    private async Task<AiEvolveResponse> CallGeminiApiInternalAsync(string prompt, Role role, string? history)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-09-2025:generateContent?key={ApiKey}";

        var systemPrompt = $@"Ön az EvolKréta rendszer mesterséges intelligenciája. Feladata egy tiszta C# osztály generálása, amely megvalósítja az `IEvolView` interfészt.
Az aktuális szerepkör: {role}.
A generált kód tiszta Avalonia UI kóddal (nem XAML fájlokkal!) építsen fel egy szép felhasználói felületet.

A kapott szerepkörtől függően az alábbi kontextusokat injektálhatja és használhatja a konstruktorban:
1. Student esetén (IStudentContext):
   - GetMyProfile() -> User?
   - GetMyGrades() -> List<Grade>
   - GetMyLessons() -> List<Lesson>

2. Teacher esetén (ITeacherContext):
   - GetMyClassStudents() -> List<User>
   - AddGrade(studentId, grade)
   - GetAllSubjects() -> List<Subject>
   - GetAllClasses() -> List<string>
   - GetLessons() -> List<Lesson>
   - AddLesson(lesson)

3. Director esetén (IDirectorContext):
   - GetAllUsers() -> List<User>
   - CreateUser(newUser)
   - DeleteUser(userId)
   - GetAllClasses() -> List<string>
   - AssignClassToStudent(studentId, className)

Generáljon JSON formátumú választ, amely megfelel az alábbi szerkezetnek:
{{
  ""viewName"": ""A funkció szép magyar megnevezése"",
  ""description"": ""A funkció rövid leírása"",
  ""sourceCode"": ""A teljes C# forráskód osztály megvalósítással"",
  ""testCode"": ""// opcionális teszt kód""
}}

Az osztályszerkezet legyen teljesen önálló, ne definiáljon újra meglévő osztályokat (mint User, Role, Subject, Grade, Lesson, IEvolView), hanem használja azokat a Kreta.Core és Kreta.Contexts névterekből!";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"Role: {role}. Prompt: {prompt}" } } }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
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
                        testCode = new { type = "STRING" }
                    },
                    required = new[] { "viewName", "description", "sourceCode" }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var responseText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (responseText == null)
            throw new Exception("Az AI üres választ adott.");

        var result = JsonSerializer.Deserialize<AiEvolveResponse>(responseText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Nem sikerült deszerializálni az AI választ.");

        return result;
    }
}