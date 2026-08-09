using System.Diagnostics.CodeAnalysis;

// This assembly is a tool this repository runs on itself and it ships nowhere, so
// it is not what the coverage floor is about. Excluding it is not only tidiness:
// the report writes every file name relative to the deepest directory all covered
// sources share, so an assembly outside the plugin moves that root up and every
// path the floor's list names stops matching, which the check reports as files
// that were never compiled.
[assembly: ExcludeFromCodeCoverage]
