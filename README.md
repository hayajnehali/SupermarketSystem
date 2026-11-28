# SupermarketSystem

A multi-branch supermarket platform built as a distributed Modular Monolith.

## Technology
- .NET 10 / ASP.NET Core 10
- EF Core 10
- SQL Server 2025
- Angular clients are maintained as separate applications.

## Hosts
- `Central.Api` / `Central.Worker`: central authority and orchestration.
- `Branch.Api` / `Branch.Worker`: branch-local operations and synchronization.

## Architecture
Clean Architecture with separate Central and Branch Domain, Application, Infrastructure and host projects. Central and Branch databases remain independent; cross-site workflows use versioned contracts and durable synchronization.

## Build
Open `SupermarketSystem.sln` in Visual Studio 2026 with the .NET 10 SDK.

Database contexts, entity configurations and migrations are intentionally introduced only alongside their completed physical schema specifications.
