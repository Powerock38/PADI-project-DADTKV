# DADTKV project

## Prerequisites

This project uses .NET 7.0.  
The `dotnet` command must be in your $PATH environment variable.

## Running the program

### TL;DR: at the root of this repository:
```bash
dotnet build
cd Management/bin/Debug/net7.0/
./Management
```

### More details:

The main project is `Management.csproj`.  
It reads the configuration and launches the other projects as separate processes.

This program can be run using the command line : first build the solution using `dotnet build` at the root of the repository,
then run the `Management` project executable (may be .exe on Windows) in `Management/bin/Debug/net7.0/`.

At this time it works only if your working directory is `Management/bin/Debug/net7.0/` when launching the Management console.

## Disclaimer

This project is developed using JetBrains Rider, not Visual Studio.

## Usage

All proccesses are killed when entering "q" or "quit" or "exit" or "stop" in the Management console.
