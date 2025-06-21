// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using System.Diagnostics;
using UnityEditor;

namespace Nova.Editor
{
    internal static class ToolbarMenu
    {
        [MenuItem("Tools/Nova/FAQ")]
        private static void ShowHelpDialog()
        { 
            NovaHelpWindow.ShowHelpDialog();
        }

        [MenuItem("Tools/Nova/Manual")]
        private static void OpenManual()
        {
            Process.Start("https://novaui.io/manual/");
        }

        [MenuItem("Tools/Nova/API Reference")]
        private static void OpenAPI()
        {
            Process.Start("https://novaui.io/api/");
        }

        [MenuItem("Tools/Nova/Samples")]
        private static void OpenSamples()
        {
            Process.Start("https://novaui.io/samples/");
        }

        [MenuItem("Tools/Nova/Feedback and Support")]
        private static void OpenSupport()
        {
            Process.Start("https://github.com/NovaUI-Unity/Feedback/discussions");
        }
    }
}

