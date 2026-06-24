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
                                   You are a C# / Avalonia developer.
                                   When given a request, you must return ONLY a JSON object:
                                   {
                                     "action": "CREATE",
                                     "target": "SakkKlubView",
                                     "label": "Sakk Klub",
                                     "handlerName": "SakkKlubView",
                                     "handlerMethod": "using Avalonia.Controls;\nusing Kreta.Core;\nnamespace Kreta.Dynamic;\npublic class SakkKlubView : UserControl, IEvolView { ... }",
                                     "runtimeScript": "DisplayText = \"Sakk Klub betöltve\";"
                                   }
                                   CRITICAL: The handlerMethod must contain a complete, compile-safe C# class implementing IEvolView.
                                   Output ONLY valid JSON. No markdown fences.
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