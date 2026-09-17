#if UNITY_5_3_OR_NEWER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Unity's C# 9 profile does not provide IsExternalInit, which Roslyn requires
    /// when compiling record declarations. Keep this shim Unity-only so the
    /// engine-independent .NET CI continues to use the framework-provided type.
    /// </summary>
    public static class IsExternalInit
    {
    }
}
#endif
