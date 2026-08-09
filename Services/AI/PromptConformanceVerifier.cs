using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
/// A generált C# kód és a formális specifikáció közötti megfelelőség eredményét tartalmazó DTO.
/// </summary>
public class PromptConformanceResult
{
    /// <summary>
    /// Igaz, ha a kód 100%-ban megfelel a specifikációnak (se alul-, se túlgenerálás nincs).
    /// </summary>
    public bool IsStrictlyConformant { get; set; }

    /// <summary>
    /// A kötelezően kért metódusok lefedettségi aránya (0.0 - 1.0, azaz 0% - 100%).
    /// </summary>
    public double CoverageRate { get; set; }

    /// <summary>
    /// A talált nem engedélyezett, tiltott vagy túlgenerált hívások száma (Scope Creep Count).
    /// </summary>
    public int ScopeCreepCount { get; set; }

    /// <summary>
    /// A kódból hiányzó, de a specifikáció által előírt kötelező metódusok listája.
    /// </summary>
    public List<string> MissingRequiredMethods { get; set; } = new();

    /// <summary>
    /// A kódban észlelt tiltott vagy túlgenerált metódushívások listája.
    /// </summary>
    public List<string> DetectedForbiddenCalls { get; set; } = new();

    /// <summary>
    /// A kódban azonosított összes metódushívás listája auditáláshoz.
    /// </summary>
    public List<string> IdentifiedMethodCalls { get; set; } = new();

    /// <summary>
    /// Részletes ellenőrzési naplóüzenetek a TDK mérésekhez.
    /// </summary>
    public List<string> ValidationLogs { get; set; } = new();

    /// <summary>
    /// Szöveges összefoglaló az ellenőrzés eredményéről.
    /// </summary>
    public string Summary => IsStrictlyConformant
        ? $"[CONFORMANCE SUCCESS] 100%-os konformitás! Lefedettség: {CoverageRate * 100:F0}%, Mellékhatások: {ScopeCreepCount}."
        : $"[CONFORMANCE FAILURE] Eltérés a specifikációtól! Lefedettség: {CoverageRate * 100:F0}%, Hiányzó: {MissingRequiredMethods.Count}, Tiltott/Extra: {ScopeCreepCount}.";
}

/// <summary>
/// A Prompt-to-Code Conformance ellenőrző szolgáltatás interfésze.
/// </summary>
public interface IPromptConformanceVerifier
{
    /// <summary>
    /// Összeveti a generált C# forráskódot a formális specifikációval, ellenőrizve az alul- és túlgenerálást.
    /// </summary>
    /// <param name="csharpSource">Az AI által generált C# kód.</param>
    /// <param name="spec">A Formális Prompt Refiner által előállított specifikáció.</param>
    /// <returns>A validációs vizsgálat részletes eredménye.</returns>
    PromptConformanceResult VerifyConformance(string csharpSource, FormalPromptSpecification spec);
}

/// <summary>
/// Roslyn AST (Abstract Syntax Tree) alapú ellenőrző motor, amely garantálja, 
/// hogy a generált kód se nem csinál többet, se nem csinál kevesebbet a kértnél.
/// </summary>
public class PromptConformanceVerifier : IPromptConformanceVerifier
{
    public PromptConformanceResult VerifyConformance(string csharpSource, FormalPromptSpecification spec)
    {
        var result = new PromptConformanceResult();

        if (string.IsNullOrWhiteSpace(csharpSource))
        {
            result.ValidationLogs.Add("A megadott C# forráskód üres.");
            result.IsStrictlyConformant = false;
            return result;
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpSource);
            var root = syntaxTree.GetRoot();
            
            var invocationNodes = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            var identifiedCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var invocation in invocationNodes)
            {
                string callText = invocation.Expression.ToString();
                
                if (callText.Contains("."))
                {
                    callText = callText.Split('.').Last();
                }

                identifiedCalls.Add(callText);
            }

            result.IdentifiedMethodCalls = identifiedCalls.ToList();
            result.ValidationLogs.Add($"Aknázott metódushívások az AST-ban: {string.Join(", ", identifiedCalls)}");


            int requiredCount = spec.RequiredContextMethods?.Count ?? 0;
            int matchedRequiredCount = 0;

            if (spec.RequiredContextMethods != null && requiredCount > 0)
            {
                foreach (var reqMethod in spec.RequiredContextMethods)
                {
                   
                    string cleanReqName = reqMethod.Replace("()", "").Trim();

                    bool isPresent = identifiedCalls.Any(call => call.Equals(cleanReqName, StringComparison.OrdinalIgnoreCase));
                    if (isPresent)
                    {
                        matchedRequiredCount++;
                    }
                    else
                    {
                        result.MissingRequiredMethods.Add(cleanReqName);
                        result.ValidationLogs.Add($"[ALULGENERÁLÁS] Hiányzó kötelező metódus: '{cleanReqName}'");
                    }
                }

                result.CoverageRate = (double)matchedRequiredCount / requiredCount;
            }
            else
            {
                result.CoverageRate = 1.0;
            }
            
            if (spec.ForbiddenContextMethods != null)
            {
                foreach (var forbiddenMethod in spec.ForbiddenContextMethods)
                {
                    string cleanForbiddenName = forbiddenMethod.Replace("()", "").Trim();

                    bool isDetected = identifiedCalls.Any(call => call.Equals(cleanForbiddenName, StringComparison.OrdinalIgnoreCase));
                    if (isDetected)
                    {
                        result.DetectedForbiddenCalls.Add(cleanForbiddenName);
                        result.ValidationLogs.Add($"[TÚLGENERÁLÁS / TILTOTT HÍVÁS] Megengedhetetlen metódushívás észlelve: '{cleanForbiddenName}'");
                    }
                }
            }
            
            if (spec.TargetRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var studentForbiddenList = new[] { "AddGrade", "CreateUser", "DeleteUser", "AssignClassToStudent", "AddLesson" };
                foreach (var forbidden in studentForbiddenList)
                {
                    if (identifiedCalls.Contains(forbidden) && !result.DetectedForbiddenCalls.Contains(forbidden))
                    {
                        result.DetectedForbiddenCalls.Add(forbidden);
                        result.ValidationLogs.Add($"[RBAC SÉRÉS] Diák szerepkörben tiltott hívás található: '{forbidden}'");
                    }
                }
            }

            result.ScopeCreepCount = result.DetectedForbiddenCalls.Count;
            
            result.IsStrictlyConformant = (result.MissingRequiredMethods.Count == 0) && (result.ScopeCreepCount == 0);

            Console.WriteLine("=========================================");
            Console.WriteLine($"[PromptConformanceVerifier] EREDMÉNY:");
            Console.WriteLine($" - {result.Summary}");
            Console.WriteLine("=========================================");

            return result;
        }
        catch (Exception ex)
        {
            result.ValidationLogs.Add($"AST elemezési hiba történt: {ex.Message}");
            result.IsStrictlyConformant = false;
            return result;
        }
    }
}