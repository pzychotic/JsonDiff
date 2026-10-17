> [!NOTE]
> 🚧 Work in progress 🚧

# JsonDiff

[![GitHub License](https://badgen.net/github/license/pzychotic/JsonDiff)](https://github.com/pzychotic/JsonDiff/blob/main/LICENSE)

A JSON diff viewer.

## Usage

Open two JSON files side by side (File ▸ Open Left/Right, drag & drop, or `JsonDiff.exe left.json right.json`).
Dropping two files at once fills both sides.

- **Red**: only on this side; the other side shows a hatched gap so both stay aligned.
- **Yellow**: same key, different value.
- **Purple**: same key, different type (e.g. a number vs. an object).

Object keys are matched by name and shown alphabetically by default (View ▸ Sort Keys turns this off).
Array elements are aligned like a text diff; a changed element is compared field by field.

## Build

### From the command line

Prerequisites:
- .NET SDK 10.0 - install from https://dotnet.microsoft.com/

Build and run:
1. Build:
   ```
   dotnet build
   ```
2. Test (optional):
   ```
   dotnet test
   ```
3. Run:
   ```
   dotnet run --project Source\JsonDiff\JsonDiff.csproj
   ```

### From Visual Studio 2026

Prerequisites:
- .Net Desktop development workload
- .Net 10.0 Runtime
- .Net SDK

Just open ```JsonDiff.slnx``` build and run.

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Microsoft.Xaml.Behaviors.Wpf](https://github.com/microsoft/XamlBehaviorsWpf)
