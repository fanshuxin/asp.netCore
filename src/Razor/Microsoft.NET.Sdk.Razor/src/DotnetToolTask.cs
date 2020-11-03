// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public abstract class DotNetToolTask : ToolTask
    {
        private string _dotnetPath;

        public bool Debug { get; set; }

        public bool DebugTool { get; set; }

        [Required]
        public string ToolAssembly { get; set; }

        public bool UseServer { get; set; }

        // Specifies whether we should fallback to in-process execution if server execution fails.
        public bool ForceServer { get; set; }

        // Specifies whether server execution is allowed when PipeOptions.CurrentUserOnly is not available.
        // For testing purposes only.
        public bool SuppressCurrentUserOnlyPipeOptions { get; set; }

        public string PipeName { get; set; }

        protected override string ToolName => Path.GetDirectoryName(DotNetPath);

        // If we're debugging then make all of the stdout gets logged in MSBuild
        protected override MessageImportance StandardOutputLoggingImportance => DebugTool ? MessageImportance.High : base.StandardOutputLoggingImportance;

        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        internal abstract string Command { get; }

        protected override string GenerateFullPathToTool() => DotNetPath;

        private string DotNetPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_dotnetPath))
                {
                    return _dotnetPath;
                }

                _dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrEmpty(_dotnetPath))
                {
                    throw new InvalidOperationException("DOTNET_HOST_PATH is not set");
                }

                return _dotnetPath;
            }
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"exec \"{ToolAssembly}\"" + (DebugTool ? " --debug" : "");
        }

        protected override string GetResponseFileSwitch(string responseFilePath)
        {
            return "@\"" + responseFilePath + "\"";
        }

        protected abstract override string GenerateResponseFileCommands();

        public override bool Execute()
        {
            if (Debug)
            {
                Log.LogMessage(MessageImportance.High, "Waiting for debugger in pid: {0}", Process.GetCurrentProcess().Id);
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            }

            return base.Execute();
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
        }

        protected override void LogToolCommand(string message)
        {
            if (Debug)
            {
                Log.LogMessage(MessageImportance.High, message);
            }
            else
            {
                base.LogToolCommand(message);
            }
        }

        protected override bool HandleTaskExecutionErrors()
        {
            if (!HasLoggedErrors)
            {
                var toolCommand = Path.GetFileNameWithoutExtension(ToolAssembly) + " " + Command;
                // Show a slightly better error than the standard ToolTask message that says "dotnet" failed.
                Log.LogError($"{toolCommand} exited with code {ExitCode}.");
                return false;
            }

            return base.HandleTaskExecutionErrors();
        }
    }
}
