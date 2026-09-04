using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 유닛 및 부대 선택 시 우측 하단 패널에 커맨드 버튼(이동, 어택, 정지 등)을 동적으로 생성하고 핫키를 표시하는 매니저입니다.
/// </summary>
public class CommandUIManager : MonoBehaviour
{
    private static CommandUIManager _instance;
    public static CommandUIManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<CommandUIManager>();
            return _instance;
        }
    }

    [Header("UI References")]
    public Transform buttonContainer;
    public GameObject commandButtonPrefab; // Prefab with Button, Image(Icon), Text/TMPro(Hotkey)

    private List<GameObject> activeButtons = new List<GameObject>();

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (buttonContainer == null)
        {
            Transform panel = transform.Find("CommandPannel") ?? transform.Find("ButtonContainer");
            if (panel != null) buttonContainer = panel;
            else buttonContainer = transform;
        }
    }

    public void ShowCommands(List<CommandButtonData> commands)
    {
        ClearButtons();

        if (commands == null || commands.Count == 0)
            return;

        if (buttonContainer == null) buttonContainer = transform;
        if (commandButtonPrefab == null)
        {
            Debug.LogWarning("[CommandUIManager] commandButtonPrefab이 할당되지 않았습니다!");
            return;
        }

        foreach (var cmd in commands)
        {
            GameObject btnObj = Instantiate(commandButtonPrefab, buttonContainer);
            activeButtons.Add(btnObj);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => cmd.onClickAction?.Invoke());
            }

            // 아이콘 설정
            Transform iconTransform = btnObj.transform.Find("Icon");
            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null)
                {
                    if (cmd.icon != null)
                    {
                        iconImg.sprite = cmd.icon;
                        iconImg.enabled = true;
                    }
                    else
                    {
                        // 아이콘이 없으면 배경색만 남기거나 투명 처리
                        iconImg.enabled = false;
                    }
                }
            }

            // 핫키 및 커맨드명 텍스트 설정 (상하 2분할 텍스트 지원)
            TMPro.TextMeshProUGUI[] tmps = btnObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmps.Length >= 2)
            {
                // 상단: 핫키 뱃지 (예: [Z])
                tmps[0].text = !string.IsNullOrEmpty(cmd.hotkeyText) ? $"[{cmd.hotkeyText}]" : "";
                // 하단: 한글 명령 이름 (예: 공격)
                tmps[1].text = cmd.buttonName;
            }
            else if (tmps.Length == 1)
            {
                tmps[0].text = !string.IsNullOrEmpty(cmd.hotkeyText)
                    ? $"[{cmd.hotkeyText}]\n{cmd.buttonName}"
                    : cmd.buttonName;
            }
            else
            {
                Text legacyText = btnObj.GetComponentInChildren<Text>(true);
                if (legacyText != null)
                {
                    legacyText.text = !string.IsNullOrEmpty(cmd.hotkeyText)
                        ? $"[{cmd.hotkeyText}]\n{cmd.buttonName}"
                        : cmd.buttonName;
                }
            }
        }
    }

    public void ClearButtons()
    {
        foreach (var btn in activeButtons)
        {
            if (btn != null) Destroy(btn);
        }
        activeButtons.Clear();
    }
}
