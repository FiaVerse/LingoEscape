// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal class NovaHelpWindow : EditorWindow
    {
        private const string WelcomeMessage = "Thank you for choosing Nova!\n\nIf you're new to Nova," +
                                              " we recommend diving in to the scenes under Nova\\Sample\\UIControls " +
                                              "to get started.\n\nIf you're looking for even more complete example projects, " +
                                              "such as a settings menu, inventory system, XR handtracking, etc., then we recommend checking out" +
                                              " our GitHub page, where the full source for those projects is readily available to download!" +
                                              "\n\nIf you'd prefer something more instructive, we have several step-by-step " +
                                              "video tutorials for you on our YouTube channel, and our documentation is chock-full of videos, code snippets," +
                                              " and everything else you might need to become a true Nova expert!\n\nGot a feature request, have a question, or found a bug? Get " +
                                              "in touch with us via email and/or start a new discussion with the Nova Community on GitHub!";

        private const string EnjoyingNova = "Enjoying Nova? Mind leaving us a ";
        private const string LeaveReview = "review?";
        private const string NovaFAQ = "Nova FAQ";
        private const float FooterHeight = 36;
        private const float SectionGap = 18;
        private const float LogoWidthPercentOfLogoTexture = 0.75f;

        private static float LogoWidth => Labels.Logo.image.width;
        private static float LogoHeight => Labels.Logo.image.height;

        // was trying to use EditorStyles.inspectorDefaulMargins,
        // but the internal instance of EditorStyles isn't always initialized
        // when we go to access it. Using this instead, as it's more reliable.
        private static RectOffset FallbackPadding => new RectOffset(18, 4, 4, 0);

        private static Rect Window
        {
            get
            {
                Vector2 padding = new Vector2(FallbackPadding.horizontal, FallbackPadding.vertical);
                Vector2 size = 0.5f * new Vector2(LogoWidth, LogoWidth) + padding;
                Vector2 center = EditorGUIUtility.GetMainWindowPosition().center;

                return new Rect(center - (0.5f * size), size);
            }
        }

        [InitializeOnLoadMethod]
        private static void OpenWindow()
        {
            EditorApplication.delayCall += ShowHelpDialogFirstTime;
        }

        private static void ShowHelpDialogFirstTime()
        {
            if (NovaEditorPrefs.HelpDialogPresented)
            {
                return;
            }

            ShowHelpDialog();
        }

        public static void ShowHelpDialog()
        {
            GetOrCreateWindow().Show();
        }

        private void OnEnable()
        {
            NovaEditorPrefs.HelpDialogPresented = true;
        }

        Vector2 scrollPosition = Vector2.zero;

        private void OnGUI()
        {
            float height = Mathf.Min(position.size.y, LogoWidth) / 3f;
            float width = Mathf.Min(position.size.x, height * LogoWidth / LogoHeight);

            Rect logo = NovaGUI.Layout.GetControlRect(GUILayout.Height(height));
            logo = logo.Center(width);

            EditorGUI.LabelField(logo, Labels.Logo);

            NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());

            EditorGUILayout.Space(SectionGap);

            DrawLinks(logo.width * LogoWidthPercentOfLogoTexture);

            EditorGUILayout.Space(SectionGap);

            DrawWelcomeMessage();

            DrawLeaveReview();
        }

        private void DrawWelcomeMessage()
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            previousRect.y -= NovaGUI.MinSpaceBetweenFields;

            NovaGUI.Styles.DrawSeparator(previousRect);

            float scrollViewHeight = (position.size.y * 0.5f) - (FooterHeight + SectionGap);

            Rect scrollRect = previousRect.Center(position.size.x);
            scrollRect.y = previousRect.yMax;
            scrollRect.height = scrollViewHeight + NovaGUI.MinSpaceBetweenFields;
            NovaGUI.Styles.Draw(scrollRect, NovaGUI.Styles.OverlayColor);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollViewHeight));
            EditorGUILayout.LabelField(WelcomeMessage, NovaGUI.Styles.ParagraphLabel);
            EditorGUILayout.EndScrollView();

            NovaGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
        }

        private void DrawLinks(float totalWidth)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            float fieldWidth = EditorGUIUtility.fieldWidth;

            GUIStyle labelStyle = NovaGUI.Styles.LargeHeader;

            Vector2 labelSize = labelStyle.CalcSize(EditorGUIUtility.TrTempContent("UI Playground"));
            float column1 = labelSize.x;
            float column2 = labelStyle.CalcSize(EditorGUIUtility.TrTempContent("Discover")).x;
            float column3 = labelStyle.CalcSize(EditorGUIUtility.TrTempContent("Manual")).x;
            float column4 = labelStyle.CalcSize(EditorGUIUtility.TrTempContent("Community")).x;
            
            EditorGUIUtility.labelWidth = Mathf.Max(column1, column2, column3, column4);
            EditorGUIUtility.fieldWidth = 0;

            float totalLabelWidth = column1 + column2 + column3 + column4;

            float columnSpace = Mathf.Max((totalWidth - totalLabelWidth) / 3, NovaGUI.MinSpaceBetweenFields);

            Rect headers = NovaGUI.Layout.GetControlRect(GUILayout.Height(labelSize.y));
            headers = headers.Center(totalWidth);

            Rect row1 = NovaGUI.Layout.GetControlRect(GUILayout.Height(labelSize.y));
            row1 = row1.Center(totalWidth);

            Rect row2 = NovaGUI.Layout.GetControlRect(GUILayout.Height(labelSize.y));
            row2 = row2.Center(totalWidth);

            EditorGUI.LabelField(headers, "Experiment", labelStyle);
            
            if (NovaGUI.LinkButton(row1, "UI Controls"))
            {
                OpenScene("UIControls");
            }

            if (NovaGUI.LinkButton(row2, "UI Playground"))
            {
                OpenScene("UIPlayground");
            }

            headers.xMin += column1 + columnSpace;
            row1.xMin += column1 + columnSpace;
            row2.xMin += column1 + columnSpace;

            EditorGUI.LabelField(headers, "Discover", labelStyle);
            NovaGUI.LinkLabel(row1, "Examples", "https://novaui.io/samples/");
            NovaGUI.LinkLabel(row2, "YouTube", "https://www.youtube.com/@NovaUI");

            headers.xMin += column2 + columnSpace;
            row1.xMin += column2 + columnSpace;
            row2.xMin += column2 + columnSpace;

            EditorGUI.LabelField(headers, "Learn", labelStyle);
            NovaGUI.LinkLabel(row1, "Manual", "https://novaui.io/manual/");
            NovaGUI.LinkLabel(row2, "API", "https://novaui.io/api/");

            headers.xMin += column3 + columnSpace;
            row1.xMin += column3 + columnSpace;
            row2.xMin += column3 + columnSpace;

            EditorGUI.LabelField(headers, "Contact", labelStyle);
            NovaGUI.LinkLabel(row1, "Community", "https://github.com/NovaUI-Unity/Feedback/discussions");
            NovaGUI.LinkLabel(row2, "Email", "mailto:contact@novaui.io");

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUIUtility.fieldWidth = fieldWidth;
        }

        private void DrawLeaveReview()
        {
            Rect position = NovaGUI.Layout.GetControlRect(GUILayout.Height(FooterHeight));

            float questionWidth = EditorStyles.label.CalcSize(EditorGUIUtility.TrTempContent(EnjoyingNova)).x;
            float answerWidth = EditorStyles.linkLabel.CalcSize(EditorGUIUtility.TrTempContent(LeaveReview)).x;

            position = position.Center(new Vector2(questionWidth + answerWidth, EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing));

            float labelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = questionWidth;
            EditorGUI.LabelField(position, EnjoyingNova);
            EditorGUIUtility.labelWidth = labelWidth;

            position.xMin += questionWidth;
            NovaGUI.LinkLabel(position, LeaveReview, "https://u3d.as/2Sge", largeLink: false);
        }

        private static NovaHelpWindow GetOrCreateWindow()
        {
            NovaHelpWindow window = GetWindow<NovaHelpWindow>(NovaFAQ);

            if (window == null)
            {
                window = CreateWindow<NovaHelpWindow>(NovaFAQ);
            }

            window.position = Window;

            return window;
        }

        private static void OpenScene(string sceneName)
        { 
            string[] guids = AssetDatabase.FindAssets($"{sceneName} t:scene");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.Contains("Nova"))
                {
                    SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    AssetDatabase.OpenAsset(scene);
                    break;
                }
            }
        }
    }
}
