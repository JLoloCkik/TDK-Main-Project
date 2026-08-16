using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
/// Előfeldolgozó LLM szolgáltatás, amely a kötetlen nyers promptot formális JSON specifikációvá alakítja,
/// és elvégzi az korai RBAC elő-szűrést a C# kódgenerálás előtt.
/// </summary>
public class PromptRefinerService : IPromptRefinerService
{
    private readonly HttpClient _httpClient;

    public PromptRefinerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FormalPromptSpecification> RefinePromptAsync(string rawPrompt, Role role, string apiKey)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine($"[PromptRefiner] Prompt finomítása indítva: '{rawPrompt}' (Szerepkör: {role})");

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var systemInstruction = $@"You are the Formal Requirements Refiner for ""EvolKréta"".
Your sole job is to analyze an unstructured user query and transform it into a formal, strictly typed specification JSON.

ACTIVE USER ROLE: {role}

ROLE PERMISSIONS & CONTEXT METHODS:
1. Student (Diák):
   - Allowed Context: IStudentContext
   - Allowed Methods: GetMyGrades(), GetMyProfile(), GetMyLessons(), QueryEntities(), GetEntity()
   - FORBIDDEN: Any grade insertion, teacher functions, administration, SaveEntity(), DeleteEntity().
2. Teacher (Tanár):
   - Allowed Context: ITeacherContext
   - Allowed Methods: GetMyClassStudents(), AddGrade(), GetAllSubjects(), GetAllClasses(), GetLessons(), AddLesson(), QueryEntities(), GetEntity(), SaveEntity(), DeleteEntity()
   - FORBIDDEN: Director administration (CreateUser, DeleteUser, AssignClassToStudent).
3. Director (Igazgató):
   - Allowed Context: IDirectorContext
   - Allowed Methods: GetAllUsers(), CreateUser(), DeleteUser(), GetAllClasses(), AssignClassToStudent(), QueryEntities(), GetEntity(), SaveEntity(), DeleteEntity()

GENERIC ENTITY FEATURES (e.g. notice board / ""NoticeMessage"", events, polls, message walls, or any
other brand-new concept the user invents that is NOT Grade/Lesson/Subject/User):
- There is NO dedicated class or method for these. They are always built on `QueryEntities`, `GetEntity`,
  and — for Teacher/Director only — `SaveEntity`/`DeleteEntity`.
- When normalizing such a request, set `requiredContextMethods` to the actual generic method names above
  (e.g. [""QueryEntities"", ""SaveEntity""]) — NEVER invent a feature-specific method name like ""AddNotice"".
- If a Student prompt requires `SaveEntity` or `DeleteEntity` (i.e. creating/editing/deleting something),
  that is a role violation: Students may only read via `QueryEntities`/`GetEntity`.

TASK:
1. Determine if the user prompt violates their active role (e.g., Student asking to write a grade, delete users, or create/edit/delete a generic entity).
2. If role is violated, set `isRoleViolating: true` and explain why in `violationReason`.
3. Normalize the intent into explicit required features, required methods (using ONLY the real method names listed above), and forbidden methods.
4. Keep JSON response strictly conforming to the requested schema.";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"Active Role: {role}. Raw User Query: {rawPrompt}" } } }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                // A specifikáció rövid, strukturált JSON - nincs szükség "gondolkodásra" (thinking),
                // ez gyorsabbá és token-hatékonyabbá teszi a hívást, és csökkenti a MAX_TOKENS kockázatát.
                thinkingConfig = new { thinkingBudget = 0 },
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        targetRole = new { type = "STRING" },
                        normalizedIntent = new { type = "STRING" },
                        requestedFeatures = new { type = "ARRAY", items = new { type = "STRING" } },
                        requiredContextMethods = new { type = "ARRAY", items = new { type = "STRING" } },
                        forbiddenContextMethods = new { type = "ARRAY", items = new { type = "STRING" } },
                        isRoleViolating = new { type = "BOOLEAN" },
                        violationReason = new { type = "STRING" }
                    },
                    required = new[] { "targetRole", "normalizedIntent", "isRoleViolating" }
                }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[PromptRefiner] Hálózati hiba (Státusz: {response.StatusCode}): {errorMsg}");
                return CreateAndLogFallbackSpec(rawPrompt, role, "Szerver válaszhiba");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var candidate = doc.RootElement.GetProperty("candidates")[0];

            string? responseText = null;
            if (candidate.TryGetProperty("content", out var contentEl) &&
                contentEl.TryGetProperty("parts", out var partsEl) &&
                partsEl.ValueKind == JsonValueKind.Array &&
                partsEl.GetArrayLength() > 0 &&
                partsEl[0].TryGetProperty("text", out var textEl))
            {
                responseText = textEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                string? finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
                return CreateAndLogFallbackSpec(rawPrompt, role, $"Üres válasz az AI-tól (finishReason: {finishReason ?? "ismeretlen"})");
            }

            var spec = JsonSerializer.Deserialize<FormalPromptSpecification>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var resultSpec = spec ?? CreateFallbackSpec(rawPrompt, role);
            LogSpecification(resultSpec);

            return resultSpec;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PromptRefiner] Hiba a specifikáció előállításakor: {ex.Message}");
            return CreateAndLogFallbackSpec(rawPrompt, role, ex.Message);
        }
    }

    private void LogSpecification(FormalPromptSpecification resultSpec)
    {
        Console.WriteLine("[PromptRefiner] AI ÁLTAL FINOMÍTOTT SPECIFIKÁCIÓ:");
        Console.WriteLine($" - Cél szerepkör: {resultSpec.TargetRole}");
        Console.WriteLine($" - Normalizált szándék: {resultSpec.NormalizedIntent}");
        Console.WriteLine($" - Kért funkciók: {string.Join(", ", resultSpec.RequestedFeatures)}");
        Console.WriteLine($" - Szükséges kontextus metódusok: {string.Join(", ", resultSpec.RequiredContextMethods)}");
        Console.WriteLine($" - Tiltott metódusok: {string.Join(", ", resultSpec.ForbiddenContextMethods)}");
        Console.WriteLine($" - Szerepkör megsértve: {resultSpec.IsRoleViolating}");
        if (resultSpec.IsRoleViolating)
        {
            Console.WriteLine($" - Indoklás: {resultSpec.ViolationReason}");
        }

        Console.WriteLine("=========================================");
    }

    private FormalPromptSpecification CreateAndLogFallbackSpec(string rawPrompt, Role role, string reason)
    {
        Console.WriteLine($"[PromptRefiner] Tartalék specifikáció használata (Ok: {reason})");
        var spec = CreateFallbackSpec(rawPrompt, role);
        LogSpecification(spec);
        return spec;
    }

    private FormalPromptSpecification CreateFallbackSpec(string rawPrompt, Role role)
    {
        return new FormalPromptSpecification
        {
            TargetRole = role.ToString(),
            NormalizedIntent = rawPrompt,
            RequestedFeatures = new List<string> { rawPrompt },
            IsRoleViolating = false
        };
    }
}