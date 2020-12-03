// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Logging;
using Xunit;
using Microsoft.AspNetCore.Server.IntegrationTesting.IIS;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Server.IIS.FunctionalTests
{
    [Collection(PublishedSitesCollection.Name)]
    public class ShadowCopyTests : IISFunctionalTestBase
    {
        public ShadowCopyTests(PublishedSitesFixture fixture) : base(fixture)
        {
        }

        // TODO check if app init module works here
        // TODO verify app init on azure app services.
        // TODO check out of proc?
        // TODO check startup failure
        // TODO check shutting down?

        [ConditionalFact]
        public async Task ShadowCopyWorks()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopy"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = directory.FullName;

            var deploymentResult = await DeployAsync(deploymentParameters);
            await deploymentResult.HttpClient.GetStringAsync("Wow!");

            var directoryInfo = new DirectoryInfo(deploymentResult.ContentRoot);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }

            foreach (var dirInfo in directoryInfo.GetDirectories())
            {
                dirInfo.Delete(recursive: true);
            }
        }

        [ConditionalFact]
        public async Task ShadowCopyRelativeInSameDirectoryFails()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopy"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = "test/";
            deploymentParameters.ApplicationPath = directory.FullName;

            var deploymentResult = await DeployAsync(deploymentParameters);
            var response = await deploymentResult.HttpClient.GetAsync("Wow!");
            Assert.False(response.IsSuccessStatusCode);

            // Check if directory can be deleted.
            // Can't delete the folder but can delete all content in it.

            var directoryInfo = new DirectoryInfo(deploymentResult.ContentRoot);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }

            foreach (var dirInfo in directoryInfo.GetDirectories())
            {
                dirInfo.Delete(recursive: true);
            }
        }

        [ConditionalFact]
        public async Task ShadowCopyRelativeOutsideDirectoryWorks()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopy"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = $"..\\{directory.Name}";
            deploymentParameters.ApplicationPath = directory.FullName;

            var deploymentResult = await DeployAsync(deploymentParameters);
            await deploymentResult.HttpClient.GetStringAsync("Wow!");

            // Check if directory can be deleted.
            // Can't delete the folder but can delete all content in it.

            var directoryInfo = new DirectoryInfo(deploymentResult.ContentRoot);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }

            foreach (var dirInfo in directoryInfo.GetDirectories())
            {
                dirInfo.Delete(recursive: true);
            }
        }

        [ConditionalFact]
        [MaximumOSVersion(OperatingSystems.Windows, WindowsVersions.Win10_20H1, SkipReason = "Shutdown hangs https://github.com/dotnet/aspnetcore/issues/25107")]
        public async Task ShadowCopySingleFileChangedWorks()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopy"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = directory.FullName;

            var deploymentResult = await DeployAsync(deploymentParameters);

            DirectoryCopy(deploymentResult.ContentRoot, directory.FullName, copySubDirs: true);

            await deploymentResult.HttpClient.GetStringAsync("Wow!");

            // Rewrite file
            var dirInfo = new DirectoryInfo(deploymentResult.ContentRoot);
            string dllPath = "";
            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (file.Extension == ".dll")
                {
                    dllPath = file.FullName;
                    break;
                }
            }
            var fileContents = File.ReadAllBytes(dllPath);
            File.WriteAllBytes(dllPath, fileContents);

            deploymentResult.AssertWorkerProcessStop();
            EventLogHelpers.VerifyEventLogEvent(deploymentResult, EventLogHelpers.ShutdownFileChange(deploymentResult), Logger);
        }

        [ConditionalFact]
        [MaximumOSVersion(OperatingSystems.Windows, WindowsVersions.Win10_20H1, SkipReason = "Shutdown hangs https://github.com/dotnet/aspnetcore/issues/25107")]
        public async Task ShadowCopyE2EWorks()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopy"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = directory.FullName;
            var deploymentResult = await DeployAsync(deploymentParameters);

            DirectoryCopy(deploymentResult.ContentRoot, directory.FullName, copySubDirs: true);

            await deploymentResult.HttpClient.GetStringAsync("Wow!");

            var secondTempDir = CreateTempDirectory();

            // copy back and forth to cause file change notifications.
            DirectoryCopy(deploymentResult.ContentRoot, secondTempDir.FullName, copySubDirs: true);
            DirectoryCopy(secondTempDir.FullName, deploymentResult.ContentRoot, copySubDirs: true);

            deploymentResult.AssertWorkerProcessStop();

            EventLogHelpers.VerifyEventLogEvent(deploymentResult, EventLogHelpers.ShutdownFileChange(deploymentResult), Logger);
        }

        protected static DirectoryInfo CreateTempDirectory()
        {
            var tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
            var target = new DirectoryInfo(tempPath);
            target.Create();
            return target;
        }

        // copied from https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
    }
}
