using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Options window shared by the title screen and the pause menu. GENERAL: volume, fullscreen, VSync.
/// CONTROLS: click a key, press the new key (Esc cancels); a key already used by another action is swapped.
/// </summary>
public sealed class OptionsPanel : MonoBehaviour
{
    private const float Width = 380f;
    private const float Height = 270f;

    /// <summary>Frame in which an options window closed: the same Esc press must not also close its parent.</summary>
    public static int ClosedFrame { get; private set; } = -1;

    private Action onClose;
    private PlayerInput input;
    private RectTransform tabContent;
    private MenuButton generalTab;
    private MenuButton controlsTab;
    private MenuButton backButton;
    private readonly List<Selectable> currentItems = new();
    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private int rebindEndedFrame = -1;
    private bool showingControls;

    public static OptionsPanel Open(Transform frame, Action onClose)
    {
        var root = MenuUI.Stretch("Options", frame);
        MenuUI.Panel(root, null, MenuUI.Theme != null ? MenuUI.Theme.backdrop : new Color(0f, 0f, 0f, 0.7f))
            .raycastTarget = true;
        var panel = root.gameObject.AddComponent<OptionsPanel>();
        panel.onClose = onClose;
        panel.Build();
        return panel;
    }

    private void Build()
    {
        input = new PlayerInput();
        KeyBindings.Register(input.asset);

        float x = (MenuUI.VirtualWidth - Width) / 2f;
        float y = (MenuUI.VirtualHeight - Height) / 2f;
        MenuUI.Window("Window", transform, x, y, Width, Height, out RectTransform content);
        float w = Width - 24f;

        MenuUI.Label(content, "OPTIONS", 0f, 2f, w, 18f, 16, TextAnchor.UpperCenter, heading: true, shadow: true);
        generalTab = MenuUI.MakeButton(content, "GENERAL", w / 2f - 112f, 24f, 104f, 18f, () => ShowTab(false), 8);
        controlsTab = MenuUI.MakeButton(content, "CONTROLS", w / 2f + 8f, 24f, 104f, 18f, () => ShowTab(true), 8);

        RectTransform body = MenuUI.Box("Tab Body", content, 0f, 48f, w, Height - 106f);
        MenuUI.Panel(body, MenuUI.Theme != null ? MenuUI.Theme.panelDark : null);
        tabContent = MenuUI.Stretch("Tab Content", body, 8f);

        backButton = MenuUI.MakeButton(content, "BACK", w / 2f - 50f, Height - 50f, 100f, 18f, Close, 8);
        ShowTab(false);
    }

    private void ShowTab(bool controls)
    {
        CancelRebind();
        showingControls = controls;
        // the open tab looks pressed in
        MenuTheme theme = MenuUI.Theme;
        if (theme != null)
        {
            generalTab.image.sprite = controls ? theme.buttonNormal : theme.buttonPressed;
            controlsTab.image.sprite = controls ? theme.buttonPressed : theme.buttonNormal;
        }
        foreach (Transform child in tabContent)
            Destroy(child.gameObject);
        currentItems.Clear();

        if (controls)
            BuildControls();
        else
            BuildGeneral();

        // Navigation: tabs on top, the tab's items, then Back.
        var order = new List<Selectable> { controls ? controlsTab : generalTab };
        order.AddRange(currentItems);
        order.Add(backButton);
        MenuUI.LinkVertical(order);
        SetHorizontal(generalTab, controlsTab);
        MenuUI.Select(currentItems.Count > 0 ? currentItems[0] : backButton);
    }

    private static void SetHorizontal(Selectable left, Selectable right)
    {
        Navigation l = left.navigation;
        l.mode = Navigation.Mode.Explicit;
        l.selectOnRight = right;
        left.navigation = l;
        Navigation r = right.navigation;
        r.mode = Navigation.Mode.Explicit;
        r.selectOnLeft = left;
        right.navigation = r;
    }

    private void BuildGeneral()
    {
        float w = tabContent.rect.width > 0f ? tabContent.rect.width : Width - 40f;
        Text volumeValue = MenuUI.Label(tabContent, "", w - 40f, 6f, 40f, 12f, 8, TextAnchor.UpperRight);
        MenuUI.Label(tabContent, "Master Volume", 8f, 6f, 120f, 12f);
        void ShowVolume(float v) => volumeValue.text = $"{Mathf.RoundToInt(v * 100f)}%";
        ShowVolume(GameSettings.MasterVolume);
        Slider volume = MenuUI.MakeSlider(tabContent, 130f, 6f, w - 180f, GameSettings.MasterVolume, v =>
        {
            GameSettings.MasterVolume = v;
            ShowVolume(v);
        });
        currentItems.Add(volume);

        currentItems.Add(MenuUI.MakeToggleRow(tabContent, "Fullscreen", 0f, 30f, w, GameSettings.Fullscreen,
            v => GameSettings.Fullscreen = v));
        currentItems.Add(MenuUI.MakeToggleRow(tabContent, "VSync", 0f, 52f, w, GameSettings.VSync,
            v => GameSettings.VSync = v));
    }

    private void BuildControls()
    {
        float w = tabContent.rect.width > 0f ? tabContent.rect.width : Width - 40f;
        float y = 0f;
        foreach (KeyBindings.Entry entry in KeyBindings.Rebindable)
        {
            KeyBindings.Entry captured = entry;
            MenuUI.Label(tabContent, entry.Label, 8f, y + 5f, 140f, 12f);
            MenuButton key = MenuUI.MakeButton(tabContent, "", w - 110f, y, 110f, 18f, null, 8);
            key.SetAction(() => StartRebind(captured, key));
            RefreshKey(entry, key);
            currentItems.Add(key);
            y += 21f;
        }

        currentItems.Add(MenuUI.MakeButton(tabContent, "RESET DEFAULTS", w / 2f - 60f, y + 4f, 120f, 18f, () =>
        {
            KeyBindings.ResetToDefaults(input.asset);
            ShowTab(true);
        }, 8));
    }

    private void RefreshKey(KeyBindings.Entry entry, MenuButton key)
    {
        InputAction action = input.asset.FindAction(entry.Action);
        key.SetText(KeyBindings.DisplayName(action, action != null ? KeyBindings.FindBindingIndex(action, entry) : -1));
    }

    private void StartRebind(KeyBindings.Entry entry, MenuButton key)
    {
        CancelRebind();
        InputAction action = input.asset.FindAction(entry.Action);
        int index = action != null ? KeyBindings.FindBindingIndex(action, entry) : -1;
        if (index < 0)
            return;

        string oldPath = action.bindings[index].effectivePath;
        key.SetText("PRESS A KEY...");
        SetNavigation(false);
        input.asset.Disable();

        var op = action.PerformInteractiveRebinding(index)
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Pointer>")
            .OnMatchWaitForAnother(0.05f);
        if (entry.Action != "Pause")
            op.WithCancelingThrough("<Keyboard>/escape");

        rebinding = op
            .OnComplete(o =>
            {
                SwapDuplicate(action, index, oldPath);
                KeyBindings.Save(input.asset);
                EndRebind();
                int row = currentItems.IndexOf(key);
                ShowTab(true); // redraws every key (a swapped key changed too)
                if (row >= 0 && row < currentItems.Count)
                    MenuUI.Select(currentItems[row]);
            })
            .OnCancel(o =>
            {
                EndRebind();
                RefreshKey(entry, key);
            })
            .Start();
    }

    /// <summary>If the new key is used by another rebindable action, that action gets the old key.</summary>
    private void SwapDuplicate(InputAction changed, int changedIndex, string oldPath)
    {
        string newPath = changed.bindings[changedIndex].effectivePath;
        foreach (KeyBindings.Entry other in KeyBindings.Rebindable)
        {
            InputAction action = input.asset.FindAction(other.Action);
            int index = action != null ? KeyBindings.FindBindingIndex(action, other) : -1;
            if (index < 0 || (action == changed && index == changedIndex))
                continue;

            if (string.Equals(action.bindings[index].effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                action.ApplyBindingOverride(index, oldPath);
        }
    }

    private void EndRebind()
    {
        rebinding?.Dispose();
        rebinding = null;
        rebindEndedFrame = Time.frameCount;
        StartCoroutine(RestoreNavigationNextFrame());
    }

    private void CancelRebind()
    {
        if (rebinding == null)
            return;

        rebinding.Cancel();
        rebinding?.Dispose();
        rebinding = null;
        SetNavigation(true);
    }

    private IEnumerator RestoreNavigationNextFrame()
    {
        // The key that completed the rebind (Enter, Space…) must not also click the focused button.
        yield return null;
        yield return null;
        SetNavigation(true);
    }

    private static void SetNavigation(bool on)
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = on;
    }

    private void Update()
    {
        if (rebinding != null || Time.frameCount - rebindEndedFrame < 3)
            return;

        if (MenuInput.CancelPressed())
            Close();
    }

    public void Close()
    {
        if (this == null)
            return;

        CancelRebind();
        GameSettings.Save();
        ClosedFrame = Time.frameCount;
        Action callback = onClose;
        onClose = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        CancelRebind();
        SetNavigation(true);
        if (input != null)
        {
            KeyBindings.Unregister(input.asset);
            input.Dispose();
            input = null;
        }
    }
}

/// <summary>Menu-level input read from the UI input module (Esc / Backspace / gamepad B = cancel).</summary>
public static class MenuInput
{
    public static bool CancelPressed()
    {
        if (EventSystem.current != null
            && EventSystem.current.currentInputModule is InputSystemUIInputModule module
            && module.cancel != null && module.cancel.action != null
            && module.cancel.action.WasPressedThisFrame())
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame);
    }
}
