# Drop Renamer

Drop Renamer is a small Windows app that renames files using the destination folder name and a sequential number.

Drop files onto the app, choose a destination folder, review the result, and run the operation.

## Example

If the destination folder is named `Travel Photos`:

```text
Travel Photos_001.jpg
Travel Photos_002.jpg
Travel Photos_003.png
```

## Features

- Add files by drag and drop
- Choose the destination from a folder tree
- Copy or move files while renaming them
- Rename files in place when the source and destination folders are the same
- Preview original names, new names, destinations, and operation status
- Highlight in-place rename rows in orange
- Skip occupied sequence numbers automatically
- Remove selected items from the list
- Remember the last destination and operation mode
- Remember window position, size, and proportional detail-column layout
- Reset the window to a visible position if a saved monitor is no longer available
- Show a result or error for each file

There is intentionally no file picker. Files are always supplied by drag and drop.

## Download

Download `DropRenamer.exe` from the latest GitHub Release.

The Windows x64 executable is self-contained and includes the .NET 10 runtime. Users do not need to install .NET separately.

Windows SmartScreen may show a warning because the executable is not code-signed.

## System requirements

- Windows 10 or Windows 11
- 64-bit Windows (x64)

## Usage

1. Run `DropRenamer.exe`.
2. Drag files onto the window.
3. Choose the destination folder.
4. Select Copy or Move.
5. Review the proposed names and destinations.
6. Run the operation.

Test with copies of important files before processing a large batch.

## Build from source

Requirements:

- .NET 10 SDK
- Windows, or a build environment that can target WPF

```powershell
dotnet build DropRenamer.sln
```

Run from source:

```powershell
dotnet run --project .\src\DropRenamer\DropRenamer.csproj
```

Create the self-contained Windows x64 executable:

```powershell
dotnet publish .\src\DropRenamer\DropRenamer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:Version=1.0.0
```

## Technology

- C#
- WPF
- .NET 10
