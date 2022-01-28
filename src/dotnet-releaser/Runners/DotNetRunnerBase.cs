﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;

namespace DotNetReleaser.Runners;

public record DotNetResult(CommandResult CommandResult, string CommandLine, string Output)
{
    public bool HasErrors => CommandResult.ExitCode != 0;
}

[DebuggerDisplay("{" + nameof(ToDebuggerDisplay) + "(),nq}")]
public abstract class DotNetRunnerBase : IDisposable
{
    protected DotNetRunnerBase(string command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Arguments = new List<string>();
        Properties = new Dictionary<string, object>();
        WorkingDirectory = Environment.CurrentDirectory;
    }

    public string Command { get; }

    public List<string> Arguments { get; }

    public Dictionary<string, object> Properties { get; }

    public string WorkingDirectory { get; set; }

    protected virtual IEnumerable<string> ComputeArguments() => Arguments;

    protected virtual IReadOnlyDictionary<string, object> ComputeProperties() => Properties;

    protected async Task<DotNetResult> RunImpl()
    {
        return await Run(Command, ComputeArguments(), ComputeProperties(), WorkingDirectory);
    }

    private string ToDebuggerDisplay()
    {
        return $"dotnet {GetFullArguments(Command, ComputeArguments(), ComputeProperties())}";
    }

    private static string GetFullArguments(string command, IEnumerable<string> arguments, IReadOnlyDictionary<string, object>? properties)
    {
        var argsBuilder = new StringBuilder($"{command}");

        // Pass all our user properties to msbuild
        if (properties != null)
        {
            foreach (var property in properties)
            {
                argsBuilder.Append($" -p:{property.Key}={EscapePath(GetPropertyValueAsString(property.Value))}");
            }
        }

        // Add all arguments
        foreach (var arg in arguments)
        {
            argsBuilder.Append($" {arg}");
        }

        return argsBuilder.ToString();
    }

    private static string GetPropertyValueAsString(object value)
    {
        if (value is bool b) return b ? "true" : "false";
        if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }

    private static async Task<DotNetResult> Run(string command, IEnumerable<string> args, IReadOnlyDictionary<string, object>? properties = null, string? workingDirectory = null)
    {
        var stdOutAndErrorBuffer = new StringBuilder();

        var arguments = GetFullArguments(command, args, properties);
        var wrap = Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory ?? Environment.CurrentDirectory)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutAndErrorBuffer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdOutAndErrorBuffer))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        var result = await wrap;

        return new DotNetResult(result, $"dotnet {arguments}",stdOutAndErrorBuffer.ToString());
    }

    private static readonly Regex MatchWhitespace = new Regex(@"[\s\:]");

    public static string EscapePath(string path)
    {
        path = path.Replace("\"", "\\\"");
        return MatchWhitespace.IsMatch(path) ? $"\"{path}\"" : path;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}