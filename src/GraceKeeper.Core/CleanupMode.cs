namespace GraceKeeper.Core;

public enum CleanupMode
{
    Boot,
    Runtime,
    SafetyNet,
    ManualForce,
    // SYSTEM-context manual invocation triggered by the dashboard via
    // schtasks /Run "GraceKeeper - Manual Cleanup". Equivalent semantics to
    // Boot/ManualForce (always bounce-eligible), but distinct so logs and
    // safety-net gates can tell them apart.
    Manual
}
