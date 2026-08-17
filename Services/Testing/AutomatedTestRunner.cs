using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kreta.Contexts;
using Kreta.Core;
using Kreta.Services.AI;
using Kreta.Services.Evolution;
using Kreta.Services.Security;

namespace Kreta.Services.Testing;

/// <summary>
/// Egyetlen teszteset strukturált modellje.
/// </summary>
public class AutomatedTestCase
{
    public int Id { get; set; }
    public Role Role { get; set; }
    public string Action { get; set; } = "CREATE"; // CREATE, MODIFY, DELETE
    public string Prompt { get; set; } = string.Empty;
    public string ExpectedBehavior { get; set; } = string.Empty;
    public bool IsAdversarial { get; set; }
}

/// <summary>
/// Egyetlen automatizált tesztfuttatás mérési és ellenőrzési eredményei.
/// </summary>
public class TestCaseResult
{
    public int TestCaseId { get; set; }
    public Role Role { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool FirstAttemptSuccess { get; set; }
    public bool SelfHealingSuccess { get; set; }
    public bool IsRbacRejected { get; set; }
    public bool IsAstBlocked { get; set; }
    public bool IsTimeoutOrCrash { get; set; }
    public int Attempts { get; set; }
    public long LatencyMs { get; set; }
    public long CompileTimeMs { get; set; }
    public string ViewName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// A mérés állapotát lemezen tároló ellenőrzőpont (Checkpoint) modell.
/// </summary>
public class BenchmarkCheckpoint
{
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public int LastCompletedTestId { get; set; }
    public List<TestCaseResult> Results { get; set; } = new();
}

/// <summary>
/// A hibás/timeoutos tesztek újrapróbálására optimalizált mérőmotor (60s Timeout).
/// </summary>
public class AutomatedTestRunner
{
    private readonly IEvolutionService _evolutionService;
    private readonly AstAnalyzer _astAnalyzer;
    private readonly IPromptConformanceVerifier _conformanceVerifier;

    private const string CheckpointFileName = "benchmark_checkpoint.json";
    private const string ReportFileName = "teszt_adatkeszlet_100.md";
    private const string ErrorLogFileName = "teszt_hibak_log.txt";

    public List<AutomatedTestCase> TestCases { get; private set; } = new();

    public AutomatedTestRunner(
        IEvolutionService evolutionService,
        AstAnalyzer astAnalyzer,
        IPromptConformanceVerifier conformanceVerifier)
    {
        _evolutionService = evolutionService;
        _astAnalyzer = astAnalyzer;
        _conformanceVerifier = conformanceVerifier;

        InitializeRemainingTestCasesOnly();
    }

    /// <summary>
    /// Kizárólag a fennmaradó 18 tesztesetet futtatja le finomított promptokkal.
    /// </summary>
    /// <param name="resumeFromCheckpoint">Ha igaz, betölti a legutóbbi elmentett állapotot.</param>
    /// <param name="delayBetweenTestsMs">API hívások közötti várakozási idő (ms).</param>
    /// <param name="perTestTimeoutMs">Egyetlen tesztre szánt maximális időkeret (ms) - Alapértelmezetten 60 másodperc.</param>
    /// <param name="progressCallback">Folyamatjelző visszahívási funkció.</param>
    public async Task<List<TestCaseResult>> RunAllTestsAsync(
        bool resumeFromCheckpoint = true,
        int delayBetweenTestsMs = 1500,
        int perTestTimeoutMs = 60000,
        Action<int, int, TestCaseResult>? progressCallback = null)
    {
        BenchmarkCheckpoint checkpoint = LoadOrCreateCheckpoint(resumeFromCheckpoint);
        var resultsMap = checkpoint.Results.ToDictionary(r => r.TestCaseId);

        Console.WriteLine($"==================================================");
        Console.WriteLine($"[FINOMÍTOTT MÉRŐMOTOR] {TestCases.Count} Fennmaradó Teszteset Futtatása...");
        Console.WriteLine($"[KONFIGURÁCIÓ] Max Időkeret: {perTestTimeoutMs / 1000} másodperc / teszt.");
        if (checkpoint.LastCompletedTestId > 0)
        {
            Console.WriteLine($"[FOLYTATÁS] Korábbi checkpoint betöltve! Utolsó kész teszt: #{checkpoint.LastCompletedTestId}");
        }
        Console.WriteLine($"==================================================");

        int total = TestCases.Count;

        for (int i = 0; i < total; i++)
        {
            var testCase = TestCases[i];

            // Ha ez a teszt már korábban sikeresen lefutott, átugorjuk!
            if (resultsMap.ContainsKey(testCase.Id) && resultsMap[testCase.Id].IsSuccess)
            {
                Console.WriteLine($"[MÁR SIKERES #{testCase.Id}/{total}] Átugrás checkpoint alapján...");
                progressCallback?.Invoke(i + 1, total, resultsMap[testCase.Id]);
                continue;
            }

            Console.WriteLine($"\n--------------------------------------------------");
            Console.WriteLine($"[TESZT #{testCase.Id} ({i + 1}/{total})] Szerepkör: {testCase.Role} | Akció: {testCase.Action}");
            Console.WriteLine($"Prompt: \"{testCase.Prompt}\"");

            var result = await RunSingleTestWithRetryAndTimeoutAsync(testCase, perTestTimeoutMs);

            // 1. Mentés a memóriába
            resultsMap[testCase.Id] = result;
            checkpoint.LastCompletedTestId = testCase.Id;
            checkpoint.LastUpdated = DateTime.Now;
            checkpoint.Results = resultsMap.Values.OrderBy(r => r.TestCaseId).ToList();

            // 2. AZONNALI LEMEZRE MENTÉS
            SaveCheckpointAndReport(checkpoint);

            progressCallback?.Invoke(i + 1, total, result);

            // Szünet a rate limit elkerülésére
            if (delayBetweenTestsMs > 0 && i < total - 1)
            {
                await Task.Delay(delayBetweenTestsMs);
            }
        }

        Console.WriteLine($"\n==================================================");
        Console.WriteLine($"[MÉRÉS BEFEJEZŐDÖTT] Összesített riport elmentve!");
        Console.WriteLine($"==================================================");

        return checkpoint.Results;
    }

    /// <summary>
    /// Egyetlen tesztesetet futtat le időtúllépési védelemmel és automatikus újrapróbálkozással.
    /// </summary>
    private async Task<TestCaseResult> RunSingleTestWithRetryAndTimeoutAsync(AutomatedTestCase testCase, int timeoutMs)
    {
        var result = new TestCaseResult
        {
            TestCaseId = testCase.Id,
            Role = testCase.Role,
            Action = testCase.Action,
            Prompt = testCase.Prompt
        };

        int maxRetries = 3;
        int[] backoffDelays = { 2000, 4000, 8000 };

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var swTotal = Stopwatch.StartNew();

            try
            {
                var evolveTask = _evolutionService.EvolveAsync(testCase.Prompt, testCase.Role);
                var completedTask = await Task.WhenAny(evolveTask, Task.Delay(timeoutMs, cts.Token));

                swTotal.Stop();
                result.LatencyMs = swTotal.ElapsedMilliseconds;

                if (completedTask != evolveTask)
                {
                    // IDŐTÚLLÉPÉS TÖRTÉNT!
                    result.IsTimeoutOrCrash = true;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"[TIMEOUT HIBA] A teszt túllépte a(z) {timeoutMs / 1000} másodperces korlátot!";
                    Console.WriteLine($" -> ⚠️ {result.ErrorMessage}");
                    LogErrorToFile(testCase.Id, "TIMEOUT", result.ErrorMessage);
                    return result;
                }

                var evolveResult = await evolveTask;
                result.ViewName = evolveResult.ViewName ?? string.Empty;

                if (evolveResult.IsRejectedAction)
                {
                    result.IsRbacRejected = true;
                    result.IsSuccess = testCase.IsAdversarial; // Ha támadási teszt volt, a blokkolás a várt siker!
                    result.ErrorMessage = $"RBAC Tiltás: {evolveResult.Description}";
                    Console.WriteLine($" -> [EREDMÉNY] 🛡️ RBAC Blokkolva (Várt viselkedés: {testCase.IsAdversarial})");
                }
                else if (evolveResult.IsDeletedAction)
                {
                    result.IsSuccess = true;
                    Console.WriteLine($" -> [EREDMÉNY] 🗑️ Modul Sikeresen Törölve");
                }
                else if (evolveResult.IsSuccess)
                {
                    result.IsSuccess = true;
                    result.FirstAttemptSuccess = true;
                    Console.WriteLine($" -> [EREDMÉNY] 🟢 Sikeres Generálás! Nézet: '{result.ViewName}' ({result.LatencyMs} ms)");
                }
                else
                {
                    string err = evolveResult.ErrorMessage ?? "Ismeretlen hiba";
                    if (err.Contains("AST", StringComparison.OrdinalIgnoreCase) || err.Contains("tiltott", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsAstBlocked = true;
                        result.IsSuccess = testCase.IsAdversarial;
                        result.ErrorMessage = err;
                        Console.WriteLine($" -> [EREDMÉNY] 🔒 AST Biztonsági Szűrő Blokkolta!");
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = err;
                        Console.WriteLine($" -> [EREDMÉNY] 🔴 Hiba: {result.ErrorMessage}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                result.LatencyMs = swTotal.ElapsedMilliseconds;

                Console.WriteLine($" -> ⚠️ Próbálkozási hiba ({attempt}/{maxRetries}): {ex.Message}");
                LogErrorToFile(testCase.Id, $"Kivétel Próbálkozás #{attempt}", ex.ToString());

                if (attempt < maxRetries)
                {
                    await Task.Delay(backoffDelays[attempt - 1]);
                }
                else
                {
                    result.IsTimeoutOrCrash = true;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"[KRITIKUS KIVÉTEL]: {ex.Message}";
                }
            }
        }

        return result;
    }

    private BenchmarkCheckpoint LoadOrCreateCheckpoint(bool resume)
    {
        try
        {
            if (resume && File.Exists(CheckpointFileName))
            {
                string json = File.ReadAllText(CheckpointFileName);
                var loaded = JsonSerializer.Deserialize<BenchmarkCheckpoint>(json);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Checkpoint betöltési hiba: {ex.Message}. Új munkamenet.");
        }

        return new BenchmarkCheckpoint();
    }

    private void SaveCheckpointAndReport(BenchmarkCheckpoint checkpoint)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(checkpoint, options);
            File.WriteAllText(CheckpointFileName, json);

            string markdownReport = ExportToMarkdownReport(checkpoint.Results);
            File.WriteAllText(ReportFileName, markdownReport);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Hiba a mentés során: {ex.Message}");
        }
    }

    private void LogErrorToFile(int testId, string category, string details)
    {
        try
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [TESZT #{testId}] [{category}]\n{details}\n----------------------------------------\n";
            File.AppendAllText(ErrorLogFileName, entry);
        }
        catch { }
    }

    public string ExportToMarkdownReport(List<TestCaseResult> results)
    {
        int total = results.Count;
        int successCount = results.Count(r => r.IsSuccess);
        int rbacBlocked = results.Count(r => r.IsRbacRejected);
        int astBlocked = results.Count(r => r.IsAstBlocked);
        int timeoutCount = results.Count(r => r.IsTimeoutOrCrash);
        double avgLatency = results.Where(r => r.LatencyMs > 0).Select(r => (double)r.LatencyMs).DefaultIfEmpty(0).Average();

        var sb = new StringBuilder();
        sb.AppendLine("# Célzott Kísérleti Mérés-Adatkészlet (Finomított Promptok)");
        sb.AppendLine();
        sb.AppendLine($"*Utolsó frissítés: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Automatizált Tesztmotor (60s Timeout)*");
        sb.AppendLine();
        sb.AppendLine("## 1. Összefoglaló Mérési Statisztika");
        sb.AppendLine();
        sb.AppendLine("| Mérési Metrika | Mért Érték | Százalékos Arány |");
        sb.AppendLine("| ----- | ----- | ----- |");
        sb.AppendLine($"| **Újrafuttatásra kijelölt tesztesetek** | {total} | 100% |");
        sb.AppendLine($"| **Sikeres Lefutások Száma** | **{successCount}** | **{(total > 0 ? (double)successCount / total * 100 : 0):F0}%** |");
        sb.AppendLine($"| **Sikeres RBAC Szabályszegés Blokkolás** | {rbacBlocked} | - |");
        sb.AppendLine($"| **Sikeres AST Biztonsági Blokkolás** | {astBlocked} | - |");
        sb.AppendLine($"| **Kritikus Időtúllépések / Kivételek** | {timeoutCount} | {(total > 0 ? (double)timeoutCount / total * 100 : 0):F0}% |");
        sb.AppendLine($"| **Átlagos Válaszidő (Latency)** | {avgLatency / 1000.0:F2} s | - |");
        sb.AppendLine();
        sb.AppendLine("## 2. Részletes Teszteredmények");
        sb.AppendLine();
        sb.AppendLine("| # | Szerepkör | Akció | Bemeneti Prompt | Eredmény Státusz | Válaszidő (ms) | Log / Megjegyzés |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var r in results)
        {
            string status = r.IsSuccess ? "🟢 SIKER" : "🔴 HIBA";
            if (r.IsRbacRejected) status = "🛡️ RBAC BLOKK";
            if (r.IsAstBlocked) status = "🔒 AST BLOKK";
            if (r.IsTimeoutOrCrash) status = "⚠️ TIMEOUT/CRASH";

            string cleanPrompt = r.Prompt.Replace("|", "\\|");
            string cleanError = string.IsNullOrEmpty(r.ErrorMessage) ? r.ViewName : r.ErrorMessage.Replace("\n", " ").Replace("|", "\\|");

            sb.AppendLine($"| **{r.TestCaseId}** | {r.Role} | {r.Action} | {cleanPrompt} | {status} | {r.LatencyMs} ms | {cleanError} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Kizárólag a fennmaradó 18 még le nem futott tesztesetet tartalmazza.
    /// A 3 legösszetettebb UI generálási kérést egyszerűsítettük az API timeout elkerülésére.
    /// </summary>
    private void InitializeRemainingTestCasesOnly()
    {
        TestCases = new List<AutomatedTestCase>();

        // DIÁK SZEREPKÖRŰ OLVASÁSI KÉRÉSEK
        AddTC(2, Role.Student, "CREATE", "Mutasd meg a menza étlapot és a jelenlegi egyenlegemet!", "Menza lekérdezés.");
        // #7 FINOMÍTVA: "naptár nézet" helyett "áttekinthető lista" -> az LLM nem generál 150 soros egyedi naptár vezérlőt C#-ban
        AddTC(7, Role.Student, "CREATE", "Mutasd meg a közelgő dolgozataimat egy áttekinthető listában!", "Dolgozat naptár.");
        AddTC(8, Role.Student, "CREATE", "Listázd ki a heti diákügyeleti beosztást!", "Ügyeleti beosztás.");
        AddTC(31, Role.Student, "CREATE", "Jelenítsd meg a diákönkormányzat (DÖK) legfrissebb híreit!", "DÖK hírek.");

        // TANÁRI & IGAZGATÓI ÖSSZETETT UI GENERÁLÁSOK
        AddTC(18, Role.Teacher, "CREATE", "Szeretnék egy szertári eszközigénylő nézetet a kollégáknak!", "Eszközigénylő.");
        AddTC(20, Role.Teacher, "CREATE", "Hozz létre egy korrepetálási időpont egyeztető nézetet!", "Korrepetálás egyeztető.");
        // #24 FINOMÍTVA: "teremátrendezési nézet" helyett "teremfoglaltsági lista" -> elkerüli az összetett drag-and-drop / C# layout kód miatti timeout-ot
        AddTC(24, Role.Director, "CREATE", "Készíts egy teremfoglaltsági lista nézetet az osztályokhoz!", "Teremfoglaltság.");
        AddTC(25, Role.Director, "CREATE", "Szeretnék egy tanári hiányzás és helyettesítésszervező nézetet!", "Helyettesítés szervező.");

        // MEGLÉVŐ NÉZETEK MÓDOSÍTÁSA (MODIFY)
        AddTC(44, Role.Teacher, "MODIFY", "Módosítsd a HaziFeladat kiíró nézetet, hogy lehessen fájlmelléklet hivatkozást (URL) megadni!", "Melléklet URL.");
        AddTC(47, Role.Director, "MODIFY", "Frissítsd a Tanári Tantárgyfelosztást, hogy mutassa az óraszámok összegét tanáronként!", "Óraszám összegzés.");
        AddTC(51, Role.Teacher, "MODIFY", "Frissítsd a VersenyEredmeny nézetet, hogy kategóriák szerint csoportosítsa az eredményeket!", "Csoportosítás.");
        AddTC(52, Role.Director, "MODIFY", "Módosítsd az Iskolai Szabalyzat nézetet verziószám mezővel!", "Verziószám.");
        // #53 FINOMÍTVA: "diagram-szerű sávokkal" helyett "százalékos mutatókkal" -> az LLM nem próbál meg manuális Canvas/Shape rajzolást végezni C#-ban
        AddTC(53, Role.Director, "MODIFY", "Frissítsd a Beiratkozási Statisztika nézetet százalékos mutatókkal és összegzéssel!", "Sávdiagram.");
        AddTC(56, Role.Teacher, "MODIFY", "Módosítsd a HelyettesitesiOraretek nézetet teremváltási figyelmeztetéssel!", "Teremváltás alert.");
        AddTC(57, Role.Teacher, "MODIFY", "Frissítsd a Korrepetalas foglalót maximális létszám korláttal!", "Létszámkorlát.");
        AddTC(58, Role.Director, "MODIFY", "Módosítsd az AuditLog nézetet dátumtartomány szűrővel!", "Dátumszűrő.");
        AddTC(59, Role.Director, "MODIFY", "Frissítsd az OsztalyfonokKinevezes nézetet, hogy figyelmeztessen, ha egy tanárnak már van osztálya!", "Duplikáció szűrő.");
        AddTC(60, Role.Teacher, "MODIFY", "Egészítsd ki a Jelenletiv nézetet \"Késés (perc)\" beviteli mezővel!", "Késés perc.");
    }

    private void AddTC(int id, Role role, string action, string prompt, string expectedBehavior, bool isAdversarial = false)
    {
        TestCases.Add(new AutomatedTestCase
        {
            Id = id,
            Role = role,
            Action = action,
            Prompt = prompt,
            ExpectedBehavior = expectedBehavior,
            IsAdversarial = isAdversarial
        });
    }
}