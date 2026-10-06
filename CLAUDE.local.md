# CLAUDE.local.md

## Code organization

Small request/result records or structs that exist only to pass context into, or answers out of, one big class (e.g. a dialog ViewModel's `ClockTimePickerRequest`/`ClockTimePickerResult`) live in that class's file, after the class, in the same namespace - not in separate files.
