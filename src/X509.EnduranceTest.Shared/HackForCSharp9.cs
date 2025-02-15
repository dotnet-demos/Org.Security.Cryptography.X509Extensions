﻿/// <summary>
/// Hack to get C# 9.0 compiled for .Net Framework projects
/// https://blog.ndepend.com/using-c9-record-and-init-property-in-your-net-framework-4-x-net-standard-and-net-core-projects/
/// </summary>
/// <remarks>
/// In .Net 5 the below class is already present. So better do conditional compilation for below code.
/// </remarks>
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}