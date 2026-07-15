namespace Kreta.Services.AI;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using Kreta.Core;

public class AiService : IAiService {
    // Alapértelmezett modell — .env-ben GEMINI_MODEL=... megadásával felülírható
    // fordítás nélkül, ha Google később elavulttá tesz egy modellnevet.
    private const string DefaultModel = "gemini-2.5-flash";

    public async Task<AiEvolveResponse> GenerateFeatureAsync(string userPrompt, Role userRole,
        string? previousError = null) {
        // --- 1. API kulcs: explicit módon olvassuk be, ne a Client "rejtett" ---
        // --- env-var keresésére hagyatkozzunk, mert az hibakereshetetlen. ---
        string? apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                         ?? System.Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey)) {
            throw new InvalidOperationException(
                "Hiányzik a GEMINI_API_KEY! Ellenőrizd, hogy a .env fájl a projekt gyökerében van, " +
                "és tartalmazza a 'GEMINI_API_KEY=...' sort.");
        }

        string model = System.Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? DefaultModel;

        string systemInstruction = """
                                   You are the automated C# and Avalonia UI compiler-agent for "EvolKréta", a self-evolving educational system.
                                   Your objective is to generate safe, compile-safe, and strictly role-appropriate C# code for a dynamic view.

                                   THE ACTIVE USER ROLE IS PROVIDED DYNAMICALLY. YOU MUST COMPLY WITH THE RBAC MATRIX BELOW:

                                   =========================================
                                   AVAILABLE CLASSES AND INTERFACES (CRITICAL FOR C# COMPILATION):
                                   =========================================
                                   - `Role` Enum: Student, Teacher, Director.
                                   - `User` Class: int Id, string Name, string Email, Role Role.
                                   - `Grade` Class: int Id, int Value, int Weight, DateTime Date, int StudentId, User Student, int SubjectId, Subject Subject.
                                   - `Subject` Class: int Id, string Name.
                                   - `IStudentContext` Interface:
                                       * List<Grade> GetMyGrades();
                                       * User? GetMyProfile();
                                   - `ITeacherContext` Interface:
                                       * List<User> GetMyClassStudents();
                                       * void AddGrade(int studentId, Grade grade);
                                   - `IDirectorContext` Interface:
                                       * void CreateUser(User newUser);
                                       * void DeleteUser(int userId);

                                   =========================================
                                   ROLE-BASED ACCESS CONTROL (RBAC) MATRIX:
                                   =========================================

                                   1. ROLE: Director (Igazgató)
                                      - Access Scope: Unrestricted global school administration and metadata.
                                      - Authorized Actions:
                                        * Can create school-wide events, tournaments (e.g., "Sakk verseny", "Iskolai Sportnap").
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
                                        * Can request personalized analytical widgets (e.g., "How many grades do I need to reach an average of 5.0?").
                                      - Critical Limitations:
                                        * CANNOT write grades, cannot modify any database records, cannot access other students' profiles or grades, cannot create school or class-level events.
                                      - Code Constraints: Can ONLY reference 'IStudentContext'.
                                        * ANY reference to 'ITeacherContext' or 'IDirectorContext' is a critical security breach.

                                   =========================================
                                   SECURITY ENFORCEMENT & SHIELD (GUARDRAILS):
                                   =========================================
                                   - If a user requests a feature that violates their role permissions (e.g., a Student requests to write grades, create a tournament, or modify other users), you MUST reject the request.
                                   - To reject a request, return a JSON object with this exact error schema:
                                     {
                                       "action": "REJECT",
                                       "target": "HibaView",
                                       "label": "Hiba",
                                       "handlerName": "HibaView",
                                       "handlerMethod": "using Avalonia.Controls;\nnamespace Kreta.Dynamic;\npublic class HibaView : UserControl, IEvolView {\n    public string ViewName => \"Hiba\";\n    public Role RequiredRole => Role.Student;\n}",
                                       "runtimeScript": "DisplayText = \"Hiba: Nincs jogosultsága ehhez a művelethez!\";"
                                     }

                                   =========================================
                                   C# COMPILATION RULES:
                                   =========================================
                                   - The returned 'handlerMethod' must contain a complete, standalone C# class implementing 'IEvolView' and inheriting from 'UserControl'.
                                   - DO NOT USE MVVM (Model-View-ViewModel) pattern! Write all logic directly in the C# Code-Behind.
                                   - DO NOT USE 'CommunityToolkit.Mvvm', 'ObservableObject', 'RelayCommand' or any external libraries.
                                   - Use ONLY 'Kreta.Core', 'Kreta.Contexts' and standard Avalonia.Controls namespaces.
                                   - NEVER use System.IO, System.Diagnostics, System.Reflection, System.Net, File, Directory, Process, Assembly.
                                   - Output ONLY valid, parsable JSON matching the AiEvolveResponse schema.
                                   """;

        string userMessage = $"Kérés (Jogosultság: {userRole}): {userPrompt}";

        if (!string.IsNullOrWhiteSpace(previousError)) {
            // Self-Healing Loop: visszaküldjük az előző hibát, hogy az AI ki tudja javítani.
            userMessage += $"\n\nFONTOS: Az előző próbálkozásod HIBÁS volt, és a rendszer elutasította. " +
                           $"Íme a pontos hiba:\n{previousError}\n\n" +
                           $"Generálj EGY TELJES, ÚJ, JAVÍTOTT megoldást, ami ezt a hibát kiküszöböli!";
        }

        var client = new Client(apiKey: apiKey);

        GenerateContentResponse response;
        try {
            response = await client.Models.GenerateContentAsync(
                model: model,
                contents: $"{systemInstruction}\n\n{userMessage}",
                config: new GenerateContentConfig { ResponseMimeType = "application/json" }
            );
        }
        catch (Exception ex) {
            throw new Exception($"A Gemini API hívás sikertelen volt (modell: {model}). Részletek: {ex.Message}", ex);
        }

        string rawText = response.Candidates?[0].Content?.Parts?[0].Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawText)) {
            throw new Exception("Az AI üres választ adott. Próbáld meg más megfogalmazással.");
        }

        string jsonPayload = ExtractJson(rawText);

        AiEvolveResponse? parsedResponse;
        try {
            parsedResponse = JsonSerializer.Deserialize<AiEvolveResponse>(
                jsonPayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex) {
            throw new Exception(
                $"Az AI válasza nem volt érvényes JSON ({ex.Message}). Nyers válasz:\n{Truncate(rawText, 500)}");
        }

        return parsedResponse ?? throw new Exception("Sikertelen JSON deszerializáció (üres eredmény).");
    }

    /// <summary>
    /// Kinyeri a JSON objektumot a modell válaszából, akkor is, ha a modell
    /// mégis markdown fence-t vagy egyéb szöveget rakott köré (a gyakorlatban
    /// előfordul annak ellenére, hogy a system prompt tiltja).
    /// </summary>
    private static string ExtractJson(string text) {
        text = text.Trim();
        text = text.Replace("```json", "").Replace("```", "").Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start >= 0 && end > start) {
            return text.Substring(start, end - start + 1);
        }

        return text;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
}