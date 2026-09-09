# Publication Guide

## Current State

`dotnet-drmanhattan` is structured as a standalone NuGet package.

Current coordinates:

- package: `dotnet-drmanhattan`
- version: `0.1.2`
- repository: `github.com/animalab-netizen/dotnet-drmanhattan`

## Distribution Model

The package is intended for:

- direct NuGet distribution as the public .NET DrManhattan runtime
- consumption by validation projects and backend examples
- installation without any private feed requirement

## Installation

```bash
dotnet add package dotnet-drmanhattan
```

## Release Checklist

1. Run `dotnet build`
2. Run `dotnet run --project tests/DotNetDrManhattan.Validation/DotNetDrManhattan.Validation.csproj`
3. Update `CHANGELOG.md`
4. Confirm version in `src/DotNetDrManhattan/DotNetDrManhattan.csproj`
5. Commit release metadata
6. Create and push tag `v0.1.2`
