using System;
using System.Linq;
using JellyField.Core;
using JellyField.Data;
using JellyField.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace JellyField.Editor
{
    [CustomEditor(typeof(LevelAsset))]
    public class LevelAssetEditor : UnityEditor.Editor
    {
        private static readonly string[] ColorNames =
        {
            "Trống",
            "Xanh lá",
            "Cyan",
            "Hồng",
            "Tím",
            "Vàng"
        };
        private static readonly Color[] Colors =
        {
            new Color(.16f, .19f, .24f),
            new Color(.23f, .83f, .29f),
            new Color(.05f, .76f, .85f),
            new Color(.98f, .24f, .64f),
            new Color(.62f, .19f, .92f),
            new Color(1, .75f, .05f)
        };
        private ReorderableList queue;
        private int selectedCell, brush = 1, newSize;
        private string solution;
        private LevelAsset Level => (LevelAsset)target;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            newSize = Level.width;
            queue = new ReorderableList(serializedObject, serializedObject.FindProperty("sequence"), true, true, true, true);
            queue.elementHeight = 70;
            queue.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Jelly vào khay");
            queue.drawElementCallback = (rect, index, active, focused) =>
            {
                var item = queue.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.LabelField(new Rect(rect.x, rect.y + 20, 42, 20), (index + 1).ToString());
                DrawJelly(new Rect(rect.x + 45, rect.y + 3, 62, 62), item.FindPropertyRelative("colors"), true);
                if (GUI.Button(new Rect(rect.x + 118, rect.y + 19, 100, 26), "Tô toàn khối"))
                    Fill(item.FindPropertyRelative("colors"));
                for (int c = 1; c <= 5; c++)
                {
                    var swatch = new Rect(rect.x + 118 + (c - 1) * 22, rect.y + 49, 19, 17);
                    EditorGUI.DrawRect(swatch, Colors[c]);
                    if (GUI.Button(swatch, new GUIContent(brush == c ? "•" : "", ColorNames[c]), GUIStyle.none))
                        brush = c;
                }
            };
            queue.onAddCallback = list =>
            {
                int index = list.serializedProperty.arraySize++;
                Fill(list.serializedProperty.GetArrayElementAtIndex(index).FindPropertyRelative("colors"));
                list.index = index;
            };
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
        }

        private void OnUndo()
        {
            newSize = Level.width;
            solution = null;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.LabelField("THIẾT KẾ MÀN JELLY FIELD", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelName"), new GUIContent("Tên màn"));
            newSize = EditorGUILayout.IntSlider("Cạnh khung lưới vuông", newSize, 1, 6);
            EditorGUILayout.LabelField("Kích thước", newSize + " × " + newSize);
            using (new EditorGUI.DisabledScope(newSize == Level.width && newSize == Level.height && Level.cells != null && Level.cells.Length == newSize * newSize && Level.cells.All(c => c != null)))
                if (GUILayout.Button("Áp dụng kích thước"))
                    ResizeBoard();
            EditorGUILayout.IntSlider(serializedObject.FindProperty("traySize"), 1, 2, new GUIContent("Số vị trí khay"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MỤC TIÊU", EditorStyles.boldLabel);
            var goals = serializedObject.FindProperty("goals");
            for (int color = 1; color <= 5; color++)
            {
                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 16, 16), Colors[color]);
                var value = goals.GetArrayElementAtIndex(color);
                value.intValue = Mathf.Max(0, EditorGUI.IntField(new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height), ColorNames[color], value.intValue));
            }

            EditorGUILayout.LabelField("BẢNG MÀU", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
                for (int c = 1; c <= 5; c++)
                {
                    var previous = GUI.backgroundColor;
                    GUI.backgroundColor = Colors[c];
                    if (GUILayout.Button((brush == c ? "✓ " : "") + ColorNames[c], GUILayout.Height(30)))
                        brush = c;
                    GUI.backgroundColor = previous;
                }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BÀN BAN ĐẦU", EditorStyles.boldLabel);
            DrawBoard();
            DrawSelectedCell();
            EditorGUILayout.Space();
            queue.DoLayoutList();
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
                solution = null;
            using (new EditorGUI.DisabledScope(Level.Errors(false).Count > 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Tạo chuỗi khay có lời giải", GUILayout.Height(28)))
                    GenerateTray();
            var errors = Level.Errors();
            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            using (new EditorGUI.DisabledScope(errors.Count > 0 || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Tìm lời giải", GUILayout.Height(28)))
                    solution = LevelSearch.Find(Level.ToDefinition());
                if (GUILayout.Button("Chơi màn này trong Scene", GUILayout.Height(28)))
                    PlayLevel();
            }

            if (!string.IsNullOrEmpty(solution))
                EditorGUILayout.HelpBox(solution, MessageType.Info);
            if (errors.Count == 0)
                DrawWarnings();
        }

        private void GenerateTray()
        {
            var definition = new LevelDefinition
            {
                Name = Level.levelName, Width = Level.width, Height = Level.height,
                TraySize = Level.traySize, Mask = Level.cells.Select(cell => cell.enabled).ToArray(),
                Goals = (int[])Level.goals.Clone()
            };
            for (int i = 0; i < Level.cells.Length; i++)
                if (Level.cells[i].enabled && Level.cells[i].hasJelly)
                    definition.Initial.Add(i, Level.cells[i].jelly.ToPiece());
            var generated = TraySequenceGenerator.Generate(definition);
            if (generated == null)
            {
                solution = "Chưa tìm được chuỗi có lời giải trong giới hạn tìm kiếm. Chuỗi cũ được giữ nguyên.";
                return;
            }
            Undo.RecordObject(Level, "Generate tray sequence");
            Level.sequence = generated.Sequence.Select(piece => new JellyLayout { colors = (JellyColor[])piece.Cells.Clone() }).ToList();
            EditorUtility.SetDirty(Level);
            serializedObject.Update();
            AssetDatabase.SaveAssetIfDirty(Level);
            solution = "Đã tạo chuỗi và kiểm chứng cách thắng:\n" + string.Join("\n", generated.Cells.Select((cell, i) =>
                (i + 1) + ". Khay 1 → hàng " + (cell / Level.width + 1) + ", cột " + (cell % Level.width + 1)));
        }

        private void DrawBoard()
        {
            var cells = serializedObject.FindProperty("cells");
            float size = Mathf.Min(55, (EditorGUIUtility.currentViewWidth - 45) / Mathf.Max(1, Level.width));
            for (int row = 0; row < Level.height; row++)
            {
                var rowRect = GUILayoutUtility.GetRect(size * Level.width, size);
                for (int col = 0; col < Level.width; col++)
                {
                    int index = row * Level.width + col;
                    if (index >= cells.arraySize)
                        continue;
                    var cell = cells.GetArrayElementAtIndex(index);
                    var rect = new Rect(rowRect.x + col * size, rowRect.y, size - 3, size - 3);
                    if (GUI.Button(rect, GUIContent.none))
                    {
                        selectedCell = index;
                        GUI.FocusControl(null);
                    }

                    var inside = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
                    bool exists = cell.FindPropertyRelative("enabled").boolValue;
                    if (exists && cell.FindPropertyRelative("hasJelly").boolValue)
                        DrawJelly(inside, cell.FindPropertyRelative("jelly").FindPropertyRelative("colors"), false);
                    else
                    {
                        EditorGUI.DrawRect(inside, exists ? Colors[0] : new Color(.1f, .1f, .1f));
                        GUI.Label(inside, exists ? "+" : "×", EditorStyles.centeredGreyMiniLabel);
                    }

                    if (selectedCell == index)
                    {
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), Color.white);
                        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Color.white);
                    }
                }
            }
        }

        private void DrawSelectedCell()
        {
            var cells = serializedObject.FindProperty("cells");
            if (cells.arraySize == 0)
                return;
            selectedCell = Mathf.Clamp(selectedCell, 0, cells.arraySize - 1);
            var cell = cells.GetArrayElementAtIndex(selectedCell);
            EditorGUILayout.LabelField("Ô đang chọn: hàng " + (selectedCell / Level.width + 1) + ", cột " + (selectedCell % Level.width + 1));
            var enabled = cell.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent("Có ô trên bàn"));
            if (!enabled.boolValue)
                return;
            var hasJelly = cell.FindPropertyRelative("hasJelly");
            EditorGUILayout.PropertyField(hasJelly, new GUIContent("Đặt sẵn jelly"));
            if (hasJelly.boolValue)
            {
                var colors = cell.FindPropertyRelative("jelly").FindPropertyRelative("colors");
                var rect = GUILayoutUtility.GetRect(90, 90);
                DrawJelly(new Rect(rect.x, rect.y, 86, 86), colors, true);
                if (GUI.Button(new Rect(rect.x + 100, rect.y + 25, 130, 28), "Tô toàn khối"))
                    Fill(colors);
            }
        }

        private void DrawJelly(Rect rect, SerializedProperty colors, bool paint)
        {
            for (int q = 0; q < 4; q++)
            {
                var corner = new Rect(rect.x + (q % 2) * rect.width / 2, rect.y + (q / 2) * rect.height / 2, rect.width / 2 - 1, rect.height / 2 - 1);
                int color = q < colors.arraySize ? colors.GetArrayElementAtIndex(q).enumValueIndex : 0;
                EditorGUI.DrawRect(corner, Colors[Mathf.Clamp(color, 0, 5)]);
                if (paint && GUI.Button(corner, GUIContent.none, GUIStyle.none))
                    colors.GetArrayElementAtIndex(q).enumValueIndex = brush;
            }
        }

        private void Fill(SerializedProperty colors)
        {
            colors.arraySize = 4;
            for (int q = 0; q < 4; q++)
                colors.GetArrayElementAtIndex(q).enumValueIndex = brush;
        }

        private void ResizeBoard()
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(Level, "Resize jelly board");
            var resized = new LevelCell[newSize * newSize];
            for (int row = 0; row < newSize; row++)
                for (int col = 0; col < newSize; col++)
                    resized[row * newSize + col] = Level.cells != null && row < Level.height && col < Level.width && row * Level.width + col < Level.cells.Length ? Level.cells[row * Level.width + col] ?? new LevelCell() : new LevelCell();
            Level.cells = resized;
            Level.width = newSize;
            Level.height = newSize;
            EditorUtility.SetDirty(Level);
            serializedObject.Update();
            solution = null;
        }

        private void PlayLevel()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene("Assets/Scenes/JellyField.unity");
            var game = UnityEngine.Object.FindAnyObjectByType<JellyGame>();
            var settings = new SerializedObject(game);
            var campaign = settings.FindProperty("campaign").objectReferenceValue as Campaign;
            int index = campaign == null ? -1 : Array.IndexOf(campaign.levels, Level);
            if (index < 0)
            {
                solution = "Level chưa thuộc Campaign của Game Controller.";
                return;
            }

            settings.FindProperty("startLevel").intValue = index;
            settings.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorApplication.isPlaying = true;
        }

        private void DrawWarnings()
        {
            EditorGUILayout.Space();
            var all = Level.cells.Where(c => c.enabled && c.hasJelly).Select(c => c.jelly).Concat(Level.sequence).ToArray();
            for (int c = 1; c <= 5; c++)
            {
                int available = all.Count(p => p.colors.Contains((JellyColor)c));
                bool repeats = Level.sequence.Any(p => p.colors.Contains((JellyColor)c));
                if (!repeats && Level.goals[c] > available)
                    EditorGUILayout.HelpBox("Mục tiêu " + ColorNames[c] + " vượt số vùng màu có thể cung cấp (" + available + "). Màn này không thể thắng.", MessageType.Warning);
            }

            if (Level.ToDefinition().CreateBoard().FindMatches().Total > 0)
                EditorGUILayout.HelpBox("Bàn ban đầu có màu đã chạm nhau. Chúng sẽ được xử lý sau lượt đặt đầu tiên.", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(Campaign))]
    public class CampaignEditor : UnityEditor.Editor
    {
        [MenuItem("Jelly Field/Edit campaign")]
        private static void OpenCampaign()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Campaign>("Assets/Levels/Campaign.asset");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var campaign = (Campaign)target;
            try
            {
                campaign.Create();
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }
    }
}
