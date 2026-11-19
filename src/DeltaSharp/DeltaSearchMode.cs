namespace DeltaSharp;

/// <summary>Controls how much work the encoder spends searching for source matches.</summary>
public enum DeltaSearchMode
{
    /// <summary>
    /// Searches the full candidate chain unless the remaining target is matched.
    /// This preserves the existing behavior and can be slow on repetitive inputs.
    /// It does not guarantee the smallest possible patch.
    /// </summary>
    Exhaustive = 0,

    /// <summary>
    /// Examines at most 256 source candidates at each target position.
    /// Patches still reconstruct the target exactly, but may be much larger.
    /// This bounds search effort, not elapsed time, and is not faster for every input.
    /// </summary>
    Bounded = 1
}
