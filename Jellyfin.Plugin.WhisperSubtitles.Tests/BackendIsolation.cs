using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Finds types that name a concrete backend from outside the backend folder.
/// </summary>
/// <remarks>
/// The bound is worth knowing before trusting a green run. This reads signatures:
/// base types, implemented interfaces, field and property types, and the
/// parameter and return types of constructors and methods. It does not read
/// method bodies, so a concrete backend constructed inside a method and never
/// named in a signature is invisible to it. That gap is the reason the greppable
/// invariants in #52 exist beside this rather than instead of it.
/// </remarks>
internal static class BackendIsolation
{
    /// <summary>
    /// Names every type outside <paramref name="backendNamespace"/> whose signatures
    /// name a concrete implementation of <see cref="ITranscriptionBackend"/>.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <param name="backendNamespace">The namespace concrete backends are allowed to live in.</param>
    /// <returns>The offending type names, each with the backend it named.</returns>
    public static IReadOnlyList<string> Violations(Assembly assembly, string backendNamespace)
    {
        var concreteBackends = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITranscriptionBackend).IsAssignableFrom(t))
            .ToHashSet();

        if (concreteBackends.Count == 0)
        {
            return Array.Empty<string>();
        }

        var found = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (IsInside(type, backendNamespace) || concreteBackends.Contains(type))
            {
                continue;
            }

            foreach (var named in SignatureTypes(type))
            {
                if (concreteBackends.Contains(named))
                {
                    found.Add($"{type.FullName} names {named.FullName}");
                }
            }
        }

        return found;
    }

    private static bool IsInside(Type type, string backendNamespace) =>
        type.Namespace is not null
        && (string.Equals(type.Namespace, backendNamespace, StringComparison.Ordinal)
            || type.Namespace.StartsWith(backendNamespace + ".", StringComparison.Ordinal));

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var i in type.GetInterfaces())
        {
            yield return i;
        }

        foreach (var f in type.GetFields(All))
        {
            yield return f.FieldType;
        }

        foreach (var p in type.GetProperties(All))
        {
            yield return p.PropertyType;
        }

        foreach (var c in type.GetConstructors(All))
        {
            foreach (var p in c.GetParameters())
            {
                yield return p.ParameterType;
            }
        }

        foreach (var m in type.GetMethods(All))
        {
            yield return m.ReturnType;

            foreach (var p in m.GetParameters())
            {
                yield return p.ParameterType;
            }
        }
    }
}
