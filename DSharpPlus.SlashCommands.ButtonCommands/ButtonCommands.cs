using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using Emzi0767.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DSharpPlus.SlashCommands.ButtonCommands
{
    /// <summary>
    /// Extension method to Enable it
    /// </summary>
    public static class ButtonCommandsExtensionMethods
    {
        public static ButtonCommands EnableButtonCommands(this SlashCommandsExtension slash)
        {
            return new ButtonCommands(slash);
        }

        public static IReadOnlyDictionary<int, ButtonCommands> EnableShardedButtonCommands(this IReadOnlyDictionary<int, SlashCommandsExtension> slashes)
        {
            var modules = new Dictionary<int, ButtonCommands>();
            foreach (var item in slashes)
            {
                var module = item.Value.EnableButtonCommands();
                modules.Add(item.Key, module);
            }
            return modules;
        }
    }

    /// <summary>
    /// Enabled the CustomId's of ButtonComponents to be mapped to (ungrouped and parameterless) SlashCommands
    /// </summary>
    public sealed class ButtonCommands
    {
        //The regular D#+ extension
        private readonly SlashCommandsExtension _slash;
        //Paramter check of the regular extension
        private MethodInfo _resolveInteractionCommandParameters { get; set; }
        //Method to execute the commands from the regular extension
        private MethodInfo _runCommand { get; set; }

        //List of SlashCommands and their methods
        //Fricc this field >:(
        private FieldInfo _commandMethods { get; set; }

        //Error event from the regular extension
        private AsyncEvent<SlashCommandsExtension, SlashCommandErrorEventArgs> _slashError { get; set; }
        //Executed event from the regular extension
        private AsyncEvent<SlashCommandsExtension, SlashCommandExecutedEventArgs> _slashExecuted { get; set; }
        //COnfig fromt he regular extension (just needed for DI)
        private SlashCommandsConfiguration _slashConfiguration { get; set; }

        //Main setup for everything
        internal ButtonCommands(SlashCommandsExtension slash)
        {
            this._slash = slash;

            //Gets some reusable stuff from the regular extension for use in the Handler
            var _slashType = _slash.GetType();
            _runCommand = _slashType.GetMethod("RunCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            _resolveInteractionCommandParameters = _slashType.GetMethod("ResolveInteractionCommandParameters", BindingFlags.NonPublic | BindingFlags.Instance);
            _commandMethods = _slashType.GetField("<_commandMethods>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            var rawError = _slashType.GetField("_slashError", BindingFlags.NonPublic | BindingFlags.Instance);
            _slashError = rawError.GetValue(slash) as AsyncEvent<SlashCommandsExtension, SlashCommandErrorEventArgs>;
            var rawExecuted = _slashType.GetField("_slashExecuted", BindingFlags.NonPublic | BindingFlags.Instance);
            _slashExecuted = rawExecuted.GetValue(slash) as AsyncEvent<SlashCommandsExtension, SlashCommandExecutedEventArgs>;
            var rawConfig = _slashType.GetField("<__configuration>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            _slashConfiguration = rawExecuted.GetValue(slash) as SlashCommandsConfiguration;

            //Register the Component Handler
            slash.Client.ComponentInteractionCreated += HandleComponentInteraction;
        }

        /// <summary>
        /// Main handlter to accept the button interactions
        /// </summary>
        private Task HandleComponentInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                //Since the CommandMethods class is 'internal' I move them into a seperate list of objects to make better use of them
                var commandMethodsRaw = _commandMethods.GetValue(_slash);
                var commandMethods = new List<object>();

                foreach (var item in commandMethodsRaw as IEnumerable)
                    commandMethods.Add(item);

                //Check if Compontent (may need to specify a bit more so really only buttons work)
                if (e.Interaction.Type == InteractionType.Component)
                {
                    //Creates the context
                    var context = new InteractionContext();
                    var contextFields = context.GetType().BaseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                    contextFields.First(x => x.Name == "<Interaction>k__BackingField").SetValue(context, e.Interaction);
                    contextFields.First(x => x.Name == "<Channel>k__BackingField").SetValue(context, e.Interaction.Channel);
                    contextFields.First(x => x.Name == "<Guild>k__BackingField").SetValue(context, e.Interaction.Guild);
                    contextFields.First(x => x.Name == "<User>k__BackingField").SetValue(context, e.Interaction.User);
                    contextFields.First(x => x.Name == "<Client>k__BackingField").SetValue(context, sender);
                    contextFields.First(x => x.Name == "<SlashCommandsExtension>k__BackingField").SetValue(context, _slash);
                    contextFields.First(x => x.Name == "<CommandName>k__BackingField").SetValue(context, e.Interaction.Data.CustomId);
                    contextFields.First(x => x.Name == "<InteractionId>k__BackingField").SetValue(context, e.Interaction.Id);
                    contextFields.First(x => x.Name == "<Token>k__BackingField").SetValue(context, e.Interaction.Token);
                    if (this._slashConfiguration != null)
                    {
                        var serviceField = this._slashConfiguration.GetType().GetField("<IServiceProvider>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                        var serviceValue = serviceField.GetValue(this._slashConfiguration);
                        contextFields.First(x => x.Name == "<Services>k__BackingField").SetValue(context, serviceValue);
                    }
                    contextFields.First(x => x.Name == "<Type>k__BackingField").SetValue(context, ApplicationCommandType.SlashCommand);

                    try
                    {
                        //Gets the method for the command
                        //TODO: Maybe tehre is an easier way for this?
                        var methods = commandMethods.Where(x =>
                        {
                            var nameField = x.GetType().GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                            var nameValue = nameField.GetValue(x);
                            return nameValue.ToString() == e.Interaction.Data.CustomId;
                        });

                        //Might leave this in just in case, though could maybe spam if people use broken buttons
                        if (!methods.Any())
                            throw new InvalidOperationException("A slash command was executed, but no command was registered for it.");

                        //Basically the same as the regular extension, just done via Reflection
                        if (methods.Any())
                        {
                            var t = methods.First().GetType().GetField("<Method>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                            var method = t.GetValue(methods.First()) as MethodInfo;

                            var args = await ((Task<List<object>>)_resolveInteractionCommandParameters.Invoke(_slash, new object[] { e, context, method, e.Interaction.Data.Options }));
                            await ((Task)_runCommand.Invoke(_slash, new object[] { context, method, args }));
                        }

                        var exArgs = new SlashCommandExecutedEventArgs();
                        exArgs.GetType().GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(exArgs, context);

                        await this._slashExecuted.InvokeAsync(_slash, exArgs);
                    }
                    catch (Exception ex)
                    {
                        var errArgs = new SlashCommandErrorEventArgs();
                        errArgs.GetType().GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(errArgs, context);
                        errArgs.GetType().GetField("<Exception>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(errArgs, ex);
                        await this._slashError.InvokeAsync(_slash, errArgs);
                    }
                }
            });
            return Task.CompletedTask;
        }
    }
}
