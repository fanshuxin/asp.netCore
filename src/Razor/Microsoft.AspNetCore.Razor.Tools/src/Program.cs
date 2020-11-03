// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            DebugMode.HandleDebugSwitch(ref args);

            var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => { cancel.Cancel(); };

            var outputWriter = new StringWriter();
            var errorWriter = new StringWriter();

            var application = new Application(
                cancel.Token,
                outputWriter,
                errorWriter);

            var result = application.Execute(args);

            var output = outputWriter.ToString();
            var error = errorWriter.ToString();

            outputWriter.Dispose();
            errorWriter.Dispose();

            Console.Write(output);
            Console.Error.Write(error);

            return result;
        }
    }
}
