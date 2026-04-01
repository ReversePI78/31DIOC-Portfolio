using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Cheat;
using UnityEngine.TextCore.Text;
using Unity.VisualScripting;
using System.Text.RegularExpressions;
using System.Globalization;




#if UNITY_EDITOR
using UnityEditor;
#endif

public class DialogueEventDispatcher : Dispatcher
{
    bool isPlayingEvent;
    public override void SetupDispatch(List<string> datas)
    {
        StartCoroutine(PlayEvents(datas));
    }

    public void SetupDispatch_NonSkip(List<string> datas)
    {
        StartCoroutine(PlayEvents(datas, true));
    }

    public void SetupDispatch_NotSubstring1(List<string> datas)
    {
        StartCoroutine(PlayEvents(datas, false, false));
    }

    IEnumerator PlayEvents(List<string> events, bool playOnlyNonSkip = false, bool substring1 = true)
    {
        isPlayingEvent = true;

        base.SetupDispatch(events);
        if(substring1)
            events = events.Select(e => e.Substring(1)).ToList(); // DialogueEvents들은 모두 *혹은 #로 시작하기 때문에 첫 글자를 없애준다.

        if (playOnlyNonSkip) 
            CheckNonSkipEvents(ref events);

        foreach (string eventStr in events)
        {
            var parsedStr = SplitKeyAndDetails<DispatchType_DialogueEvent>(eventStr);

            yield return Dispatch_DialogueEvent_NonSkip(parsedStr.key, parsedStr.details);
            if (!playOnlyNonSkip) 
                yield return Dispatch_DialogueEvent_Skip(parsedStr.key, parsedStr.details);
        }

        isPlayingEvent = false;

        void CheckNonSkipEvents(ref List<string> events)
        {
            List<int> removeIndex = new();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].StartsWith(DispatchType_DialogueEvent.EnableCharacter.ToString()))
                {
                    if (events[i].Split(",").Length > 2) // 캐릭터 안보이게 하는 EnableCharacter가 아닌 경우,
                        removeIndex.Add(i);
                }
            }

            for(int i = removeIndex.Count - 1; i >= 0; i--)
            {
                events.RemoveAt(removeIndex[i]);
            }
        }
    }


    protected override IEnumerator Dispatch_DialogueEvent_NonSkip(DispatchType_DialogueEvent eventID, List<string> details)
    {
        switch (eventID)
        {
            // NonSkipEvents
            case DispatchType_DialogueEvent.PlayBGM : PlayBGM(details); break;
            case DispatchType_DialogueEvent.JumpBlockLine : JumpBlockLine(details); break;
            case DispatchType_DialogueEvent.JumpRandomBlockLine : JumpRandomBlockLine(details); break;
            case DispatchType_DialogueEvent.CompleteScript : CompleteScript(); break;
            case DispatchType_DialogueEvent.EndScript : EndScript(); break;
            case DispatchType_DialogueEvent.EnableCharacter:
                if (CheckSkipEventCondition(eventID, details))
                    yield return EnableCharacter(details); 
                break;
            case DispatchType_DialogueEvent.UpdateCharacterData : UpdateCharacterData(details); break;
            case DispatchType_DialogueEvent.UpdateStatusData : UpdateStatusData(details); break;
            case DispatchType_DialogueEvent.AddTrigger : AddTrigger(details); break;
            //case DispatchType_DialogueEvent.EnterNightActivity : EnterNightActivity(); break;
            case DispatchType_DialogueEvent.NextDay : yield return NextDay(details); break;
            case DispatchType_DialogueEvent.ClearMatchedEventScripts: ClearMatchedEventScripts(); break;
            case DispatchType_DialogueEvent.ShowGameOverOveraly : ShowGameOverOveraly(details); break;
            //case DispatchType_DialogueEvent.ExitNightActivity : ExitNightActivity(); break;
            case DispatchType_DialogueEvent.AllowTrustElevation : AllowTrustElevation(details); break;
            case DispatchType_DialogueEvent.ResolvePlaceholder: ResolvePlaceholder(details); break;

            case DispatchType_DialogueEvent.ParsingFailed: Debug.LogError($"ConditionDispatcher : Dispatch_DialogueEvent_Skip에서 conditionID가 올바르지 않습니다. conditionID : {eventID.ToString()}"); break;
        }

        bool CheckSkipEventCondition(DispatchType_DialogueEvent eventID, List<string> details)
        {
            if (!MainGameSceneDialogueViewer.IsPlaySkipCoroutine) // 현재 스킵중이 아니면 true 반환
                return true;

            bool returnValue = false;

            switch (eventID)
            {
                case DispatchType_DialogueEvent.EnableCharacter:
                    if (details.Count < 2)
                    {
                        MainGameSceneDialogueViewer.configureCharacterIDAfterSkip = CharacterID.None;
                        returnValue = true;
                    }
                    break;
            }

            return returnValue;
        }
    }

    protected override IEnumerator Dispatch_DialogueEvent_Skip(DispatchType_DialogueEvent eventID, List<string> details)
    {
        switch (eventID)
        {
            // SkipableEvents
            case DispatchType_DialogueEvent.Pause : yield return Pause(details); break;
            case DispatchType_DialogueEvent.PlaySFX : PlaySFX(details); break;
            case DispatchType_DialogueEvent.ChangeScene : yield return ChangeScene(details); break;
            case DispatchType_DialogueEvent.AutoNext : AutoNext(details); break;
            case DispatchType_DialogueEvent.EnableCharacterShadow :yield return EnableCharacterShadow(details); break;
            case DispatchType_DialogueEvent.DisableCharacterShadow : yield return DisableCharacterShadow(details); break;
            case DispatchType_DialogueEvent.FadeInFacility : yield return FadeInFacility(details); break;
            case DispatchType_DialogueEvent.FadeOutFacility : yield return FadeOutFacility(details); break;
            case DispatchType_DialogueEvent.ChangeCharacterExpression : ChangeCharacterExpression(details); break;
            case DispatchType_DialogueEvent.AdjustCharacterScale : AdjustCharacterScale(details); break;
            case DispatchType_DialogueEvent.CustomScrollSpeed : CustomScrollSpeed(details); break;
            case DispatchType_DialogueEvent.DisableSpeechBubble : DisableSpeechBubble(); break;

            // 컷씬 전용
            case DispatchType_DialogueEvent.MoveCameraTo : yield return MoveCameraTo(details); break;
            case DispatchType_DialogueEvent.ShowNextCut: yield return ShowNextCut(details); break;
            case DispatchType_DialogueEvent.StartGameAfterPrologue: yield return StartGameAfterPrologue(); break;

            case DispatchType_DialogueEvent.ParsingFailed: Debug.LogError($"ConditionDispatcher : Dispatch_DialogueEvent_Skip에서 conditionID가 올바르지 않습니다. conditionID : {eventID.ToString()}"); break;
        }
    }

    IEnumerator Pause(List<string> details) {
        if (!float.TryParse(details[0], out float waitTime))
        {
            Debug.LogWarning($"[Pause] duration 파싱 실패: {details[0]}");
            waitTime = 0f;
        }

        yield return new WaitForSeconds(waitTime);
    }

    void PlayBGM(List<string> details)
    {
        int trackNum = -1; // 멈출거면 trackNum을 음수로

        if (!int.TryParse(details[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out trackNum))
        {
            Debug.LogWarning($"[PlayBGM] trackNum 파싱 실패: {details[0]}");
        }

        if (trackNum > 0) ManagerObj.SoundManager.PlayBGM(trackNum);
        else ManagerObj.SoundManager.StopBGM();
    }

    void PlaySFX(List<string> details)
    {
        if (details.Count == 0)
        {
            Debug.LogWarning($"[PlaySFX] SFXID 가 존재하지 않습니다 details 크기 : 0");
        }

        ManagerObj.SoundManager.PlaySFX(details[0]);
    }

    void JumpBlockLine(List<string> details)
    {
        if (!int.TryParse(details[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nextBlockLine))
        {
            Debug.LogWarning($"[JumpBlockLine] nextBlockLine 파싱 실패: {details[0]}");
            return;
        }

        ManagerObj.ScriptManager.NowBlockLine = nextBlockLine - 1;
    }

    void JumpRandomBlockLine(List<string> details)
    {
        if (!int.TryParse(details[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minBlockLine) || !int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxBlockLine))
        {
            Debug.LogWarning($"[JumpRandomBlockLine] minBlockLine 혹은 maxBlockLine 파싱 실패 min : {details[0]} max : {details[1]}");
            return;
        }

        ManagerObj.ScriptManager.NowBlockLine = UnityEngine.Random.Range(minBlockLine, maxBlockLine) - 1; // UnityEngine.Random.Range(int, int)는 max 값 포함
    }

    void EndScript()
    {
        ManagerObj.ScriptManager.NowBlockLine = ManagerObj.ScriptManager.ScriptData.Count;
    }

    void CompleteScript()
    {
        ScriptManager scriptManager = ManagerObj.ScriptManager;
        // 대화 완료 시 메시지 띄우는 것은 구현은 해뒀지만 좀 정신없어서 일단 주석처리한 상태, 밑 줄 주석만 제거하면 실행 가능함
        // MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.ConversationComplete, scriptManager.CurrentScriptRequest.ScriptID, scriptManager.MostRecentCategoryLabel));
        ManagerObj.ScriptManager.AddCurrentScriptOnCompletedList();
        EndScript();
    }

    IEnumerator ChangeScene(List<string> details)
    {
        if (Enum.TryParse(details[0], ignoreCase: true, out SceneCategory sceneType))
        {
            ManagerObj.SceneFlowManager.LoadScene(sceneType);

            while (ManagerObj.SceneFlowManager.IsSceneLoading)
                yield return null;

            yield return new WaitForSeconds(1f); // 씬 로딩 완료 후 1초 대기
        }
        else
        {
            Debug.LogError($"[ChangeScene] sceneType의 키 값이 잘못되었음 sceneType : {details[0]}");
            yield break;
        }

        ManagerObj.CharacterManager.ConfiguredCharacterID = CharacterID.None;

        StartCoroutine(PlayScriptAfterWait());

        IEnumerator PlayScriptAfterWait()
        {
            while (isPlayingEvent)
                yield return null;

            if (sceneType == SceneCategory.CutScene)
            {
                if (details.Count < 2 || details[1] == "")
                {
                    Debug.LogError($"[ChangeScene] CutSceneName 설정이 잘못되었음. details.Count : {details.Count}, 입력된 CutSceneName : {details[1]}");
                    yield break;
                }
                else
                {
                    yield return ((CutSceneDialogueViewer)ManagerObj.ScriptManager.GetViewer).SetViewer(details[1]);
                    //yield return ((CutSceneDialogueViewer)ManagerObj.ScriptManager.GetViewer).LoadCutSceneSprites(details[1]);
                }
            }
        }
    }

    void AutoNext(List<string> details)
    {
        if (!float.TryParse(details[0], out float autoNextDuration))
        {
            Debug.LogWarning($"[AutoNext] duration 파싱 실패: {details[0]}, autoNextDuration은 3으로 설정");
            autoNextDuration = 2.5f;
        }

        if (ManagerObj.ScriptManager.GetViewer is MainGameSceneDialogueViewer mainGameSceneDialogueViewer)
            mainGameSceneDialogueViewer.AutoNextDuration = autoNextDuration;
        else
            Debug.LogWarning($"[AutoNext] ManagerObj.ScriptManager.GetViewer가 MainGameSceneDialogueViewer가 아닙니다.");
    }

    void ChangeCharacterExpression(List<string> details)
    {
        CharacterManager characterManager = ManagerObj.CharacterManager;
        if (characterManager.GetConfiguredCharacterController is CharacterController characterController)
        {
            if (details.Count < 3)
            {
                Debug.LogWarning($"[ChangeCharacterExpression] details.Count의 크기가 3보다 작습니다.: {details.Count}");
                return;
            }

            characterController.SetExpression(new Expression(details[0], details[1], details[2]));
        }
    }

    void AdjustCharacterScale(List<string> details)
    {
        float scale = 1;
        if (!float.TryParse(details[0], out scale))
        {
            Debug.LogWarning($"[AdjustCharacterScale] scale 파싱 실패: {details[0]}, scale은 1로 설정");
        }

        ManagerObj.CharacterManager.AdjustCharacterScale(scale);
    }

    IEnumerator EnableCharacterShadow(List<string> details)
    {
        bool disableBody = false;
        if (!bool.TryParse(details[0], out disableBody))
        {
            Debug.LogWarning($"[EnableCharacterShadow] disableBody 파싱 실패: {details[0]}");
        }

        float duration = 1;
        if (!float.TryParse(details[1], out duration))
        {
            Debug.LogWarning($"[EnableCharacterShadow] duration 파싱 실패: {details[0]}");
        }

        yield return ManagerObj.CharacterManager.EnableCharacterShadow(disableBody, duration);
    }

    IEnumerator DisableCharacterShadow(List<string> details)
    {
        bool disableBody = false;
        if (!bool.TryParse(details[0], out disableBody))
        {
            Debug.LogWarning($"[DisableCharacterShadow] disableBody 파싱 실패: {details[0]}");
        }

        float duration = 1;
        if (!float.TryParse(details[1], out duration))
        {
            Debug.LogWarning($"[DisableCharacterShadow] duration 파싱 실패: {details[1]}");
        }

        yield return ManagerObj.CharacterManager.DisableCharacterShadow(disableBody, duration);
    }

    IEnumerator FadeInFacility(List<string> details)
    {
        bool onCharacter = false;
        if (!bool.TryParse(details[0], out onCharacter))
        {
            Debug.LogWarning($"[FadeInFacility] onCharacter 파싱 실패: {details[0]}");
        }

        if (!float.TryParse(details[1], out float beforeDuration))
        {
            Debug.LogWarning($"[FadeInFacility] beforeDuration 파싱 실패: {details[2]}");
            beforeDuration = 1f;
            yield break;
        }
        if (!float.TryParse(details[2], out float fadingDuration))
        {
            Debug.LogWarning($"[FadeInFacility] fadingDuration 파싱 실패: {details[2]}");
            fadingDuration = 1f;
            yield break;
        }
        if (!float.TryParse(details[3], out float afterDuration))
        {
            Debug.LogWarning($"[FadeInFacility] afterDuration 파싱 실패: {details[2]}");
            afterDuration = 1f;
            yield break;
        }

        yield return ManagerObj.FacilityManager.EnableFadeIn(onCharacter, beforeDuration, fadingDuration, afterDuration);
    }

    IEnumerator FadeOutFacility(List<string> details)
    {
        if (!float.TryParse(details[0], out float beforeDuration))
        {
            Debug.LogWarning($"[FadeOutFacility] beforeDuration 파싱 실패: {details[2]}");
            beforeDuration = 1f;
            yield break;
        }
        if (!float.TryParse(details[1], out float fadingDuration))
        {
            Debug.LogWarning($"[FadeOutFacility] fadingDuration 파싱 실패: {details[2]}");
            fadingDuration = 1f;
            yield break;
        }
        if (!float.TryParse(details[2], out float afterDuration))
        {
            Debug.LogWarning($"[FadeOutFacility] afterDuration 파싱 실패: {details[2]}");
            afterDuration = 1f;
            yield break;
        }

        yield return ManagerObj.FacilityManager.EnableFadeOut(beforeDuration, fadingDuration, afterDuration);
    }

    IEnumerator EnableCharacter(List<string> details)
    {
        if (details.Count < 2)
        {
            ManagerObj.CharacterManager.DisableCharacterObj();
            yield break;
        }

        if (!float.TryParse(details[0], out float showTimer))
        {
            Debug.LogWarning($"[EnableCharacter] showTimer 파싱 실패: {details[0]}");
            showTimer = 1.5f;
        }

        for (int i = 1; i < details.Count; i++)
        {
            ManagerObj.CharacterManager.ConfigureCharacter(details[i]);
            yield return new WaitForSeconds(showTimer);
        }
    }

    void UpdateCharacterData(List<string> details)
    {
        if (!CharacterID.TryParse(details[0], out CharacterID characterID))
        {
            Debug.LogWarning($"[UpdateCharacterData] details[0]를 CharacterID로 파싱 실패 details[0] : {details[0]}");
            return;
        }

        Character character = ManagerObj.CharacterManager.GetCharacterData(characterID);

        if (!UpdatableCharacterInfoCategory.TryParse(details[1], out UpdatableCharacterInfoCategory updatableCharacterInfoCategory))
        {
            Debug.LogWarning($"[UpdatableCharacterInfoCategory] details[1]를 CharacterID로 파싱 실패 details[1] : {details[1]}");
            return;
        }

        switch (updatableCharacterInfoCategory)
        {
            case UpdatableCharacterInfoCategory.OpenName:
                if (!character.IsNameOpened)
                {
                    ManagerObj.CharacterManager.OpenCharacterName(characterID);
                    UpdatedInfoPanel.Instance.ShowViewLine(characterID);
                }
                break;
            case UpdatableCharacterInfoCategory.Eliminate: break;
            case UpdatableCharacterInfoCategory.OpenSpecialNote:
                if(details.Count < 3)
                {
                    Debug.LogWarning($"[UpdateCharacterData] details.Count가 3보다 작습니다. details.Count : {details.Count}");
                    return;
                }
                string noteID = details[2];
                ManagerObj.CharacterManager.OpenSpecialNote(characterID, noteID);
                UpdatedInfoPanel.Instance.ShowViewLine(characterID, noteID);
                break;
            case UpdatableCharacterInfoCategory.UpdateReliability:
                if (details.Count < 3)
                {
                    Debug.LogWarning($"[UpdateCharacterData] details.Count가 3보다 작습니다. details.Count : {details.Count}");
                    return;
                }
                int reliabilityValue = 5;
                if(!int.TryParse(details[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out reliabilityValue))
                {
                    Debug.LogWarning($"[UpdateCharacterData] details[2]를 int로 변환시킬 수 없습니다. details[2] : {details[2]}");
                }
                UpdatedInfoPanel.Instance.ShowViewLine(characterID, reliabilityValue);
                ManagerObj.CharacterManager.UpdateReliability(characterID, reliabilityValue);
                break;
            case UpdatableCharacterInfoCategory.UpdateReputation:
                if (details.Count < 3)
                {
                    Debug.LogWarning($"[UpdateCharacterData] details.Count가 3보다 작습니다. details.Count : {details.Count}");
                    return;
                }
                float reputationValue = 0.5f;
                if (!float.TryParse(details[2], out reputationValue))
                {
                    Debug.LogWarning($"[UpdateCharacterData] details[2]를 float로 변환시킬 수 없습니다. details[2] : {details[2]}");
                }
                UpdatedInfoPanel.Instance.ShowViewLine(characterID, reputationValue);
                ManagerObj.CharacterManager.UpdateReputation(characterID, reputationValue);
                break;
            case UpdatableCharacterInfoCategory.AllowTrustElevation:
                if (details.Count != 2)
                {
                    Debug.LogWarning($"[AllowTrustElevation] details.Count가 2가 아닙니다. details.Count : {details.Count}");
                    return;
                }
                ManagerObj.CharacterManager.AllowTrustElevation(characterID);
                break;
        }
    }

    void AddTrigger(List<string> details)
    {
        ManagerObj.InGameProgressManager.AddTrigger(details);
    }

    void CustomScrollSpeed(List<string> details)
    {
        if (!float.TryParse(details[0], out float scrollSpeed))
        {
            Debug.LogWarning($"[CustomScrollSpeed] scrollSpeed 파싱 실패: {details[0]}, scrollSpeed은 0으로 설정"); // customScrollSpeed가 0이면 커스텀 스피드를 적용하지 않음
            scrollSpeed = 0f;
        }

        ManagerObj.ScriptManager.GetViewer.SetScrollSpeed(scrollSpeed);
    }

    void DisableSpeechBubble()
    {
        if (ManagerObj.ScriptManager.GetViewer is MainGameSceneDialogueViewer mgsViewer)
        {
            if (mgsViewer.EnabledSpeechBubble != null)
                mgsViewer.EnabledSpeechBubble.SetActive(false);
            else
                Debug.LogWarning($"[DisableSpeechBubble] EnabledSpeechBubble가 할당되지 않았습니다.");
        }
        else
            Debug.LogWarning($"[DisableSpeechBubble] ManagerObj.ScriptManager.GetViewer가 MainGameSceneDialogueViewer이 아닙니다.");
    }

    /*void EnterNightActivity()
    {
        ManagerObj.InGameProgressManager.EnterNightActivity();
    }*/

    IEnumerator MoveCameraTo(List<string> details)
    {
        if (!float.TryParse(details[0], out float targetX) || !float.TryParse(details[1], out float targetY))
        {
            Debug.LogWarning($"[MoveCameraTo] 좌표 파싱 실패: {details[0]}, {details[1]}");
            yield break;
        }

        if (!float.TryParse(details[2], out float duration))
        {
            Debug.LogWarning($"[MoveCameraTo] 이동시간 파싱 실패: {details[2]}");
            duration = 2f;
            yield break;
        }

        Vector3 startPos = Camera.main.transform.position;
        Vector3 endPos = new Vector3(targetX, targetY, startPos.z); // z 유지

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            t = Mathf.SmoothStep(0f, 1f, t); // SmoothStep 적용 (0~1 구간에서 부드럽게 가속/감속)

            Camera.main.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        Camera.main.transform.position = endPos; // 정확한 위치 보정
    }

    IEnumerator ShowNextCut(List<string> details)
    {
        CutSceneDialogueViewer cutSceneDialogueViewer = ManagerObj.ScriptManager.GetViewer as CutSceneDialogueViewer;

        if(cutSceneDialogueViewer == null)
        {
            Debug.LogWarning($"[ShowNextCut] 현재 뷰어는 CutSceneDialogueViewer가 아닙니다.");
            yield break;
        }

        float timeToChange = 2.5f;
        if (!float.TryParse(details[0], out timeToChange))
        {
            Debug.LogWarning($"[ShowNextCut] timeToChange 파싱 실패, timeToChange에는 2.5가 적재됨.: {details[0]}");
        }

        yield return cutSceneDialogueViewer.ChangeCutSprite(timeToChange);
    }

    IEnumerator StartGameAfterPrologue()
    {
        yield return ManagerObj.DisplayManager.GlobalFadeIn(2.5f);

        yield return new WaitForSeconds(1.5f);

        ManagerObj.DisplayManager.ShowWindow(new WindowInfo(WindowCategory.StartGameAfterPrologue));
    }

    IEnumerator NextDay(List<string> details)
    {
        PlayerControlledScriptCategory playerControlledScriptCategory = PlayerControlledScriptCategory.NightActivity_Sleep;
        if (!Enum.TryParse($"NightActivity_{details[0]}", true, out playerControlledScriptCategory))
        {
            Debug.LogWarning($"[NextDay] playerControlledScriptCategory 파싱 실패, playerControlledScriptCategory NightActivity_Sleep가 적재됨. details[0] : {details[0]}");
        }

        switch (playerControlledScriptCategory)
        {
            case PlayerControlledScriptCategory.NightActivity_Attack_Failure: ManagerObj.InGameProgressManager.SelectedNightActivity = ActivityCategory.Sleep; break;
            case PlayerControlledScriptCategory.NightActivity_StayAwake: ManagerObj.InGameProgressManager.SelectedNightActivity = ActivityCategory.StayAwake; break;
            case PlayerControlledScriptCategory.NightActivity_Sleep: ManagerObj.InGameProgressManager.SelectedNightActivity = ActivityCategory.Sleep; break;
        }

        yield return new WaitForSeconds(1.5f);

        ManagerObj.InGameProgressManager.NextDay(playerControlledScriptCategory);
    }

    void ClearMatchedEventScripts()
    {
        ManagerObj.InGameProgressManager.GetMatchedScriptInfoQueue.Clear();
    }

    void ShowGameOverOveraly(List<string> details)
    {
        if (details.Count != 1)
            Debug.LogWarning($"[ShowGameOverOveraly] details.Count가 1이 아닙니다.: {details.Count}");

        ManagerObj.SceneFlowManager.LoadGameOverScene(details[0]);
    }

    void AllowTrustElevation(List<string> details)
    {
        if (details.Count != 1)
            Debug.LogWarning($"[AllowTrustElevation] details.Count가 1이 아닙니다.: {details.Count}");

        CharacterID characterID = CharacterID.None;
        if (!Enum.TryParse<CharacterID>(details[0], true, out characterID))
            Debug.LogWarning($"[AllowTrustElevation] details[0]를 CharacterID로 파싱 실패 : {details[0]}");

        ManagerObj.CharacterManager.AllowTrustElevation(characterID);
    }

    void ResolvePlaceholder(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogWarning($"[ShowGameOverOveraly] details.Count가 1이 아닙니다. : {details.Count}");
            return;
        }

        if (!Enum.TryParse<ResolvePlaceholderContent>(details[0], true, out ResolvePlaceholderContent resolvePlaceholderContent))
        {
            Debug.LogWarning($"[ResolvePlaceholder] details[0]를 ResolvePlaceholderContent로 파싱 실패 : {details[0]}");
            return;
        }

        InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
        switch (resolvePlaceholderContent)
        {
            case ResolvePlaceholderContent.EliminatedCharacters:
                List<string> targetCharacterNames = new();
                foreach(string targetID in inGameProgressManager.EliminationData[inGameProgressManager.CurrentDay].Targets)
                {
                    string targetName = ManagerObj.DataManager.GetEtcText(targetID);
                    if (!string.IsNullOrEmpty(targetName))
                        targetCharacterNames.Add(targetName);
                }
                string dialogue = DialogueManager.PreResolveDialogue;
                dialogue = PlaceholderResolver.RenderWithOrder(DialogueManager.PreResolveDialogue, targetCharacterNames.ToArray());

                char[] commaCandidates = { ',', '、', '，', '﹐', '､' }; // 후보 쉼표들
                char comma = commaCandidates.FirstOrDefault(c => dialogue.IndexOf(c) >= 0); // dialogue 안에 실제로 있는 쉼표 하나 선택 (없으면 기본 ',')
                if (comma == '\0') comma = ','; // FirstOrDefault가 못 찾으면 '\0' 반환

                var list = dialogue
                    .Split(comma)
                    .Select(x => PlaceholderResolver.RemovePlaceholders(x).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                // 요소가 0~2개일 땐 그냥 처리
                if (list.Count <= 1) dialogue = list.FirstOrDefault() ?? "";
                else if (list.Count == 2) dialogue = list[0] + " " + list[1];
                // 요소가 3개 이상일 때
                else
                {
                    string sep = comma + " "; // 기존 ", " 대체
                    string prefix = string.Join(sep, list.Take(list.Count - 2)); // 마지막 두 요소를 제외하고 join
                    string lastTwo = list[list.Count - 2] + list[list.Count - 1]; // 마지막 두 요소는 " " 로만 연결(원본 유지)
                    dialogue = prefix + sep + lastTwo;
                }

                DialogueManager.PreResolveDialogue = dialogue;
                break;
            case ResolvePlaceholderContent.MyCashReward:
                DialogueManager.PreResolveDialogue = PlaceholderResolver.RenderWithOrder(DialogueManager.PreResolveDialogue, ((int)ManagerObj.StatusManager.GetStatus(StatusCategory.CashReward)).ToString());
                break;
        }
    }

    public bool IsPlayingEvent
    {
        get { return isPlayingEvent; }
    }
}
