using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Tests
{
    // Keep synthetic input independent of which Editor window currently has focus.
    internal sealed class DesktopInputTestScope : IDisposable
    {
        private readonly InputSettings original;
        private readonly InputSettings settings;
        private readonly HideFlags originalFlags;
        private readonly bool runInBackground;
        public DesktopInputTestScope()
        {
            runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            original = InputSystem.settings;
            originalFlags = original.hideFlags;
            settings = UnityEngine.Object.Instantiate(original);
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // InputManager destroys HideAndDontSave settings when replacing them.
            // Retain the original temporary object so it can be restored after this test.
            if (originalFlags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
            InputSystem.settings = settings;
        }
        public void Dispose()
        {
            Application.runInBackground = runInBackground;
            InputSystem.settings = original;
            original.hideFlags = originalFlags;
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        }
    }
}
