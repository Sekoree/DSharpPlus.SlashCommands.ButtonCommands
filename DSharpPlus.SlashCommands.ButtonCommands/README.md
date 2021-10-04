# ButtonCommands

This maps ButtonComponents to SlashCommands via their CustomId (need to be groupless and parameterless)

## Usage

#### Single DiscordClient
```csharp
using DSharpPlus.SlashCommands.ButtonCommands;

SlashCommandsExtension slash = client.UseSlashCommands();
ButtonCommands buttonCmd = slash.EnableButtonCommands();
```

#### DiscordShardedClient
```csharp
using DSharpPlus.SlashCommands.ButtonCommands;

IReadOnlyDictionary<int, SlashCommandsExtension> slashes = await shardedClient.UseSlashCommandsAsync();
IReadOnlyDictionary<int, ButtonCommands> buttonCmds = slashes.EnableShardedButtonCommands();
```