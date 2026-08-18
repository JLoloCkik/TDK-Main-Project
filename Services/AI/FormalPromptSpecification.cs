using System.Collections.Generic;

namespace Kreta.Services.AI;

/// <summary>
/// Formally specified JSON model (DTO) generated from an unstructured user prompt.
/// This forms the strict specification contract for the code generating LLM.
/// </summary>
public class FormalPromptSpecification
{
    /// <summary>
    /// The targeted simulated role (Student, Teacher, Director).
    /// </summary>
    public string TargetRole { get; set; } = string.Empty;

    /// <summary>
    /// The cleaned, professional/functional summary of the request.
    /// </summary>
    public string NormalizedIntent { get; set; } = string.Empty;

    /// <summary>
    /// List of features that must be implemented on the UI.
    /// </summary>
    public List<string> RequestedFeatures { get; set; } = new();

    /// <summary>
    /// Context methods that must be used in the C# code (e.g. GetMyGrades, AddGrade).
    /// </summary>
    public List<string> RequiredContextMethods { get; set; } = new();

    /// <summary>
    /// Methods and operations strictly forbidden for the given role.
    /// </summary>
    public List<string> ForbiddenContextMethods { get; set; } = new();

    /// <summary>
    /// True if the request violates Role-Based Access Control (RBAC).
    /// </summary>
    public bool IsRoleViolating { get; set; }

    /// <summary>
    /// The justification for the rejection, if IsRoleViolating = true.
    /// </summary>
    public string ViolationReason { get; set; } = string.Empty;
}