// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestTemplates.AcceptanceTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Base class for Acceptance tests.
    /// </summary>
    public class AcceptanceTestBase
    {
        // this output is specific to the version of TP included with the runtime you are using to run the tests
        // if you see all tests failed, chances are that the output changed after you upgraded to latest version of dotnet
        private const string TestSummaryStatusMessageFormat = "Test Run Successful. Total tests: {0} Passed: {1} Total time:";
        private string standardTestOutput = string.Empty;
        private string standardTestError = string.Empty;
        private int runnerExitCode = -1;

        private string arguments = string.Empty;

        /// <summary>
        /// Invokes <c>dotnet</c> with specified arguments.
        /// </summary>
        /// <param name="arguments">Arguments provided to <c>dotnet</c>.exe</param>
        public void InvokeDotnetTest(string arguments)
        {
            this.Execute(arguments, out this.standardTestOutput, out this.standardTestError, out this.runnerExitCode);
            this.standardTestError = Regex.Replace(this.standardTestError, @"\s+", " ");
            this.standardTestOutput = Regex.Replace(this.standardTestOutput, @"\s+", " ");
        }

        /// <summary>
        /// Validate if the overall test count and results are matching.
        /// </summary>
        /// <param name="passedTestsCount">Passed test count</param>
        /// <param name="failedTestsCount">Failed test count</param>
        /// <param name="skippedTestsCount">Skipped test count</param>
        public void ValidateSummaryStatus(int passedTestsCount, int failedTestsCount, int skippedTestsCount)
        {
            var totalTestCount = passedTestsCount + failedTestsCount + skippedTestsCount;
            if (totalTestCount == 0)
            {
                // No test should be found/run
                var summaryStatus = string.Format(TestSummaryStatusMessageFormat, @"\d+", @"\d+", @"\d+", @"\d+");
                StringAssert.DoesNotMatch(
                    this.standardTestOutput,
                    new Regex(summaryStatus),
                    "Excepted: There should not be test summary{2}Actual: {0}{2}Standard Error: {1}{2}Arguments: {3}{2}",
                    this.standardTestOutput,
                    this.standardTestError,
                    Environment.NewLine,
                    this.arguments);
            }
            else
            {
                var summaryStatus = string.Format(TestSummaryStatusMessageFormat, totalTestCount, passedTestsCount, failedTestsCount, skippedTestsCount);

                Assert.IsTrue(
                    this.standardTestOutput.Contains(summaryStatus),
                    "The Test summary does not match.{3}Expected summary: {1}{3}Test Output: {0}{3}Standard Error: {2}{3}Arguments: {4}{3}",
                    this.standardTestOutput,
                    summaryStatus,
                    this.standardTestError,
                    Environment.NewLine,
                    this.arguments);
            }
        }

        private string GetDotnetExePath()
        {
            var currentDllPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AcceptanceTestBase)).Location);
            string[] paths = currentDllPath.Split("\\artifacts");
            if (paths.Length == 2)
            {
                var dotnetPath = Path.Combine(paths[0], ".dotnet", "dotnet.exe");
                if (File.Exists(dotnetPath))
                    return dotnetPath;
            }

            return "dotnet";
        }

        private void Execute(string args, out string stdOut, out string stdError, out int exitCode)
        {
            this.arguments = args;

            using (Process dotnet = new Process())
            {
                Console.WriteLine("AcceptanceTestBase.Execute: Starting dotnet.exe");
                dotnet.StartInfo.FileName = GetDotnetExePath();
                dotnet.StartInfo.Arguments = "test " + args;
                dotnet.StartInfo.UseShellExecute = false;
                dotnet.StartInfo.RedirectStandardError = true;
                dotnet.StartInfo.RedirectStandardOutput = true;
                dotnet.StartInfo.CreateNoWindow = true;

                var stdoutBuffer = new StringBuilder();
                var stderrBuffer = new StringBuilder();
                dotnet.OutputDataReceived += (sender, eventArgs) => stdoutBuffer.Append(eventArgs.Data).Append(Environment.NewLine);
                dotnet.ErrorDataReceived += (sender, eventArgs) => stderrBuffer.Append(eventArgs.Data).Append(Environment.NewLine);

                Console.WriteLine("AcceptanceTestBase.Execute: Path = {0}", dotnet.StartInfo.FileName);
                Console.WriteLine("AcceptanceTestBase.Execute: Arguments = {0}", dotnet.StartInfo.Arguments);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                dotnet.Start();
                dotnet.BeginOutputReadLine();
                dotnet.BeginErrorReadLine();
                if (!dotnet.WaitForExit(80 * 1000))
                {
                    Console.WriteLine("AcceptanceTestBase.Execute: Timed out waiting for dotnet.exe. Terminating the process.");
                    dotnet.Kill();
                }
                else
                {
                    // Ensure async buffers are flushed
                    dotnet.WaitForExit();
                }

                stopwatch.Stop();

                Console.WriteLine($"AcceptanceTestBase.Execute: Total execution time: {stopwatch.Elapsed.Duration()}");

                stdError = stderrBuffer.ToString();
                stdOut = stdoutBuffer.ToString();
                exitCode = dotnet.ExitCode;

                Console.WriteLine("AcceptanceTestBase.Execute: stdError = {0}", stdError);
                Console.WriteLine("AcceptanceTestBase.Execute: stdOut = {0}", stdOut);
                Console.WriteLine("AcceptanceTestBase.Execute: Stopped dotnet.exe. Exit code = {0}", exitCode);
            }
        }
    }
}
