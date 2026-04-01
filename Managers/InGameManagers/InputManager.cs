using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    private Dictionary<KeyCode, Action> keyActions;
    private Dictionary<InputConditionCategory, Func<bool>> conditions;

    private void Awake()
    {
        keyActions = new Dictionary<KeyCode, Action>
    {
            // 여기 있는 메서드들은 모두 ManagerObj.OptionManager.IsInGame를 체크함
        { KeyCode.Escape, OnEsc },

        { KeyCode.Alpha1, () => BookControl(1) }, // ActiveOpenedBook은 
        { KeyCode.Alpha2, () => BookControl(2) },
        { KeyCode.Alpha3, () => BookControl(3) },
        { KeyCode.Alpha4, () => BookControl(4) },

        { KeyCode.LeftControl, () => BookControl(-1) }, // 책 닫기
        { KeyCode.RightControl, () => BookControl(-1) },

        { KeyCode.Tab, () =>  DialogueLogControl() }
    };

        conditions = new Dictionary<InputConditionCategory, Func<bool>>
        {
            {InputConditionCategory.IsVisitingInteractableEntity,  () => ManagerObj.InGameProgressManager.IsVisitingInteractableEntity },
            { InputConditionCategory.IsInActivity, () => ManagerObj.InGameProgressManager.IsInActivity},
            { InputConditionCategory.IsPlayingEventScripts, () => ManagerObj.ScriptManager.IsPlayingEventScripts },
            { InputConditionCategory.IsPlayingScript, () => ManagerObj.ScriptManager.IsPlayingScript },
            { InputConditionCategory.HasPendingWindows, () => ManagerObj.DisplayManager.HasPendingWindows }
        };
    }

    void Update()
    {
        if (ManagerObj.Instance != null 
            && ManagerObj.SceneFlowManager.CurrentCategory == SceneCategory.MainGameScene
            && ManagerObj.OptionManager.IsInGame) // 메인 게임 씬, 인게임 중일 경우에만 인게임 키보드를 입력받음
        {
            foreach (var kv in keyActions)
            {
                if (Input.GetKeyDown(kv.Key))
                {
                    kv.Value.Invoke();
                    break;
                }
            }
        }
    }

    public bool PlayCheat
    {
        get => Input.GetKey(KeyCode.Space) && Input.GetKeyDown(KeyCode.C);
    }

    void OnEsc()
    {
        if (!ManagerObj.OptionManager.IsInGame || BlockOpenEscMenu) return;

        if (ESCMenu.Instance == null)
        {
            ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.ESCMenu);
        }
        else 
        { 
            Destroy(ESCMenu.Instance.gameObject); // 만일 escmenu가 나와있는 상황에서 다시 esc를 누르면 오브젝트 파괴
        } 
    }

    void BookControl(int n)
    {
        if (!ManagerObj.OptionManager.IsInGame || BookUI.Instance == null || BlockOpenBook)
            return;

        if (n == -1)
            BookUI.Instance.ActiveClosedBook();
        else
        {
            BookUI.Instance.ActivePage(n); // 책 여는 조건은 ActiveOpenedBook에서 CanOnEsc으로 검사함
        }
    }

    void DialogueLogControl()
    {
        if (!ManagerObj.OptionManager.IsInGame ||DialogueLog.Instance == null)
            return;

        DialogueLog.Instance.SetBoardActive();
    }

    public bool BlockNextDialogue { get; set; }

    public bool NextDialogueInput
    {
        get => Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    public bool BlockOpenEscMenu { get; set; }

    public bool BlockOpenBook { get; set; }

    public bool BlockOpenDialogueLog { get; set; }

    public bool BlockVisit
    {
        get
        {
            if (ManagerObj.ScriptManager.IsPlayingEventScripts
                || ManagerObj.ScriptManager.IsPlayingScript
                || ManagerObj.InGameProgressManager.IsInActivity
                || ManagerObj.InGameProgressManager.IsVisitingInteractableEntity
                || IsVisitingRoom)
                return false;
            else return true;
        }
    }

    public bool IsActivityButtonPanelDestroyed
    {
        get
        {
            if ((ManagerObj.InGameProgressManager.CurrentActivePoints == 0 && !ManagerObj.InGameProgressManager.IsNightActivity)
                || (ManagerObj.InGameProgressManager.VisitingFacility == null && ManagerObj.InGameProgressManager.VisitingCharacter == null)
                || (ManagerObj.FacilityManager.ConfiguredFacilityID == FacilityID.Lobby && ManagerObj.CharacterManager.ConfiguredCharacterID == CharacterID.None)
                || conditions[InputConditionCategory.IsVisitingInteractableEntity]()
                || conditions[InputConditionCategory.IsInActivity]()
                || conditions[InputConditionCategory.IsPlayingEventScripts]()
                || !ManagerObj.OptionManager.IsInGame)
                return true;
            else return false;
        }
    }

    public bool IsActivityButtonPanelActive
    {
        get
        {
            if ((BookUI.Instance.OpenedBook != null && BookUI.Instance.OpenedBook.gameObject.activeSelf)
                || conditions[InputConditionCategory.HasPendingWindows]()
                || conditions[InputConditionCategory.IsPlayingScript]()
                || IsInventoryEditorEnabled)
                return false;
            else return true;
        }
    }

    public bool IsInventoryEditorEnabled
    {
        get
        {
            if (ManagerObj.DisplayManager.HasPendingWindows 
                || InventoryEditor.Instance != null)
                return true;
            else return false;
        }
    }

    public bool CanEditItem
    {
        get
        {
            if (ManagerObj.InGameProgressManager.IsInActivity
                || ManagerObj.ScriptManager.IsPlayingScript
                || !ManagerObj.OptionManager.IsInGame)
                return false;
            return true;
        }
    }

    public bool CanEditMissionDatas
    {
        get
        {
            if (ManagerObj.ScriptManager.IsPlayingEventScripts
                || ManagerObj.ScriptManager.IsPlayingScript
                || ManagerObj.InGameProgressManager.IsInActivity
                || ManagerObj.InGameProgressManager.IsVisitingInteractableEntity
                || (ManagerObj.InGameProgressManager.IsNightActivity && ManagerObj.InGameProgressManager.HasDoneNightActivity))
                return false;
            else
                return true;
        }
    }

    public bool EnableDayEndButton
    {
        get
        {
            if (ManagerObj.InGameProgressManager.CurrentActivePoints == 0
                || ManagerObj.InGameProgressManager.IsInActivity
                || BookUI.Instance.OpenedBook.gameObject.activeSelf
                || ManagerObj.DisplayManager.HasPendingWindows
                || ManagerObj.ScriptManager.IsPlayingScript
                || ManagerObj.ScriptManager.IsPlayingEventScripts
                || ManagerObj.InGameProgressManager.IsVisitingInteractableEntity
                || IsVisitingRoom)
                return false;
            else return true;
        }
    }

    public bool EnableSetLifeStatGaugePanel
    {
        get
        {
            if (BookUI.Instance.OpenedBook.gameObject.activeSelf
                || (ActivityButtonPanel.Instance != null && ActivityButtonPanel.Instance.ButtonLayer.activeSelf))
                return false;
            else return true;
        }
    }

    bool IsVisitingRoom
    {
        get
        {
            return ManagerObj.InGameProgressManager.VisitingFacility != null && ManagerObj.InGameProgressManager.VisitingFacility.FacilityID == FacilityID.Room;
        }
    }

    public void DeactivateChildButtons(GameObject obj)
    {
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
        {
            Button button = child.GetComponent<Button>();
            if (button != null)
                button.interactable = false;
        }
    }
}
