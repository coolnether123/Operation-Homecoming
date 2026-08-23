using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OperationHomecoming.Core;
using static RimWorld.ModTestSupport.Test;

namespace OperationHomecoming.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Start("Operation Homecoming lifecycle contracts");
            Run("kidnap to rescue quest transition", TestRescueTransition);
            Run("terminal outcomes cannot be repeated", TestTerminalTransitions);
            Run("production Def names are present", TestProductionDefs);
            Run("shipping source has no debug actions", TestShippingDebugBoundary);
            return Finish();
        }

        private static void TestRescueTransition()
        {
            Equal(
                true,
                HomecomingLifecycleContract.TryAdvance(
                    HomecomingLifecycleStage.Kidnapped,
                    HomecomingLifecycleStage.Contacted));
            Equal(
                true,
                HomecomingLifecycleContract.TryAdvance(
                    HomecomingLifecycleStage.Contacted,
                    HomecomingLifecycleStage.RescueQuestActive));
            Equal(
                true,
                HomecomingLifecycleContract.TryAdvance(
                    HomecomingLifecycleStage.RescueQuestActive,
                    HomecomingLifecycleStage.Rescued));
            Equal(
                false,
                HomecomingLifecycleContract.TryAdvance(
                    HomecomingLifecycleStage.Rescued,
                    HomecomingLifecycleStage.RescueQuestActive));
        }

        private static void TestTerminalTransitions()
        {
            HomecomingLifecycleStage[] terminalStages =
            {
                HomecomingLifecycleStage.Rescued,
                HomecomingLifecycleStage.FailedReturnRaid,
                HomecomingLifecycleStage.FailedWoundedReturn,
                HomecomingLifecycleStage.Expired
            };

            foreach (HomecomingLifecycleStage stage in terminalStages)
            {
                True(HomecomingLifecycleContract.IsTerminal(stage), stage + " must be terminal");
                False(
                    HomecomingLifecycleContract.TryAdvance(stage, HomecomingLifecycleStage.Rescued),
                    stage + " must not transition again");
            }
        }

        private static void TestProductionDefs()
        {
            string root = FindRepositoryRoot();
            string[] expected =
            {
                "OH_RescueCaptive",
                "OH_PrisonBreak",
                "OH_KidnapCamp",
                "OH_Investigation",
                "OH_ReturnRaid",
                "OH_RescueSuccess"
            };
            var documents = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
                .Select(XDocument.Load)
                .ToArray();
            foreach (string defName in expected)
            {
                True(
                    documents.Any(d => d.Descendants().Any(e =>
                        (string)e.Element("defName") == defName)),
                    defName + " is missing from production Defs");
            }
        }

        private static void TestShippingDebugBoundary()
        {
            string root = FindRepositoryRoot();
            string[] sourceFiles = Directory.GetFiles(Path.Combine(root, "Core"), "*.cs");
            foreach (string sourceFile in sourceFiles)
            {
                NotContains("DebugAction", File.ReadAllText(sourceFile), sourceFile);
            }
        }

        private static string FindRepositoryRoot()
        {
            string directory = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(Path.Combine(directory, "Operation Homecoming.csproj")))
                {
                    return directory;
                }
                directory = Directory.GetParent(directory)?.FullName;
            }

            string environmentRoot = Environment.GetEnvironmentVariable("OPERATION_HOMECOMING_ROOT");
            if (!string.IsNullOrEmpty(environmentRoot))
            {
                return environmentRoot;
            }

            throw new DirectoryNotFoundException("Operation Homecoming repository root was not found.");
        }
    }
}
