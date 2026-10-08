using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Button + expandable panel that shows every batch of effects waiting to be resolved.
//The whole UI is built from code on its own overlay canvas, so no scene or prefab has to reference it.
public class PendingTriggersViewer : MonoBehaviour
{
    #region Layout (in the 1920x1080 reference resolution)
    //Position of the button, measured from the middle of the left edge of the screen
    const float ButtonX = 10f;
    const float ButtonY = 0f;
    const float ButtonFaceSize = 40f;
    const int MaxButtonFaces = 10;

    const float PanelWidth = 560f;
    const float MaxPanelHeight = 520f;
    const float PanelPadding = 10f;
    const float TileFaceSize = 72f;
    const int TileColumnCount = 5;
    static readonly Vector2 TileSize = new Vector2(96f, 116f);

    const float EffectTextWidth = 440f;

    const int SortingOrder = 50;
    const float PollInterval = 0.1f;
    const int DimRefreshPollCount = 5;
    const float DimmedAlpha = 0.35f;

    static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
    static readonly Color GroupColor = new Color(1f, 1f, 1f, 0.07f);
    static readonly Color FaceBackColor = new Color(0.2f, 0.2f, 0.24f, 1f);
    static readonly Color OpponentTextColor = new Color(1f, 0.6f, 0.6f, 1f);
    #endregion

    GameObject _root;
    RectTransform _button;
    TextMeshProUGUI _countText;
    RectTransform _panel;
    RectTransform _content;
    RectTransform _effectTextWindow;
    TextMeshProUGUI _effectTitleText;
    TextMeshProUGUI _effectInfoText;
    TextMeshProUGUI _effectDescriptionText;

    bool _isExpanded = false;
    SkillInfo _shownSkillInfo = null;

    float _pollTimer = 0f;
    int _pollCount = 0;

    //Flat list of the shown effects (null between batches), to detect when the pending effects have changed
    List<SkillInfo> _signature = new List<SkillInfo>();
    List<KeyValuePair<CanvasGroup, SkillInfo>> _dimTargets = new List<KeyValuePair<CanvasGroup, SkillInfo>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameObject host = new GameObject("PendingTriggersViewer");
        DontDestroyOnLoad(host);
        host.AddComponent<PendingTriggersViewer>();
    }

    void Awake()
    {
        BuildUI();

        _root.SetActive(false);
    }

    void Update()
    {
        _pollTimer -= Time.unscaledDeltaTime;

        if (_pollTimer > 0f) return;

        _pollTimer = PollInterval;

        List<PendingTriggerGroup> groups = PendingTriggers.GetGroups();

        if (groups.Count == 0)
        {
            if (_root.activeSelf)
            {
                CloseEffectText();
                _root.SetActive(false);
            }

            _signature.Clear();

            //Don't carry the opened panel over to the next game
            if (GManager.instance == null)
            {
                _isExpanded = false;
            }

            return;
        }

        if (!_root.activeSelf)
        {
            _root.SetActive(true);
        }

        List<SkillInfo> signature = GetSignature(groups);

        if (!IsSameSignature(signature))
        {
            _signature = signature;
            _pollCount = 0;

            Rebuild(groups);
        }

        else
        {
            _pollCount++;

            if (_pollCount >= DimRefreshPollCount)
            {
                _pollCount = 0;

                RefreshDimming();
            }
        }
    }

    #region Detect changes
    static List<SkillInfo> GetSignature(List<PendingTriggerGroup> groups)
    {
        List<SkillInfo> signature = new List<SkillInfo>();

        foreach (PendingTriggerGroup group in groups)
        {
            signature.AddRange(group.SkillInfos);
            signature.Add(null);
        }

        return signature;
    }

    bool IsSameSignature(List<SkillInfo> signature)
    {
        if (signature.Count != _signature.Count) return false;

        for (int i = 0; i < signature.Count; i++)
        {
            if (signature[i] != _signature[i]) return false;
        }

        return true;
    }
    #endregion

    #region Show the current batches
    void Rebuild(List<PendingTriggerGroup> groups)
    {
        _dimTargets.Clear();

        //The count text is the first child of the button and is kept
        ClearChildren(_button, keepCount: 1);
        ClearChildren(_content, keepCount: 0);

        int totalCount = 0;
        int buttonFaceCount = 0;

        for (int i = 0; i < groups.Count; i++)
        {
            PendingTriggerGroup group = groups[i];
            totalCount += group.SkillInfos.Count;

            #region Tiny faces on the button
            if (i >= 1 && buttonFaceCount < MaxButtonFaces)
            {
                RectTransform separator = NewRect("Separator", _button);
                AddImage(separator, new Color(1f, 1f, 1f, 0.5f));
                SetPreferredSize(separator, new Vector2(2f, ButtonFaceSize));
            }

            foreach (SkillInfo skillInfo in group.SkillInfos)
            {
                if (buttonFaceCount >= MaxButtonFaces) break;

                RectTransform face = CreateFace(_button, ButtonFaceSize, skillInfo.CardEffect.EffectSourceCard);
                SetPreferredSize(face, face.sizeDelta);
                AddDimTarget(face, skillInfo);

                buttonFaceCount++;
            }
            #endregion

            #region Batch in the panel
            RectTransform groupRect = NewRect("Batch", _content);
            AddImage(groupRect, GroupColor);
            AddVerticalLayout(groupRect, padding: 8, spacing: 6f, fitHeight: false);

            TextMeshProUGUI header = NewText("Header", groupRect, 20f, TextAlignmentOptions.Left);
            header.fontStyle = FontStyles.Bold;
            header.text = $"{group.Label} ({group.SkillInfos.Count})";

            RectTransform grid = NewRect("Effects", groupRect);
            GridLayoutGroup gridLayoutGroup = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayoutGroup.cellSize = TileSize;
            gridLayoutGroup.spacing = new Vector2(6f, 6f);
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = TileColumnCount;

            foreach (SkillInfo skillInfo in group.SkillInfos)
            {
                CreateTile(grid, skillInfo);
            }
            #endregion
        }

        if (totalCount > buttonFaceCount)
        {
            TextMeshProUGUI moreText = NewText("More", _button, 22f, TextAlignmentOptions.Center);
            moreText.text = $"+{totalCount - buttonFaceCount}";
        }

        _countText.text = $"Triggers\n<size=26>{totalCount}</size>";

        RefreshDimming();

        if (_shownSkillInfo != null && !_signature.Contains(_shownSkillInfo))
        {
            CloseEffectText();
        }

        _panel.gameObject.SetActive(_isExpanded);

        ResizePanel();
    }

    void CreateTile(RectTransform parent, SkillInfo skillInfo)
    {
        CardSource card = skillInfo.CardEffect.EffectSourceCard;

        RectTransform tile = NewRect("Effect", parent);

        //Transparent graphic so that the whole tile can be clicked
        AddImage(tile, Color.clear).raycastTarget = true;

        tile.gameObject.AddComponent<PendingTriggerTile>().SetUp(skillInfo, ShowEffectText);

        RectTransform face = CreateFace(tile, TileFaceSize, card);
        face.anchorMin = new Vector2(0.5f, 1f);
        face.anchorMax = new Vector2(0.5f, 1f);
        face.pivot = new Vector2(0.5f, 1f);
        face.anchoredPosition = Vector2.zero;

        TextMeshProUGUI nameText = NewText("EffectName", tile, 13f, TextAlignmentOptions.Top);
        nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
        nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameText.rectTransform.anchoredPosition = Vector2.zero;
        nameText.rectTransform.sizeDelta = new Vector2(0f, TileSize.y - face.sizeDelta.y - 4f);
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.richText = false;
        nameText.text = GetEffectName(skillInfo);

        if (PendingTriggers.IsOpponentEffect(skillInfo))
        {
            nameText.color = OpponentTextColor;
        }

        AddDimTarget(tile, skillInfo);
    }

    //Small square image of the card's face, cropped the same way as the digivolution cards in CardInfo.prefab
    //(a 100x100 mask over a 200x280 card image placed at y = -52.8)
    RectTransform CreateFace(RectTransform parent, float size, CardSource card)
    {
        RectTransform outline = NewRect("Face", parent);
        outline.sizeDelta = new Vector2(size + 4f, size + 4f);
        AddImage(outline, GetCardColor(card));

        RectTransform mask = NewRect("Mask", outline);
        mask.sizeDelta = new Vector2(size, size);
        AddImage(mask, FaceBackColor);
        mask.gameObject.AddComponent<RectMask2D>();

        RectTransform cardImageRect = NewRect("CardImage", mask);
        cardImageRect.sizeDelta = new Vector2(size * 2f, size * 2.8f);
        cardImageRect.anchoredPosition = new Vector2(0f, size * -0.528f);

        Image cardImage = AddImage(cardImageRect, Color.clear);

        SetCardSprite(cardImage, card);

        return outline;
    }

    async void SetCardSprite(Image cardImage, CardSource card)
    {
        try
        {
            Sprite sprite = await card.GetCardSprite();

            //The image may have been destroyed while the sprite was loading
            if (cardImage == null) return;
            if (sprite == null) return;

            cardImage.sprite = sprite;
            cardImage.color = Color.white;
        }

        catch (System.Exception e)
        {
            Debug.LogWarning($"PendingTriggersViewer: failed to load the card image. {e.Message}");
        }
    }

    void AddDimTarget(RectTransform rectTransform, SkillInfo skillInfo)
    {
        CanvasGroup canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

        _dimTargets.Add(new KeyValuePair<CanvasGroup, SkillInfo>(canvasGroup, skillInfo));
    }

    //Effects that could not be activated right now are dimmed
    void RefreshDimming()
    {
        Dictionary<SkillInfo, bool> canActivate = new Dictionary<SkillInfo, bool>();

        foreach (KeyValuePair<CanvasGroup, SkillInfo> dimTarget in _dimTargets)
        {
            if (dimTarget.Key == null) continue;

            if (!canActivate.ContainsKey(dimTarget.Value))
            {
                canActivate[dimTarget.Value] = PendingTriggers.CanActivate(dimTarget.Value);
            }

            dimTarget.Key.alpha = canActivate[dimTarget.Value] ? 1f : DimmedAlpha;
        }
    }

    void ResizePanel()
    {
        if (!_panel.gameObject.activeInHierarchy) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        float height = Mathf.Min(LayoutUtility.GetPreferredHeight(_content) + PanelPadding * 2f, MaxPanelHeight);

        _panel.sizeDelta = new Vector2(PanelWidth, height);
    }

    static void ClearChildren(RectTransform parent, int keepCount)
    {
        for (int i = parent.childCount - 1; i >= keepCount; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

            //Destroy is delayed to the end of the frame, so hide the child from the layout groups right away
            child.SetActive(false);
            Destroy(child);
        }
    }
    #endregion

    #region Open / close
    void OnClickButton()
    {
        _isExpanded = !_isExpanded;

        if (GManager.instance != null)
        {
            if (_isExpanded)
            {
                GManager.instance.PlayDecisionSE();
            }

            else
            {
                GManager.instance.PlayCancelSE();
            }
        }

        if (!_isExpanded)
        {
            CloseEffectText();
        }

        _panel.gameObject.SetActive(_isExpanded);

        ResizePanel();
    }

    void ShowEffectText(SkillInfo skillInfo)
    {
        if (skillInfo == null) return;
        if (skillInfo.CardEffect == null) return;
        if (skillInfo.CardEffect.EffectSourceCard == null) return;

        //Right click on the shown effect again to close
        if (_shownSkillInfo == skillInfo)
        {
            CloseEffectText();
            return;
        }

        _shownSkillInfo = skillInfo;

        ICardEffect cardEffect = skillInfo.CardEffect;
        CardSource card = cardEffect.EffectSourceCard;

        List<string> infos = new List<string>();
        infos.Add(PendingTriggers.IsOpponentEffect(skillInfo) ? "Opponent's effect" : "Your effect");

        if (cardEffect.IsInheritedEffect) infos.Add("Inherited");
        if (cardEffect.IsLinkedEffect) infos.Add("Link");
        if (!PendingTriggers.CanActivate(skillInfo)) infos.Add("Can't activate now");

        _effectTitleText.text = $"{card.BaseENGCardNameFromEntity} ({card.CardID})";
        _effectInfoText.text = $"{GetEffectName(skillInfo)}\n{string.Join(" / ", infos)}";
        _effectDescriptionText.text = string.IsNullOrEmpty(cardEffect.EffectDiscription)
            ? "(No effect text)"
            : DataBase.ReplaceToASCII(cardEffect.EffectDiscription);

        _effectTextWindow.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_effectTextWindow);

        if (GManager.instance != null)
        {
            GManager.instance.PlayDecisionSE();
        }
    }

    void CloseEffectText()
    {
        _shownSkillInfo = null;

        _effectTextWindow.gameObject.SetActive(false);
    }
    #endregion

    #region Build the UI
    void BuildUI()
    {
        _root = new GameObject("Canvas");
        _root.transform.SetParent(transform, false);
        _root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler canvasScaler = _root.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0f;

        _root.AddComponent<GraphicRaycaster>();

        #region Button
        _button = NewRect("Button", _root.transform);
        SetLeftMiddleAnchor(_button, new Vector2(0f, 0.5f), new Vector2(ButtonX, ButtonY));

        Image buttonImage = AddImage(_button, PanelColor);
        buttonImage.raycastTarget = true;

        Button button = _button.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(OnClickButton);

        HorizontalLayoutGroup buttonLayoutGroup = _button.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayoutGroup.padding = new RectOffset(10, 10, 6, 6);
        buttonLayoutGroup.spacing = 5f;
        buttonLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        buttonLayoutGroup.childControlWidth = true;
        buttonLayoutGroup.childControlHeight = true;
        buttonLayoutGroup.childForceExpandWidth = false;
        buttonLayoutGroup.childForceExpandHeight = false;

        ContentSizeFitter buttonSizeFitter = _button.gameObject.AddComponent<ContentSizeFitter>();
        buttonSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        buttonSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _countText = NewText("Count", _button, 15f, TextAlignmentOptions.Center);
        _countText.enableWordWrapping = false;
        #endregion

        #region Panel
        float panelY = ButtonY - 40f;

        _panel = NewRect("Panel", _root.transform);
        SetLeftMiddleAnchor(_panel, new Vector2(0f, 1f), new Vector2(ButtonX, panelY));
        _panel.sizeDelta = new Vector2(PanelWidth, MaxPanelHeight);
        AddImage(_panel, PanelColor).raycastTarget = true;

        RectTransform viewport = NewRect("Viewport", _panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(PanelPadding, PanelPadding);
        viewport.offsetMax = new Vector2(-PanelPadding, -PanelPadding);
        viewport.gameObject.AddComponent<RectMask2D>();

        _content = NewRect("Content", viewport);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.sizeDelta = Vector2.zero;
        AddVerticalLayout(_content, padding: 0, spacing: 8f, fitHeight: true);

        ScrollRect scrollRect = _panel.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = _content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        _panel.gameObject.SetActive(false);
        #endregion

        #region Effect text window
        _effectTextWindow = NewRect("EffectText", _root.transform);
        SetLeftMiddleAnchor(_effectTextWindow, new Vector2(0f, 1f), new Vector2(ButtonX + PanelWidth + 8f, panelY));
        _effectTextWindow.sizeDelta = new Vector2(EffectTextWidth, 0f);

        Image effectTextImage = AddImage(_effectTextWindow, PanelColor);
        effectTextImage.raycastTarget = true;

        Button effectTextButton = _effectTextWindow.gameObject.AddComponent<Button>();
        effectTextButton.targetGraphic = effectTextImage;
        effectTextButton.transition = Selectable.Transition.None;
        effectTextButton.onClick.AddListener(CloseEffectText);

        AddVerticalLayout(_effectTextWindow, padding: 14, spacing: 8f, fitHeight: true);

        _effectTitleText = NewText("Title", _effectTextWindow, 24f, TextAlignmentOptions.Left);
        _effectTitleText.fontStyle = FontStyles.Bold;
        _effectTitleText.richText = false;

        _effectInfoText = NewText("Info", _effectTextWindow, 17f, TextAlignmentOptions.Left);
        _effectInfoText.color = new Color(0.75f, 0.85f, 1f, 1f);
        _effectInfoText.richText = false;

        _effectDescriptionText = NewText("Description", _effectTextWindow, 20f, TextAlignmentOptions.Left);
        _effectDescriptionText.richText = false;

        _effectTextWindow.gameObject.SetActive(false);
        #endregion
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);

        return gameObject.GetComponent<RectTransform>();
    }

    static Image AddImage(RectTransform rectTransform, Color color)
    {
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
    }

    //Vertical list. fitHeight is only for a list that is not sized by a parent layout group
    static void AddVerticalLayout(RectTransform rectTransform, int padding, float spacing, bool fitHeight)
    {
        VerticalLayoutGroup verticalLayoutGroup = rectTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayoutGroup.padding = new RectOffset(padding, padding, padding, padding);
        verticalLayoutGroup.spacing = spacing;
        verticalLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        verticalLayoutGroup.childControlWidth = true;
        verticalLayoutGroup.childControlHeight = true;
        verticalLayoutGroup.childForceExpandWidth = true;
        verticalLayoutGroup.childForceExpandHeight = false;

        if (!fitHeight) return;

        ContentSizeFitter contentSizeFitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void SetPreferredSize(RectTransform rectTransform, Vector2 size)
    {
        LayoutElement layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = size.x;
        layoutElement.preferredHeight = size.y;
    }

    static void SetLeftMiddleAnchor(RectTransform rectTransform, Vector2 pivot, Vector2 position)
    {
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = position;
    }

    static Color GetCardColor(CardSource card)
    {
        List<CardColor> cardColors = card.BaseCardColorsFromEntity;

        if (cardColors.Count >= 1 && DataBase.CardColor_ColorDarkDictionary.TryGetValue(cardColors[0], out Color color))
        {
            return color;
        }

        return Color.gray;
    }

    static string GetEffectName(SkillInfo skillInfo)
    {
        return string.IsNullOrEmpty(skillInfo.CardEffect.EffectName) ? "(Effect)" : skillInfo.CardEffect.EffectName;
    }
    #endregion
}
