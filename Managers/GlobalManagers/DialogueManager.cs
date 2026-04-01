using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    DialogueViewer dialogueViewer;
    DialogueBlock dialogueBlock;
    int nowDialogueLine;

    private void Awake()
    {
        dialogueViewer = GetComponent<DialogueViewer>();
    }

    public void SetDialogueBlock(DialogueBlock _dialogueBlock)
    {
        dialogueBlock = _dialogueBlock;
        nowDialogueLine = -1; // 0번째 인덱스부터 시작할 것이기 때문

        int blockID = ManagerObj.ScriptManager.ScriptData.IndexOf(dialogueBlock);
        if (blockID > -1)
        {
            ManagerObj.ScriptManager.CurrentReadDialogueBlockLines.ReadBlockLines.Add(blockID);
        }
        else Debug.LogError("DialogueManager : CheckBlockEnd()에서 현재 DialogueBlock이 ManagerObj.ScriptManager.ScriptData에 없습니다.");

        dialogueViewer.NextDialogueLine();
    }

    public bool IsEndBlock { get; set; }
    public void NextBasicLine()
    {
        nowDialogueLine++;

        if (ManagerObj.StatusManager.IsLifeStatDepleted && ManagerObj.OptionManager.IsInGame) // 대화 도중 라이프 스탯을 모두 소모한 경우
        {
            ManagerObj.ScriptManager.EndScript();
            ManagerObj.StatusManager.ProcessGameOverByLifeStat();
            return;
        }

        if (nowDialogueLine >= dialogueBlock.DialogueLines.Count)
        {
            ManagerObj.ScriptManager.SetNextDialogueBlock();
            IsEndBlock = true;
            return;
        }
    }

    public bool CheckBlockEnd() // 블록이 끝났다면 True 아니면 False 리턴
    {
        if (IsEndBlock)
        {
            IsEndBlock = false;
            return true;
        }
        else
            return false;
    }

    public static string PreResolveDialogue { get; set; }
    public IEnumerator PlayBasicLine()
    {
        DialogueLine dialogueLine = dialogueBlock.DialogueLines[nowDialogueLine];
        PreResolveDialogue = dialogueLine.Dialogue;

        List<string> preEvent = GetPreEventsOnly(dialogueLine.Controls);
        ManagerObj.ScriptManager.PlayDialogueEvent(preEvent);
        while (ManagerObj.ScriptManager.IsPlayingDialogueEvent)
        {
            yield return null;
        }

        if (!IsEventOnly(dialogueBlock.CharacterID))
        {
            dialogueViewer.ScrollDialogueOnTextArea(PlaceholderResolver.RemovePlaceholders(PreResolveDialogue));

            while (dialogueViewer.IsTextScrolling)
                yield return null;
        }

        ManagerObj.ScriptManager.PlayDialogueEvent(dialogueLine.Controls); // PreEvents를 제거했으니 자연스럽게 PostEvent만 실행
        while (ManagerObj.ScriptManager.IsPlayingDialogueEvent)
        {
            yield return null;
        }
        dialogueLine.Controls = new List<string>(); // 초기화(Skip 기능에서 중복 실행되지 않기 위해)
    }

    public List<string> GetPreEventsOnly(List<string> controls)
    {
        List<string> preEvent = controls.Where(c => c.StartsWith("*")).ToList(); // *로 시작하는 요소만 따로 리스트로 추출
        controls.RemoveAll(c => preEvent.Contains(c));// Controls에서 preEvent 요소 제거

        return preEvent;
    }

    public bool IsEventOnly(string str)
    {
        return str == "EventOnly";
    }

    public DialogueBlock GetNowBlock
    {
        get { return dialogueBlock; }
    }

    public DialogueLine GetNowLine
    {
        get { return dialogueBlock.DialogueLines[nowDialogueLine]; }
    }
}
