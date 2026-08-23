# Operation Homecoming developer fixture

This is a separately loadable 1.5 developer fixture. It owns all debug
actions used by live verification; the shipping Operation Homecoming assembly
contains no `DebugAction` methods.

Build it with `RimWorldManagedDir` pointing at the authoritative 1.5 managed
directory and `OperationHomecomingAssembly` pointing at the freshly built
shipping DLL. Load this folder alongside the shipping package only during
verification. Do not include it in release staging.
