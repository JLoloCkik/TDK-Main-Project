namespace Kreta.Services.AI;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Google.GenAI;
using Kreta.Core;

public class AiService : IAiService {
    public async Task<AiEvolveResponse> GenerateFeatureAsync(string userPrompt, Role userRole) {
        string[] modelsToTry = ["gemini-3.5-flash", "gemini-3-flash", "gemini-2.5-flash"];


        string systemInstruction = """
                                   You are the automated C# and Avalonia UI compiler-agent for "EvolKréta", a self-evolving educational system.
                                   Your objective is to generate safe, compile-safe, and strictly role-appropriate C# code for a dynamic view.

                                   THE ACTIVE USER ROLE IS PROVIDED DYNAMICALLY. YOU MUST COMPLY WITH THE RBAC MATRIX BELOW:

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
                                   - Use only safe, standard namespaces.
                                   - Output ONLY valid, parsable JSON. No markdown backticks, no markdown ```json fences, no extra conversational text.
                                   """;

        var client = new Client();

        Exception? lastException = null;
        foreach (var model in modelsToTry) {
            for (int retry = 0; retry < 3; retry++) {
                try {
                    var response = await client.Models.GenerateContentAsync(
                        model: model,
                        contents: $"{systemInstruction}\n\nKérés (Jogosultság: {userRole}): {userPrompt}"
                    );

                    var rawJson = response.Candidates?[0].Content?.Parts?[0].Text ?? "";

                    rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();

                    var parsedResponse = JsonSerializer.Deserialize<AiEvolveResponse>(rawJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return parsedResponse ?? throw new Exception("Sikertelen JSON deszerializáció.");
                }
                catch (Exception ex) when (ex.Message.Contains("demand") || ex.Message.Contains("503") ||
                                           ex.Message.Contains("UNAVAILABLE")) {
                    lastException = ex;

                    await Task.Delay(1000 * (retry + 1));
                }
                catch (Exception ex) {
                    lastException = ex;
                    break;
                }
            }
        }

        throw new Exception($"Nem sikerült kapcsolatot lépni az AI-val: {lastException?.Message}");
    }
}