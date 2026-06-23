namespace SBESharp.Roslyn.Ir;

/// <summary>Base type for all SBE type definitions declared in a schema.</summary>
/// <param name="Name">The name of the type as declared in the schema.</param>
/// <param name="Description">An optional human-readable description of the type.</param>
public abstract record SbeTypeDefinition(string Name, string? Description);
