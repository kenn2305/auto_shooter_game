using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

public class AnimationHitboxEditorWindow : EditorWindow
{
    private const float PreviewHeight = 230f;

    private enum PreviewDragMode
    {
        None,
        Move,
        Resize,
    }

    private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();

    [SerializeField] private AnimationHitboxTrack track;
    private SerializedObject trackSerialized;
    private SerializedProperty animatorProperty;
    private SerializedProperty clipProperty;
    private SerializedProperty targetLayerProperty;
    private SerializedProperty framesProperty;
    private SerializedProperty currentBoxesProperty;

    private ReorderableList boxList;
    [SerializeField] private int currentFrame;
    [SerializeField] private int selectedBox = -1;
    private Vector2 timelineScroll;
    private Vector2 windowScroll;

    private bool previewPlaying;
    private bool ownsAnimationMode;
    private double lastEditorTime;
    private float previewTime;
    private string previewError;

    private PreviewDragMode previewDragMode;
    private int previewDragBox = -1;
    private Vector2 previewDragStartMouse;
    private Vector2 previewDragStartOffset;
    private Vector2 previewDragStartSize;

    [MenuItem("Tools/Animation Hitbox Editor")]
    public static void OpenWindow()
    {
        AnimationHitboxEditorWindow window = GetWindow<AnimationHitboxEditorWindow>();

        window.titleContent = new GUIContent("Animation Hitbox");
        window.minSize = new Vector2(430f, 560f);
        window.TryUseSelection();
        window.Show();
    }

    public static void Open(AnimationHitboxTrack targetTrack)
    {
        OpenWindow();

        AnimationHitboxEditorWindow window = GetWindow<AnimationHitboxEditorWindow>();

        window.BindTarget(targetTrack);
        window.Focus();
    }

    private void OnEnable()
    {
        boxHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;

        SceneView.duringSceneGui += DuringSceneGUI;
        EditorApplication.update += EditorUpdate;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        lastEditorTime = EditorApplication.timeSinceStartup;

        if (track != null)
            BindTarget(track);
        else
            TryUseSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.update -= EditorUpdate;
        EditorApplication.playModeStateChanged -= PlayModeChanged;

        StopPreviewMode();
    }

    private void OnSelectionChange()
    {
        AnimationHitboxTrack selectedTrack =
            Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<AnimationHitboxTrack>() : null;

        if (selectedTrack != null && selectedTrack != track)
            BindTarget(selectedTrack);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();

        AnimationHitboxTrack newTrack = (AnimationHitboxTrack)
            EditorGUILayout.ObjectField("Hitbox Track", track, typeof(AnimationHitboxTrack), true);

        if (EditorGUI.EndChangeCheck())
            BindTarget(newTrack);

        if (track == null)
        {
            DrawNoTargetUI();
            return;
        }

        if (trackSerialized == null || trackSerialized.targetObject == null)
        {
            BindTarget(track);
        }

        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        DrawTrackSettings();
        DrawAnimationControls();
        DrawSpritePreview();
        DrawFrameHitboxes();

        EditorGUILayout.EndScrollView();
    }

    private void DrawNoTargetUI()
    {
        EditorGUILayout.HelpBox(
            "Chọn GameObject có AnimationHitboxTrack, hoặc thêm component "
                + "vào GameObject đang chứa Animator/SpriteRenderer.",
            MessageType.Info
        );

        GameObject selectedObject = Selection.activeGameObject;

        using (new EditorGUI.DisabledScope(selectedObject == null))
        {
            if (GUILayout.Button("Add Track To Selected GameObject", GUILayout.Height(28f)))
            {
                AnimationHitboxTrack newTrack = Undo.AddComponent<AnimationHitboxTrack>(selectedObject);

                BindTarget(newTrack);
                Selection.activeGameObject = selectedObject;
            }
        }
    }

    private void DrawTrackSettings()
    {
        trackSerialized.Update();

        EditorGUILayout.LabelField("Animation Source", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(animatorProperty);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(clipProperty);
        bool clipChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetLayerProperty);

        trackSerialized.ApplyModifiedProperties();

        if (clipChanged)
        {
            StopPreviewMode();
            SyncFrames();
        }

        using (new EditorGUI.DisabledScope(track.AnimationClip == null))
        {
            if (GUILayout.Button("Sync Frames From Animation Clip"))
                SyncFrames();
        }

        AnimationClip clip = track.AnimationClip;

        if (clip == null)
        {
            EditorGUILayout.HelpBox("Hãy gán Animation Clip để tạo frame.", MessageType.Warning);

            return;
        }

        EditorGUILayout.HelpBox(
            $"{clip.name}  |  {clip.frameRate:0.##} FPS  |  "
                + $"{track.ExpectedFrameCount} frames  |  {clip.length:0.###} giây",
            MessageType.None
        );
    }

    private void DrawAnimationControls()
    {
        AnimationClip clip = track.AnimationClip;
        int frameCount = track.Frames.Count;

        if (clip == null || frameCount == 0)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("|<", GUILayout.Width(34f)))
                SetCurrentFrame(0);

            if (GUILayout.Button("<", GUILayout.Width(34f)))
                SetCurrentFrame(currentFrame - 1);

            string playLabel = previewPlaying ? "Pause" : "Play";

            if (GUILayout.Button(playLabel, GUILayout.Width(62f)))
                TogglePreviewPlayback();

            if (GUILayout.Button(">", GUILayout.Width(34f)))
                SetCurrentFrame(currentFrame + 1);

            if (GUILayout.Button(">|", GUILayout.Width(34f)))
                SetCurrentFrame(frameCount - 1);

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                $"Frame {currentFrame}/{frameCount - 1}",
                EditorStyles.boldLabel,
                GUILayout.Width(110f)
            );
        }

        int sliderFrame = EditorGUILayout.IntSlider(
            "Current Frame",
            currentFrame,
            0,
            frameCount - 1
        );

        if (sliderFrame != currentFrame)
            SetCurrentFrame(sliderFrame);

        timelineScroll = EditorGUILayout.BeginScrollView(timelineScroll, true, false, GUILayout.Height(42f));

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < frameCount; i++)
            {
                bool hasHitbox = track.Frames[i].Hitboxes.Count > 0;
                string label = hasHitbox ? $"{i} ●" : i.ToString();

                bool selected = GUILayout.Toggle(
                    i == currentFrame,
                    label,
                    EditorStyles.miniButton,
                    GUILayout.Width(48f),
                    GUILayout.Height(22f)
                );

                if (selected && i != currentFrame)
                    SetCurrentFrame(i);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSpritePreview()
    {
        AnimationClip clip = track.AnimationClip;

        if (clip == null || track.Frames.Count == 0)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(100f, PreviewHeight, GUILayout.ExpandWidth(true));

        EditorGUI.DrawRect(previewRect, new Color(0.09f, 0.1f, 0.12f, 1f));

        SpriteRenderer previewRenderer = GetPreviewSpriteRenderer();
        Sprite sprite =
            previewRenderer != null && previewRenderer.sprite != null
                ? previewRenderer.sprite : GetSpriteAtCurrentFrame();

        if (sprite == null || sprite.texture == null)
        {
            GUI.Label(
                previewRect,
                "Clip này không có SpriteRenderer/m_Sprite keyframe.",
                CenteredLabelStyle()
            );

            return;
        }

        Rect fullSpriteRect = FitRect(previewRect, sprite.rect.width / sprite.rect.height, 18f);

        Rect textureRect = sprite.textureRect;
        Texture2D texture = sprite.texture;

        Rect spriteDrawRect = GetSpriteDrawRect(
            fullSpriteRect,
            sprite,
            previewRenderer != null && previewRenderer.flipX,
            previewRenderer != null && previewRenderer.flipY
        );

        Rect uv = new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height
        );

        if (previewRenderer != null && previewRenderer.flipX)
        {
            uv.x += uv.width;
            uv.width = -uv.width;
        }

        if (previewRenderer != null && previewRenderer.flipY)
        {
            uv.y += uv.height;
            uv.height = -uv.height;
        }

        Color previousGuiColor = GUI.color;

        if (previewRenderer != null)
            GUI.color = previewRenderer.color;

        GUI.DrawTextureWithTexCoords(spriteDrawRect, texture, uv, true);
        GUI.color = previousGuiColor;
        DrawPreviewHitboxes(fullSpriteRect, sprite);

        if (!string.IsNullOrEmpty(previewError))
        {
            GUI.Label(
                new Rect(previewRect.x + 5f, previewRect.y + 5f, previewRect.width - 10f, 36f),
                previewError,
                EditorStyles.helpBox
            );
        }
    }

    private void DrawFrameHitboxes()
    {
        if (track.AnimationClip == null || track.Frames.Count == 0)
            return;

        EditorGUILayout.Space(6f);

        if (boxList == null)
            BuildBoxList();

        boxList?.DoLayoutList();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(currentFrame <= 0))
            {
                if (GUILayout.Button("Copy Previous Frame"))
                {
                    Undo.RecordObject(track, "Copy Previous Hitbox Frame");
                    track.CopyFrame(currentFrame - 1, currentFrame);
                    SaveTrackChanges();
                    BuildBoxList();
                }
            }

            using (new EditorGUI.DisabledScope(track.Frames[currentFrame].Hitboxes.Count == 0))
            {
                if (GUILayout.Button("Clear Frame"))
                {
                    Undo.RecordObject(track, "Clear Hitbox Frame");
                    track.ClearFrame(currentFrame);
                    selectedBox = -1;
                    SaveTrackChanges();
                    BuildBoxList();
                }
            }
        }
    }

    private void BuildBoxList()
    {
        if (trackSerialized == null || framesProperty == null)
            return;

        trackSerialized.Update();

        if (framesProperty.arraySize == 0)
        {
            boxList = null;
            currentBoxesProperty = null;
            selectedBox = -1;
            return;
        }

        currentFrame = Mathf.Clamp(currentFrame, 0, framesProperty.arraySize - 1);

        SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(currentFrame);

        currentBoxesProperty = frameProperty.FindPropertyRelative("hitboxes");

        boxList = new ReorderableList(trackSerialized, currentBoxesProperty, true, true, true, true);

        boxList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, $"Frame {currentFrame} Hitboxes");

        boxList.elementHeightCallback = index =>
            EditorGUI.GetPropertyHeight(currentBoxesProperty.GetArrayElementAtIndex(index), true) + 4f;

        boxList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            SerializedProperty boxProperty = currentBoxesProperty.GetArrayElementAtIndex(index);

            rect.y += 2f;
            rect.height = EditorGUI.GetPropertyHeight(boxProperty, true);

            EditorGUI.PropertyField(rect, boxProperty, new GUIContent($"Hitbox {index}"), true);
        };

        boxList.onSelectCallback = list =>
        {
            SelectBox(list.index);
        };

        boxList.onAddCallback = AddBox;
        boxList.onRemoveCallback = RemoveBox;
        boxList.onReorderCallback = list =>
        {
            selectedBox = list.index;
            trackSerialized.ApplyModifiedProperties();
            SaveTrackChanges();
        };

        selectedBox =
            currentBoxesProperty.arraySize > 0 ? Mathf.Clamp(selectedBox, 0, currentBoxesProperty.arraySize - 1) : -1;

        boxList.index = selectedBox;
        track.SetEditorPreview(currentFrame, selectedBox);
    }

    private void AddBox(ReorderableList list)
    {
        trackSerialized.Update();

        int newIndex = currentBoxesProperty.arraySize;
        currentBoxesProperty.arraySize++;

        SerializedProperty newBox = currentBoxesProperty.GetArrayElementAtIndex(newIndex);

        newBox.FindPropertyRelative("boxName").stringValue = $"Hitbox {newIndex}";

        newBox.FindPropertyRelative("enabled").boolValue = true;
        newBox.FindPropertyRelative("offset").vector2Value = Vector2.zero;
        newBox.FindPropertyRelative("size").vector2Value = Vector2.one;

        trackSerialized.ApplyModifiedProperties();

        selectedBox = newIndex;
        list.index = newIndex;

        SaveTrackChanges();
        SceneView.RepaintAll();
        Repaint();
    }

    private void RemoveBox(ReorderableList list)
    {
        if (list.index < 0 || list.index >= currentBoxesProperty.arraySize)
        {
            return;
        }

        trackSerialized.Update();
        currentBoxesProperty.DeleteArrayElementAtIndex(list.index);

        selectedBox =
            currentBoxesProperty.arraySize > 0 ? Mathf.Clamp(list.index, 0, currentBoxesProperty.arraySize - 1) : -1;

        trackSerialized.ApplyModifiedProperties();
        list.index = selectedBox;

        SaveTrackChanges();
        SceneView.RepaintAll();
        Repaint();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (track == null || Application.isPlaying || currentFrame < 0 || currentFrame >= track.Frames.Count)
        {
            return;
        }

        AnimationHitboxFrameData frame = track.Frames[currentFrame];

        Handles.matrix = track.transform.localToWorldMatrix;

        for (int i = 0; i < frame.Hitboxes.Count; i++)
        {
            HitboxData box = frame.Hitboxes[i];

            if (!box.enabled)
                continue;

            bool isSelected = i == selectedBox;
            Handles.color = isSelected ? Color.yellow : Color.cyan;

            Handles.DrawWireCube(box.offset, box.size);

            if (!isSelected && Handles.Button(box.offset, Quaternion.identity, 0.08f, 0.1f, Handles.RectangleHandleCap))
            {
                SelectBox(i);
            }
        }

        if (selectedBox >= 0 && selectedBox < frame.Hitboxes.Count)
        {
            DrawSelectedBoxHandle(frame.Hitboxes[selectedBox]);
        }

        Handles.matrix = Matrix4x4.identity;
    }

    private void DrawSelectedBoxHandle(HitboxData selected)
    {
        boxHandle.center = new Vector3(selected.offset.x, selected.offset.y, 0f);

        boxHandle.size = new Vector3(Mathf.Max(0.01f, selected.size.x), Mathf.Max(0.01f, selected.size.y), 0.01f);

        boxHandle.SetColor(Color.yellow);

        EditorGUI.BeginChangeCheck();

        Vector3 newCenter = Handles.PositionHandle(boxHandle.center, Quaternion.identity);

        boxHandle.center = newCenter;
        boxHandle.DrawHandle();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(track, "Edit Animation Hitbox");

            selected.offset = new Vector2(boxHandle.center.x, boxHandle.center.y);

            selected.size = new Vector2(Mathf.Max(0.01f, boxHandle.size.x), Mathf.Max(0.01f, boxHandle.size.y));

            SaveTrackChanges();
            Repaint();
        }

        Handles.Label(boxHandle.center, $"Frame {currentFrame} / Hitbox {selectedBox}");
    }

    private void DrawPreviewHitboxes(Rect spriteRect, Sprite sprite)
    {
        if (currentFrame < 0 || currentFrame >= track.Frames.Count)
            return;

        AnimationHitboxFrameData frame = track.Frames[currentFrame];
        Rect localSpriteRect = GetSpriteLocalRect(sprite);

        if (localSpriteRect.width <= 0f || localSpriteRect.height <= 0f)
            return;

        float pixelsPerUnit = Mathf.Min(
            spriteRect.width / localSpriteRect.width,
            spriteRect.height / localSpriteRect.height
        );

        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(
            "AnimationHitboxPreviewDrag".GetHashCode(),
            FocusType.Passive
        );

        Rect[] boxRects = new Rect[frame.Hitboxes.Count];
        Rect[] resizeRects = new Rect[frame.Hitboxes.Count];
        bool[] visibleBoxes = new bool[frame.Hitboxes.Count];

        for (int i = 0; i < frame.Hitboxes.Count; i++)
        {
            HitboxData box = frame.Hitboxes[i];

            if (!box.enabled)
                continue;

            Vector2 guiCenter = new Vector2(
                spriteRect.x + (box.offset.x - localSpriteRect.xMin) * pixelsPerUnit,
                spriteRect.yMax - (box.offset.y - localSpriteRect.yMin) * pixelsPerUnit
            );

            Vector2 guiSize = new Vector2(box.size.x * pixelsPerUnit, box.size.y * pixelsPerUnit);

            Rect boxRect = new Rect(
                guiCenter.x - guiSize.x * 0.5f,
                guiCenter.y - guiSize.y * 0.5f,
                guiSize.x,
                guiSize.y
            );

            boxRects[i] = boxRect;
            visibleBoxes[i] = true;

            Color color = i == selectedBox ? Color.yellow : Color.cyan;

            DrawRectOutline(boxRect, color, i == selectedBox ? 2f : 1f);

            GUI.Label(new Rect(boxRect.x + 2f, boxRect.y + 1f, 70f, 18f), i.ToString(), EditorStyles.miniBoldLabel);

            EditorGUIUtility.AddCursorRect(boxRect, MouseCursor.MoveArrow, controlId);

            if (i == selectedBox)
            {
                Rect resizeRect = new Rect(boxRect.xMax - 6f, boxRect.yMax - 6f, 12f, 12f);

                resizeRects[i] = resizeRect;
                EditorGUI.DrawRect(resizeRect, Color.yellow);

                EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeUpLeft, controlId);
            }
        }

        EventType eventType = currentEvent.GetTypeForControl(controlId);

        if (eventType == EventType.MouseDown && currentEvent.button == 0)
        {
            int clickedBox = -1;
            PreviewDragMode clickedMode = PreviewDragMode.None;

            if (
                selectedBox >= 0
                && selectedBox < resizeRects.Length && resizeRects[selectedBox].Contains(currentEvent.mousePosition)
            )
            {
                clickedBox = selectedBox;
                clickedMode = PreviewDragMode.Resize;
            }
            else
            {
                for (int i = boxRects.Length - 1; i >= 0; i--)
                {
                    if (visibleBoxes[i] && boxRects[i].Contains(currentEvent.mousePosition))
                    {
                        clickedBox = i;
                        clickedMode = PreviewDragMode.Move;
                        break;
                    }
                }
            }

            if (clickedBox >= 0)
            {
                SelectBox(clickedBox);

                HitboxData draggedBox = frame.Hitboxes[clickedBox];

                previewDragMode = clickedMode;
                previewDragBox = clickedBox;
                previewDragStartMouse = currentEvent.mousePosition;
                previewDragStartOffset = draggedBox.offset;
                previewDragStartSize = draggedBox.size;

                Undo.RecordObject(
                    track,
                    clickedMode == PreviewDragMode.Move
                        ? "Move Preview Hitbox"
                        : "Resize Preview Hitbox"
                );

                GUIUtility.hotControl = controlId;
                currentEvent.Use();
            }
        }
        else if (
            eventType == EventType.MouseDrag
            && GUIUtility.hotControl == controlId && previewDragBox >= 0 && previewDragBox < frame.Hitboxes.Count
        )
        {
            HitboxData draggedBox = frame.Hitboxes[previewDragBox];
            Vector2 pixelDelta = currentEvent.mousePosition - previewDragStartMouse;

            if (previewDragMode == PreviewDragMode.Move)
            {
                draggedBox.offset =
                    previewDragStartOffset + new Vector2(pixelDelta.x / pixelsPerUnit, -pixelDelta.y / pixelsPerUnit);
            }
            else if (previewDragMode == PreviewDragMode.Resize)
            {
                draggedBox.size = new Vector2(
                    Mathf.Max(0.01f, previewDragStartSize.x + pixelDelta.x * 2f / pixelsPerUnit),
                    Mathf.Max(0.01f, previewDragStartSize.y + pixelDelta.y * 2f / pixelsPerUnit)
                );
            }

            SaveTrackChanges();
            currentEvent.Use();
            Repaint();
        }
        else if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            previewDragMode = PreviewDragMode.None;
            previewDragBox = -1;

            SaveTrackChanges();
            currentEvent.Use();
        }
    }

    private void SelectBox(int index)
    {
        selectedBox = index;

        if (track != null)
        {
            track.SetEditorPreview(currentFrame, selectedBox);
            SaveTrackChanges();
        }

        if (boxList != null)
            boxList.index = index;

        SceneView.RepaintAll();
        Repaint();
    }

    private void SetCurrentFrame(int frameIndex)
    {
        if (track == null || track.Frames.Count == 0)
            return;

        currentFrame = Mathf.Clamp(frameIndex, 0, track.Frames.Count - 1);

        previewTime = track.AnimationClip != null ? currentFrame / track.AnimationClip.frameRate : 0f;

        selectedBox = track.Frames[currentFrame].Hitboxes.Count > 0 ? 0 : -1;

        track.SetEditorPreview(currentFrame, selectedBox);

        BuildBoxList();
        SaveTrackChanges();
        SampleCurrentFrame();
        SceneView.RepaintAll();
        Repaint();
    }

    private void TogglePreviewPlayback()
    {
        previewPlaying = !previewPlaying;
        lastEditorTime = EditorApplication.timeSinceStartup;

        if (!previewPlaying)
            SampleCurrentFrame();
    }

    private void EditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - lastEditorTime);
        lastEditorTime = now;

        if (
            !previewPlaying
            || track == null || track.AnimationClip == null || track.Frames.Count == 0 || Application.isPlaying
        )
        {
            return;
        }

        AnimationClip clip = track.AnimationClip;
        previewTime = Mathf.Repeat(previewTime + deltaTime, clip.length);

        int frame = Mathf.Clamp(Mathf.FloorToInt(previewTime * clip.frameRate), 0, track.Frames.Count - 1);

        if (frame != currentFrame)
        {
            currentFrame = frame;
            selectedBox = track.Frames[currentFrame].Hitboxes.Count > 0 ? 0 : -1;
            BuildBoxList();
        }

        SampleAtTime(previewTime);
        Repaint();
    }

    private void SampleCurrentFrame()
    {
        if (track == null || track.AnimationClip == null)
            return;

        float time = currentFrame / track.AnimationClip.frameRate;
        SampleAtTime(time);
    }

    private void SampleAtTime(float time)
    {
        if (track == null || track.AnimationClip == null || Application.isPlaying)
        {
            return;
        }

        try
        {
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(
                track.gameObject,
                track.AnimationClip,
                Mathf.Clamp(time, 0f, Mathf.Max(0f, track.AnimationClip.length - 0.0001f))
            );
            AnimationMode.EndSampling();

            previewError = null;
            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            previewError = exception.Message;

            if (AnimationMode.InAnimationMode())
                AnimationMode.EndSampling();
        }
    }

    private void StopPreviewMode()
    {
        previewPlaying = false;

        if (ownsAnimationMode && AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();

        ownsAnimationMode = false;
        previewError = null;
        SceneView.RepaintAll();
    }

    private void PlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            StopPreviewMode();
    }

    private void SyncFrames()
    {
        if (track == null)
            return;

        Undo.RecordObject(track, "Sync Animation Hitbox Frames");
        track.SyncFramesToClip();
        SaveTrackChanges();

        trackSerialized.Update();
        framesProperty = trackSerialized.FindProperty("frames");

        currentFrame = framesProperty.arraySize > 0 ? Mathf.Clamp(currentFrame, 0, framesProperty.arraySize - 1) : 0;

        selectedBox = -1;
        BuildBoxList();
        SampleCurrentFrame();
        Repaint();
    }

    private void SaveTrackChanges()
    {
        if (track == null)
            return;

        track.SetEditorPreview(currentFrame, selectedBox);
        EditorUtility.SetDirty(track);
        PrefabUtility.RecordPrefabInstancePropertyModifications(track);

        if (track.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(track.gameObject.scene);

        trackSerialized?.Update();
        SceneView.RepaintAll();
    }

    private void BindTarget(AnimationHitboxTrack newTrack)
    {
        if (track == newTrack && trackSerialized != null)
            return;

        StopPreviewMode();

        track = newTrack;
        trackSerialized = null;
        boxList = null;
        currentBoxesProperty = null;
        currentFrame = 0;
        selectedBox = -1;

        if (track == null)
        {
            Repaint();
            return;
        }

        trackSerialized = new SerializedObject(track);
        animatorProperty = trackSerialized.FindProperty("animator");
        clipProperty = trackSerialized.FindProperty("animationClip");
        targetLayerProperty = trackSerialized.FindProperty("targetLayer");
        framesProperty = trackSerialized.FindProperty("frames");

        if (track.AnimationClip != null && track.Frames.Count != track.ExpectedFrameCount)
        {
            SyncFrames();
        }
        else
        {
            BuildBoxList();
            SampleCurrentFrame();
        }

        Repaint();
    }

    private void TryUseSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        AnimationHitboxTrack selectedTrack = Selection.activeGameObject.GetComponent<AnimationHitboxTrack>();

        if (selectedTrack != null)
            BindTarget(selectedTrack);
    }

    private Sprite GetSpriteAtCurrentFrame()
    {
        AnimationClip clip = track.AnimationClip;

        if (clip == null)
            return null;

        float time = currentFrame / clip.frameRate;
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
            {
                continue;
            }

            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);

            Sprite currentSprite = null;

            foreach (ObjectReferenceKeyframe keyframe in keyframes)
            {
                if (keyframe.time > time + 0.0001f)
                    break;

                currentSprite = keyframe.value as Sprite;
            }

            return currentSprite;
        }

        return null;
    }

    private SpriteRenderer GetPreviewSpriteRenderer()
    {
        if (track == null || track.AnimationClip == null)
            return null;

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(track.AnimationClip);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
            {
                continue;
            }

            Transform animatedTransform = string.IsNullOrEmpty(binding.path)
                ? track.transform : track.transform.Find(binding.path);

            if (animatedTransform != null && animatedTransform.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                return spriteRenderer;
            }
        }

        return track.GetComponentInChildren<SpriteRenderer>();
    }

    private static Rect GetSpriteDrawRect(Rect fullSpriteRect, Sprite sprite, bool flipX, bool flipY)
    {
        Rect sourceRect = sprite.rect;
        Rect textureRect = sprite.textureRect;
        Vector2 textureOffset = sprite.textureRectOffset;

        float horizontalOffset = flipX ? sourceRect.width - textureOffset.x - textureRect.width : textureOffset.x;

        float verticalOffsetFromBottom = flipY
            ? sourceRect.height - textureOffset.y - textureRect.height : textureOffset.y;

        return new Rect(
            fullSpriteRect.x + horizontalOffset / sourceRect.width * fullSpriteRect.width,
            fullSpriteRect.y
                + (sourceRect.height - verticalOffsetFromBottom - textureRect.height)
                    / sourceRect.height * fullSpriteRect.height,
            textureRect.width / sourceRect.width * fullSpriteRect.width,
            textureRect.height / sourceRect.height * fullSpriteRect.height
        );
    }

    private static Rect GetSpriteLocalRect(Sprite sprite)
    {
        float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);

        return new Rect(
            -sprite.pivot.x / pixelsPerUnit,
            -sprite.pivot.y / pixelsPerUnit,
            sprite.rect.width / pixelsPerUnit,
            sprite.rect.height / pixelsPerUnit
        );
    }

    private static Rect FitRect(Rect outer, float aspect, float padding)
    {
        Rect available = new Rect(
            outer.x + padding,
            outer.y + padding,
            Mathf.Max(1f, outer.width - padding * 2f),
            Mathf.Max(1f, outer.height - padding * 2f)
        );

        float availableAspect = available.width / available.height;

        if (availableAspect > aspect)
        {
            float width = available.height * aspect;
            available.x += (available.width - width) * 0.5f;
            available.width = width;
        }
        else
        {
            float height = available.width / aspect;
            available.y += (available.height - height) * 0.5f;
            available.height = height;
        }

        return available;
    }

    private static void DrawRectOutline(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);

        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);

        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    private static GUIStyle CenteredLabelStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
        };

        return style;
    }
}

[CustomEditor(typeof(AnimationHitboxTrack))]
public class AnimationHitboxTrackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Open Animation Hitbox Editor", GUILayout.Height(28f)))
        {
            AnimationHitboxEditorWindow.Open((AnimationHitboxTrack)target);
        }
    }
}
