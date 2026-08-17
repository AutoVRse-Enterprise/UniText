#if UNITEXT
#if UNITY_EDITOR
using System.Collections.Generic;
using LightSide;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace VRseBuilder.Editor.Utils
{
    public class UniTextReplacer : EditorWindow
    {
        public UniTextFontStack targetFontStack;

        private class TMPItem
        {
            public TextMeshProUGUI tmpro;
            public bool isSelected = true;
        }

        private List<TMPItem> foundTMPs = new List<TMPItem>();
        private Vector2 scrollPosition;

        [MenuItem("VRseBuilder/Others/Tools/Replace TextMeshPro with UniText", priority = 502)]
        public static void ShowWindow()
        {
            GetWindow<UniTextReplacer>("TMPro to UniText");
        }

        private void OnGUI()
        {
            GUILayout.Label("Replace TextMeshProUGUI with UniText", EditorStyles.boldLabel);
            GUILayout.Space(10);

            targetFontStack = (UniTextFontStack)EditorGUILayout.ObjectField("Default Font Stack", targetFontStack, typeof(UniTextFontStack), false);
            GUILayout.Label("Optional: All replaced components will use this Font Stack if provided.", EditorStyles.miniLabel);

            GUILayout.Space(15);
            GUILayout.Label("Scan the current scene for TextMeshProUGUI components.", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Scan Scene", GUILayout.Height(30)))
            {
                ScanScene();
            }

            if (foundTMPs.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label($"Found {foundTMPs.Count} TextMeshProUGUI components:", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                {
                    foreach (var item in foundTMPs) item.isSelected = true;
                }
                if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                {
                    foreach (var item in foundTMPs) item.isSelected = false;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                scrollPosition = GUILayout.BeginScrollView(scrollPosition, "box", GUILayout.ExpandHeight(true));
                for (int i = foundTMPs.Count - 1; i >= 0; i--)
                {
                    var item = foundTMPs[i];
                    if (item.tmpro == null)
                    {
                        foundTMPs.RemoveAt(i);
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    item.isSelected = GUILayout.Toggle(item.isSelected, GUIContent.none, GUILayout.Width(20));
                    
                    // Show pingable object field that is read-only
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(item.tmpro, typeof(TextMeshProUGUI), true);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

                GUILayout.Space(10);
                if (GUILayout.Button("Replace Checked with UniText", GUILayout.Height(30)))
                {
                    ReplaceChecked();
                }
            }
        }

        [MenuItem("CONTEXT/TextMeshProUGUI/Replace with UniText")]
        public static void ReplaceFromContext(MenuCommand command)
        {
            TextMeshProUGUI tmpro = (TextMeshProUGUI)command.context;
            ReplaceComponent(tmpro, null);
        }

        private void ScanScene()
        {
            foundTMPs.Clear();
            TextMeshProUGUI[] tmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (var tmp in tmps)
            {
                // Ensure it's a scene object, not a prefab or hidden editor object
                if (tmp != null && tmp.gameObject != null && tmp.gameObject.scene.IsValid())
                {
                    if ((tmp.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0)
                    {
                        foundTMPs.Add(new TMPItem { tmpro = tmp, isSelected = true });
                    }
                }
            }

            if (foundTMPs.Count == 0)
            {
                EditorUtility.DisplayDialog("Notice", "No TextMeshProUGUI components found in the active scene(s).", "OK");
            }
        }

        private void ReplaceChecked()
        {
            int replacedCount = 0;
            List<TMPItem> toReplace = new List<TMPItem>();

            foreach (var item in foundTMPs)
            {
                if (item.isSelected && item.tmpro != null)
                {
                    toReplace.Add(item);
                }
            }

            if (toReplace.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please select at least one component to replace.", "OK");
                return;
            }

            foreach (var item in toReplace)
            {
                if (ReplaceComponent(item.tmpro, targetFontStack))
                {
                    replacedCount++;
                }
            }

            EditorUtility.DisplayDialog("Success", $"Successfully replaced {replacedCount} TextMeshProUGUI component(s) with UniText.", "OK");
            ScanScene(); // Rescan after replacement
        }

        private static bool ReplaceComponent(TextMeshProUGUI tmpro, UniTextFontStack fontStack)
        {
            if (tmpro == null)
                return false;

            GameObject targetGo = tmpro.gameObject;

            // Save properties before destroying the component
            string text = tmpro.text;
            float fontSize = tmpro.fontSize;
            Color color = tmpro.color;
            bool enableWordWrap = tmpro.enableWordWrapping;
            bool enableAutoSizing = tmpro.enableAutoSizing;
            float fontSizeMin = tmpro.fontSizeMin;
            float fontSizeMax = tmpro.fontSizeMax;
            TextAlignmentOptions tmproAlignment = tmpro.alignment;
            bool raycastTarget = tmpro.raycastTarget;

            // Register an undo operation for the GameObject
            Undo.RegisterCompleteObjectUndo(targetGo, "Replace TMPro with UniText");

            // Remove TMPro
            Undo.DestroyObjectImmediate(tmpro);

            // Add UniText
            UniText uniText = Undo.AddComponent<UniText>(targetGo);

            // Apply properties
            uniText.Text = text;
            uniText.FontSize = fontSize;
            uniText.color = color;
            uniText.WordWrap = enableWordWrap;
            uniText.AutoSize = enableAutoSizing;
            uniText.raycastTarget = raycastTarget;

            if (enableAutoSizing)
            {
                uniText.MinFontSize = fontSizeMin;
                uniText.MaxFontSize = fontSizeMax;
            }

            if (fontStack != null)
            {
                uniText.FontStack = fontStack;
            }

            // Map alignment
            MapAlignment(tmproAlignment, out HorizontalAlignment hAlign, out VerticalAlignment vAlign);
            uniText.HorizontalAlignment = hAlign;
            uniText.VerticalAlignment = vAlign;

            EditorUtility.SetDirty(targetGo);

            return true;
        }

        private static void MapAlignment(TextAlignmentOptions tmproAlign, out HorizontalAlignment hAlign, out VerticalAlignment vAlign)
        {
            // Set defaults
            hAlign = HorizontalAlignment.Left;
            vAlign = VerticalAlignment.Top;

            switch (tmproAlign)
            {
                case TextAlignmentOptions.TopLeft:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Top;
                    break;
                case TextAlignmentOptions.Top:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Top;
                    break;
                case TextAlignmentOptions.TopRight:
                    hAlign = HorizontalAlignment.Right;
                    vAlign = VerticalAlignment.Top;
                    break;
                case TextAlignmentOptions.TopJustified:
                case TextAlignmentOptions.TopFlush:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Top;
                    break;
                case TextAlignmentOptions.TopGeoAligned:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Top;
                    break;

                case TextAlignmentOptions.Left:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Middle;
                    break;
                case TextAlignmentOptions.Center:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Middle;
                    break;
                case TextAlignmentOptions.Right:
                    hAlign = HorizontalAlignment.Right;
                    vAlign = VerticalAlignment.Middle;
                    break;
                case TextAlignmentOptions.Justified:
                case TextAlignmentOptions.Flush:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Middle;
                    break;
                case TextAlignmentOptions.CenterGeoAligned:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Middle;
                    break;

                case TextAlignmentOptions.BottomLeft:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Bottom;
                    break;
                case TextAlignmentOptions.Bottom:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Bottom;
                    break;
                case TextAlignmentOptions.BottomRight:
                    hAlign = HorizontalAlignment.Right;
                    vAlign = VerticalAlignment.Bottom;
                    break;
                case TextAlignmentOptions.BottomJustified:
                case TextAlignmentOptions.BottomFlush:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Bottom;
                    break;
                case TextAlignmentOptions.BottomGeoAligned:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Bottom;
                    break;

                // Baseline and Midline align generally act like Middle/Bottom in standard layouts.
                case TextAlignmentOptions.BaselineLeft:
                case TextAlignmentOptions.MidlineLeft:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Middle;
                    break;

                case TextAlignmentOptions.Baseline:
                case TextAlignmentOptions.Midline:
                case TextAlignmentOptions.BaselineGeoAligned:
                case TextAlignmentOptions.MidlineGeoAligned:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Middle;
                    break;

                case TextAlignmentOptions.BaselineRight:
                case TextAlignmentOptions.MidlineRight:
                    hAlign = HorizontalAlignment.Right;
                    vAlign = VerticalAlignment.Middle;
                    break;

                case TextAlignmentOptions.BaselineJustified:
                case TextAlignmentOptions.BaselineFlush:
                case TextAlignmentOptions.MidlineJustified:
                case TextAlignmentOptions.MidlineFlush:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Middle;
                    break;

                // Capline mapping
                case TextAlignmentOptions.CaplineLeft:
                case TextAlignmentOptions.CaplineJustified:
                case TextAlignmentOptions.CaplineFlush:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Top;
                    break;

                case TextAlignmentOptions.Capline:
                case TextAlignmentOptions.CaplineGeoAligned:
                    hAlign = HorizontalAlignment.Center;
                    vAlign = VerticalAlignment.Top;
                    break;

                case TextAlignmentOptions.CaplineRight:
                    hAlign = HorizontalAlignment.Right;
                    vAlign = VerticalAlignment.Top;
                    break;

                case TextAlignmentOptions.Converted:
                    hAlign = HorizontalAlignment.Left;
                    vAlign = VerticalAlignment.Top;
                    break;
            }
        }
    }
}
#endif
#endif

