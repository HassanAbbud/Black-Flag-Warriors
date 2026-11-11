using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace InstantDestruction
{
    /// <summary>
    /// Custom inspector editor class for BaseDestruction components.
    /// Provides layout configuration and hooks to launch the dedicated simulation window.
    /// </summary>
    [CustomEditor(typeof(BaseDestruction), true)]
    public class BaseDestructionEditor : Editor
    {
        /// <summary>
        /// Creates and returns the custom VisualElement layout for the inspector.
        /// Injects custom action button to open Settings Window and generates default inspector fields.
        /// </summary>
        /// <returns>The root VisualElement container for the inspector UI.</returns>
        public override VisualElement CreateInspectorGUI()
        {
            // Create root visual element
            var root = new VisualElement();

            // Container for spacing and styling the button area
            var buttonContainer = new VisualElement
            {
                style =
                {
                    marginTop = 15,
                    marginBottom = 5,
                    paddingTop = 10,
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.25f, 0.25f, 0.25f, 1f)
                }
            };

            // Button to open the custom settings window
            var openWindowButton = new Button(() =>
            {
                DestructionSettingsWindow.ShowWindow((BaseDestruction)target);
            })
            {
                text = "Open Settings Window"
            };
            
            // Set button styling
            openWindowButton.style.height = 32;
            openWindowButton.style.backgroundColor = new Color(0.12f, 0.5f, 0.72f, 1f);
            openWindowButton.style.color = Color.white;
            openWindowButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            openWindowButton.style.marginBottom = 6;

            // Button to open online documentation
            var openDocButton = new Button(() =>
            {
                Application.OpenURL("https://instantdestruction.rebush-studio.workers.dev/");
            })
            {
                text = "Open Online Documentation"
            };

            // Set doc button styling (Green color to distinguish from settings button)
            openDocButton.style.height = 32;
            openDocButton.style.backgroundColor = new Color(0.2f, 0.55f, 0.35f, 1f);
            openDocButton.style.color = Color.white;
            openDocButton.style.unityFontStyleAndWeight = FontStyle.Bold;

            buttonContainer.Add(openWindowButton);
            buttonContainer.Add(openDocButton);
            root.Add(buttonContainer);

            // Automatically generate and add default inspector properties
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            return root;
        }
    }
}
