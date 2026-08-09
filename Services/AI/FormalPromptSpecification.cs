using System.Collections.Generic;

namespace Kreta.Services.AI;

/// <summary>
/// A kötetlen felhasználói promptból generált, formálisan specifikált JSON modell (DTO).
/// Ez képezi a kódgeneráló LLM számára a szigorú specifikációs szerződést.
/// </summary>
public class FormalPromptSpecification
{
    /// <summary>
    /// A megcélzott szimulált szerepkör (Student, Teacher, Director).
    /// </summary>
    public string TargetRole { get; set; } = string.Empty;

    /// <summary>
    /// A kérés tisztított, szakmai/funkcionális összefoglalója.
    /// </summary>
    public string NormalizedIntent { get; set; } = string.Empty;

    /// <summary>
    /// A felületen kötelezően megvalósítandó funkciók listája.
    /// </summary>
    public List<string> RequestedFeatures { get; set; } = new();

    /// <summary>
    /// A C# kódban kötelezően használandó kontextus metódusok (pl. GetMyGrades, AddGrade).
    /// </summary>
    public List<string> RequiredContextMethods { get; set; } = new();

    /// <summary>
    /// Az adott szerepkör számára szigorúan tiltott metódusok és műveletek.
    /// </summary>
    public List<string> ForbiddenContextMethods { get; set; } = new();

    /// <summary>
    /// Igaz, ha a kérés megsérti a Szerepkör-alapú Hozzáférés-vezérlést (RBAC).
    /// </summary>
    public bool IsRoleViolating { get; set; }

    /// <summary>
    /// Az elutasítás indoklása, ha IsRoleViolating = true.
    /// </summary>
    public string ViolationReason { get; set; } = string.Empty;
}