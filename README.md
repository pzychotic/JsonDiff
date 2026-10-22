> [!NOTE]
> 🚧 Work in progress 🚧

# JsonDiff

[![GitHub License](https://badgen.net/github/license/pzychotic/JsonDiff)](https://github.com/pzychotic/JsonDiff/blob/main/LICENSE)
[![CI](https://github.com/pzychotic/JsonDiff/actions/workflows/ci.yml/badge.svg)](https://github.com/pzychotic/JsonDiff/actions/workflows/ci.yml)

A JSON diff viewer.

## Usage

Open two JSON files side by side (File ▸ Open Left/Right, drag & drop, or `JsonDiff.exe left.json right.json`).
Dropping two files at once fills both sides.

- **Red**: only on this side; the other side shows a hatched gap so both stay aligned.
- **Yellow**: same key, different value.
- **Purple**: same key, different type (e.g. a number vs. an object).
- **Gray**: ignored by a match rule; shown dimmed and never counted as a difference.

Object keys are matched by name and shown alphabetically by default (View ▸ Sort Keys turns this off).
Array elements are aligned like a text diff; a changed element is compared field by field.
Objects in an array that a match rule identifies are matched by their key values instead, so reordered or edited
items still line up; they are shown sorted by those values (or in the left file's order when Sort Keys is off).
Items with the same values are paired in order of appearance.

### Match rules

The rules are read from `%APPDATA%\JsonDiff\MatchRules.json`, or else from `MatchRules.json` next to
`JsonDiff.exe`. The shipped file contains no rules, only a commented example; copy it to `%APPDATA%\JsonDiff` to
start your own. File ▸ Reload rereads the rules.

```jsonc
{
  "ArrayKeys": [
    {
      // Optional: the rule applies only when all conditions hold; a list means "any of these".
      "When": { "Type": ["Sensor", "Camera"] },
      // The values that identify an object, here e.g. { "Type": "Sensor", "Fields": [{ "Name": "Id", "Value": "s1" }] }.
      "Key": ["Type", "Fields[Name=Id].Value"]
    },
    // Without "When", a rule applies to objects that have at least one of its key values.
    { "Key": ["Type", "Name"] }
  ],
  "IgnoreKeys": [
    // A property name is ignored in every object, at any depth.
    "Timestamp",
    // With "When", only in objects where all conditions hold.
    { "When": { "Type": "Sensor" }, "Keys": ["Revision"] }
  ]
}
```

Ignored members are still shown, dimmed, but never count as a difference, and array elements that differ only in
ignored members are aligned as equal.

The first rule that applies to an object wins. Paths are property names separated by dots; `[field=value]` after an
array property selects the element whose `field` is the string `value`.

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
