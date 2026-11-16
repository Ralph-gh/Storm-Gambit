using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;

// Bakes editable uGUI objects into LobbyScene using the project's existing artwork.
public static class LobbyUIBuilder
{
    public static readonly Color Panel = new Color32(12, 23, 36, 248);
    public static readonly Color Accent = new Color32(72, 57, 34, 255);
    public static readonly Color ButtonActive = new Color32(66, 52, 32, 255);
    public static readonly Color ButtonIdle = new Color32(16, 29, 43, 255);
    private static readonly Color Ink = new Color32(239, 240, 234, 255);
    private static readonly Color Muted = new Color32(177, 193, 204, 255);
    private static readonly Color Gold = new Color32(226, 189, 119, 255);
    private static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;

    public static LobbyBrowserUI Build(LobbySceneController controller, Sprite background)
    {
        var canvasObject = new GameObject("Multiplayer Lobby UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        var ui = canvasObject.AddComponent<LobbyBrowserUI>();
        ui.controller = controller;
        var backdrop = Box("Night sky", canvasObject.transform, new Color32(6, 14, 24, 255));
        Stretch(backdrop.rectTransform);
        if (background != null)
        {
            var art = Rect("Storm atmosphere", backdrop.transform).gameObject.AddComponent<RawImage>();
            Stretch(art.rectTransform);
            art.texture = background.texture;
            // Exclude the lettering in the original image; the header now has its own type.
            art.uvRect = new Rect(0, .22f, 1, .5f);
            art.color = new Color(1, 1, 1, .13f);
            art.raycastTarget = false;
        }
        var safe = Rect("Safe Area", canvasObject.transform);
        Stretch(safe);
        safe.gameObject.AddComponent<LobbySafeArea>();
        var body = Rect("Content", safe);
        Stretch(body, 48, 48, 40, 40);
        var brand = Text("Brand", body, "STORM GAMBIT  /  MULTIPLAYER", 28, Gold);
        Top(brand.rectTransform, 0, 40);
        var title = Text("Title", body, "Lobby of the Storm", 62, Ink, FontStyles.Bold);
        Top(title.rectTransform, 52, 84);
        var subtitle = Text("Subtitle", body, "Find a rival or host your own game.", 32, Muted);
        Top(subtitle.rectTransform, 146, 48);

        var surface = Box("Storm lobby surface", body, Panel);
        Stretch(surface.rectTransform, 0, 0, 220, 144);
        Border(surface.rectTransform, new Color32(133, 105, 63, 255));
        BuildIdentity(ui, surface.transform);
        var tabs = Rect("Tabs", surface.transform);
        Top(tabs, 244, 120, 32, 32);
        ui.browseTab = SimpleButton("Browse tab", tabs, "FIND A GAME");
        Anchors((RectTransform)ui.browseTab.transform, 0, 0, .5f, 1, 0, 0, -8, 0);
        ui.hostTab = SimpleButton("Host tab", tabs, "HOST A GAME");
        Anchors((RectTransform)ui.hostTab.transform, .5f, 0, 1, 1, 8, 0, 0, 0);
        SelectionLine(ui.browseTab.transform);
        SelectionLine(ui.hostTab.transform);
        SetTabSelected(ui.browseTab, true);
        SetTabSelected(ui.hostTab, false);

        ui.gamePanels = Rect("Game panels", surface.transform);
        Stretch(ui.gamePanels, 36, 36, 400, 28);
        ui.browsePanel = Rect("Browse games", ui.gamePanels).gameObject;
        Stretch((RectTransform)ui.browsePanel.transform);
        BuildBrowse(ui);
        ui.hostPanel = Rect("Create game", ui.gamePanels).gameObject;
        Stretch((RectTransform)ui.hostPanel.transform);
        BuildHost(ui);
        ui.waitingPanel = Rect("Waiting room", ui.gamePanels).gameObject;
        Stretch((RectTransform)ui.waitingPanel.transform);
        BuildWaiting(ui);
        ui.hostPanel.SetActive(false);
        ui.waitingPanel.SetActive(false);

        var status = Box("Connection feedback", surface.transform, new Color32(25, 36, 49, 255));
        Bottom(status.rectTransform, 28, 104, 36, 36);
        ui.feedbackPanel = status.gameObject;
        ui.statusText = Text("Status", status.transform, "", 32, Ink);
        Stretch(ui.statusText.rectTransform, 24, 24, 12, 12);
        ui.statusText.alignment = TextAlignmentOptions.MidlineLeft;
        ui.feedbackPanel.SetActive(false);
        ui.backButton = Button("Back to menu", body, "MAIN MENU");
        Bottom((RectTransform)ui.backButton.transform, 0, 112);
        BuildPasswordDialog(ui, canvasObject.transform);
        return ui;
    }

    private static void BuildIdentity(LobbyBrowserUI ui, Transform parent)
    {
        var identity = Rect("Player identity", parent);
        Top(identity, 24, 180, 36, 36);
        ui.identitySummary = Rect("Saved name", identity).gameObject;
        Stretch((RectTransform)ui.identitySummary.transform);
        var label = Text("Label", ui.identitySummary.transform, "PLAYING AS", 28, Gold);
        Top(label.rectTransform, 4, 40, 0, 204);
        ui.playerNameLabel = Text("Display name", ui.identitySummary.transform, "Player", 42, Ink, FontStyles.Bold);
        Top(ui.playerNameLabel.rectTransform, 58, 72, 0, 204);
        ui.playerNameLabel.overflowMode = TextOverflowModes.Ellipsis;
        ui.playerNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        ui.editNameButton = SimpleButton("Edit name", ui.identitySummary.transform, "EDIT");
        RightTop((RectTransform)ui.editNameButton.transform, 24, 120, 180);
        ui.identityEditor = Rect("Name editor", identity).gameObject;
        Stretch((RectTransform)ui.identityEditor.transform);
        var inputLabel = Text("Label", ui.identityEditor.transform, "YOUR PLAYER NAME", 28, Gold);
        Top(inputLabel.rectTransform, 0, 40);
        ui.playerNameInput = Input("Player name", ui.identityEditor.transform, "Enter your name", 24);
        Top((RectTransform)ui.playerNameInput.transform, 52, 120, 0, 196);
        ui.saveNameButton = SimpleButton("Save name", ui.identityEditor.transform, "SAVE");
        RightTop((RectTransform)ui.saveNameButton.transform, 52, 120, 176);
        ui.identitySummary.SetActive(false);
        var rule = Box("Identity separator", parent, new Color32(68, 76, 80, 255));
        Top(rule.rectTransform, 220, 1, 36, 36);
    }

    private static void BuildBrowse(LobbyBrowserUI ui)
    {
        var parent = ui.browsePanel.transform;
        var heading = Text("List title", parent, "Available games", 40, Ink, FontStyles.Bold);
        Top(heading.rectTransform, 0, 60, 0, 248);
        ui.gameCountText = Text("Game count", parent, "Looking for games...", 30, Muted);
        Top(ui.gameCountText.rectTransform, 68, 44, 0, 248);
        ui.refreshButton = SimpleButton("Refresh", parent, "REFRESH");
        RightTop((RectTransform)ui.refreshButton.transform, 0, 120, 224);
        var scrollRect = Rect("Games scroll", parent);
        Stretch(scrollRect, 0, 0, 148, 0);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        var viewport = Rect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        ui.rows = Rect("Game rows", viewport);
        ui.rows.anchorMin = new Vector2(0, 1);
        ui.rows.anchorMax = Vector2.one;
        ui.rows.pivot = new Vector2(.5f, 1);
        ui.rows.sizeDelta = Vector2.zero;
        var layout = ui.rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ui.rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = ui.rows;
        var empty = Box("Browser message", viewport, new Color32(18, 32, 47, 255));
        Top(empty.rectTransform, 16, 440);
        Border(empty.rectTransform, new Color32(66, 77, 89, 255));
        ui.emptyState = empty.gameObject;
        ui.emptyTitle = Text("Heading", empty.transform, "No games available", 42, Ink, FontStyles.Bold);
        Top(ui.emptyTitle.rectTransform, 32, 72, 28, 28);
        ui.emptyText = Text("Description", empty.transform, "Host a game and let your next rival find you.", 34, Muted);
        Top(ui.emptyText.rectTransform, 124, 116, 28, 28);
        ui.emptyHostButton = Button("Host from browser", empty.transform, "HOST A GAME");
        Bottom((RectTransform)ui.emptyHostButton.transform, 36, 120, 24, 24);
        ui.retryButton = SimpleButton("Retry", empty.transform, "TRY AGAIN");
        Bottom((RectTransform)ui.retryButton.transform, 36, 120, 28, 28);
        ui.retryButton.gameObject.SetActive(false);
        ui.rowTemplate = CreateRow(parent);
        ui.rowTemplate.gameObject.SetActive(false);
    }

    private static void BuildHost(LobbyBrowserUI ui)
    {
        var parent = ui.hostPanel.transform;
        var scrollRect = Rect("Host form scroll", parent);
        Stretch(scrollRect, 0, 0, 0, 156);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        var viewport = Rect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        var form = Rect("Host form", viewport);
        form.anchorMin = new Vector2(0, 1);
        form.anchorMax = Vector2.one;
        form.pivot = new Vector2(.5f, 1);
        form.sizeDelta = new Vector2(0, 784);
        scroll.viewport = viewport;
        scroll.content = form;
        var title = Text("Heading", form, "Host a game", 44, Ink, FontStyles.Bold);
        Top(title.rectTransform, 0, 64);
        var intro = Text("Description", form, "Your game will appear in the game list.", 32, Muted);
        Top(intro.rectTransform, 80, 88);
        var nameLabel = Text("Name label", form, "GAME NAME", 28, Gold);
        Top(nameLabel.rectTransform, 186, 40);
        ui.roomNameInput = Input("Game name", form, "Optional game name", 40);
        Top((RectTransform)ui.roomNameInput.transform, 236, 120);
        var toggleRect = Rect("Require password", form);
        Top(toggleRect, 390, 120);
        ui.passwordToggle = toggleRect.gameObject.AddComponent<Toggle>();
        var checkBackground = Box("Checkbox", toggleRect, new Color32(32, 48, 65, 255));
        checkBackground.rectTransform.anchorMin = checkBackground.rectTransform.anchorMax = new Vector2(0, .5f);
        checkBackground.rectTransform.pivot = new Vector2(0, .5f);
        checkBackground.rectTransform.sizeDelta = new Vector2(64, 64);
        checkBackground.rectTransform.anchoredPosition = Vector2.zero;
        Border(checkBackground.rectTransform, Gold);
        var check = Box("Check", checkBackground.transform, Gold);
        Stretch(check.rectTransform, 15, 15, 15, 15);
        var toggleLabel = Text("Label", toggleRect, "Require a password", 34, Ink);
        Stretch(toggleLabel.rectTransform, 92, 0, 0, 0);
        toggleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var toggleTarget = toggleRect.gameObject.AddComponent<Image>();
        toggleTarget.color = Color.clear;
        ui.passwordToggle.targetGraphic = toggleTarget;
        ui.passwordToggle.graphic = check;
        ui.passwordToggle.isOn = false;
        ui.hostPasswordInput = Input("Host password", form, "8–64 characters", 64, true);
        Top((RectTransform)ui.hostPasswordInput.transform, 532, 120);
        var note = Text("Password hint", form, "When enabled, only players with your password can join.", 30, Muted);
        Top(note.rectTransform, 680, 88);
        ui.createButton = Button("Create game", parent, "CREATE GAME");
        Bottom((RectTransform)ui.createButton.transform, 0, 120);
    }

    private static void BuildWaiting(LobbyBrowserUI ui)
    {
        var parent = ui.waitingPanel.transform;
        var label = Text("Label", parent, "WAITING ROOM", 28, Gold);
        Top(label.rectTransform, 0, 48);
        ui.waitingTitle = Text("Game name", parent, "Your game", 44, Ink, FontStyles.Bold);
        Top(ui.waitingTitle.rectTransform, 64, 120);
        ui.waitingDetails = Text("Players", parent, "Waiting for an opponent...", 34, Ink);
        Stretch(ui.waitingDetails.rectTransform, 0, 0, 216, 160);
        ui.leaveButton = SimpleButton("Leave game", parent, "LEAVE GAME");
        Bottom((RectTransform)ui.leaveButton.transform, 0, 120);
    }

    private static void BuildPasswordDialog(LobbyBrowserUI ui, Transform parent)
    {
        var overlay = Box("Password dialog", parent, new Color(0, 0, 0, .88f));
        Stretch(overlay.rectTransform);
        ui.passwordDialog = overlay.gameObject;
        var panel = Box("Dialog panel", overlay.transform, Panel);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f, .5f);
        panel.rectTransform.sizeDelta = new Vector2(940, 860);
        Border(panel.rectTransform, Gold);
        var label = Text("Label", panel.transform, "PASSWORD PROTECTED", 28, Gold);
        Top(label.rectTransform, 36, 48, 36, 36);
        ui.dialogTitle = Text("Game name", panel.transform, "Join game", 44, Ink, FontStyles.Bold);
        Top(ui.dialogTitle.rectTransform, 108, 112, 36, 36);
        ui.joinPasswordInput = Input("Join password", panel.transform, "Enter password", 64, true);
        Top((RectTransform)ui.joinPasswordInput.transform, 256, 120, 36, 36);
        ui.dialogStatus = Text("Password status", panel.transform, "Enter the host's password to join.", 32, Muted);
        Top(ui.dialogStatus.rectTransform, 404, 140, 36, 36);
        ui.passwordJoinButton = Button("Join game", panel.transform, "JOIN GAME");
        Bottom((RectTransform)ui.passwordJoinButton.transform, 172, 120, 28, 28);
        ui.passwordCancelButton = SimpleButton("Cancel", panel.transform, "CANCEL");
        Bottom((RectTransform)ui.passwordCancelButton.transform, 28, 120, 36, 36);
        overlay.gameObject.SetActive(false);
    }

    private static LobbyRoomRow CreateRow(Transform parent)
    {
        var card = Box("Game row template", parent, new Color32(21, 36, 51, 255));
        Border(card.rectTransform, new Color32(66, 77, 89, 255));
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 240;
        var row = card.gameObject.AddComponent<LobbyRoomRow>();
        row.roomName = Text("Game name", card.transform, "Game name", 38, Ink, FontStyles.Bold);
        Top(row.roomName.rectTransform, 24, 56, 24, 248);
        row.roomName.textWrappingMode = TextWrappingModes.NoWrap;
        row.roomName.overflowMode = TextOverflowModes.Ellipsis;
        row.hostName = Text("Host name", card.transform, "Hosted by Player", 32, Muted);
        Top(row.hostName.rectTransform, 94, 48, 24, 248);
        row.hostName.textWrappingMode = TextWrappingModes.NoWrap;
        row.hostName.overflowMode = TextOverflowModes.Ellipsis;
        row.access = Text("Access and players", card.transform, "OPEN  ·  1/2", 28, Gold);
        Top(row.access.rectTransform, 162, 48, 24, 248);
        row.joinButton = Button("Join", card.transform, "JOIN");
        var buttonRect = (RectTransform)row.joinButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1, .5f);
        buttonRect.pivot = new Vector2(1, .5f);
        buttonRect.sizeDelta = new Vector2(204, 120);
        buttonRect.anchoredPosition = new Vector2(-20, 0);
        return row;
    }

    private static TMP_InputField Input(string name, Transform parent, string hint, int limit, bool password = false)
    {
        var box = Box(name, parent, new Color32(7, 17, 29, 255));
        Border(box.rectTransform, new Color32(73, 89, 103, 255));
        var input = box.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("Text area", box.transform);
        Stretch(viewport, 24, 24, 12, 12);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = Text("Text", viewport, "", 36, Ink);
        Stretch(value.rectTransform);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        value.textWrappingMode = TextWrappingModes.NoWrap;
        var placeholder = Text("Placeholder", viewport, hint, 32, Muted);
        Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.targetGraphic = box;
        input.characterLimit = limit;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        return input;
    }

    private static UIButton Button(string name, Transform parent, string label)
    {
        var frame = Resources.Load<Sprite>("LobbyButtonFrame");
        var button = SimpleButton(name, parent, label);
        var image = button.GetComponent<Image>();
        if (frame != null)
        {
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3f;
            image.color = Color.white;
            foreach (var trim in image.GetComponentsInChildren<Image>())
                if (trim != image) trim.gameObject.SetActive(false);
        }
        return button;
    }

    private static UIButton SimpleButton(string name, Transform parent, string label)
    {
        var image = Box(name, parent, ButtonIdle);
        Border(image.rectTransform, new Color32(125, 104, 69, 255));
        var button = image.gameObject.AddComponent<UIButton>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1);
        colors.pressedColor = new Color(.72f, .72f, .72f, 1);
        colors.disabledColor = new Color(.5f, .5f, .5f, .7f);
        button.colors = colors;
        var text = Text("Label", image.transform, label, 34, Gold, FontStyles.Bold);
        Stretch(text.rectTransform, 20, 20, 12, 12);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28;
        text.fontSizeMax = 34;
        return button;
    }

    public static void SetTabSelected(UIButton button, bool selected)
    {
        button.GetComponent<Image>().color = selected ? ButtonActive : ButtonIdle;
        button.GetComponentInChildren<TMP_Text>().color = selected ? Gold : Muted;
        var line = button.transform.Find("Selected");
        if (line != null) line.gameObject.SetActive(selected);
    }

    private static void SelectionLine(Transform parent)
    {
        var line = Box("Selected", parent, Gold);
        Bottom(line.rectTransform, 0, 4);
        line.raycastTarget = false;
    }

    private static void Border(RectTransform parent, Color color)
    {
        var top = Box("Top trim", parent, color); Top(top.rectTransform, 0, 1);
        var bottom = Box("Bottom trim", parent, color); Bottom(bottom.rectTransform, 0, 1);
        var left = Box("Left trim", parent, color); Anchors(left.rectTransform, 0, 0, 0, 1, 0, 0, 1, 0);
        var right = Box("Right trim", parent, color); Anchors(right.rectTransform, 1, 0, 1, 1, -1, 0, 0, 0);
        top.raycastTarget = bottom.raycastTarget = left.raycastTarget = right.raycastTarget = false;
    }

    private static TMP_Text Text(string name, Transform parent, string text, int size, Color color, FontStyles style = FontStyles.Normal)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    private static Image Box(string name, Transform parent, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        => Anchors(rect, 0, 0, 1, 1, left, bottom, -right, -top);
    private static void Anchors(RectTransform rect, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
    private static void Top(RectTransform rect, float top, float height, float left = 0, float right = 0)
        => Anchors(rect, 0, 1, 1, 1, left, -top - height, -right, -top);
    private static void Bottom(RectTransform rect, float bottom, float height, float left = 0, float right = 0)
        => Anchors(rect, 0, 0, 1, 0, left, bottom, -right, bottom + height);
    private static void RightTop(RectTransform rect, float top, float height, float width)
        => Anchors(rect, 1, 1, 1, 1, -width, -top - height, 0, -top);
}
