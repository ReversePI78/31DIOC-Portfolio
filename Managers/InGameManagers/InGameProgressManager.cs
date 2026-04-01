using CsvHelper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using static UnityEngine.GraphicsBuffer;
using Unity.VisualScripting;

#if UNITY_EDITOR
using Unity.PlasticSCM.Editor.WebApi;
using static UnityEditor.Experimental.GraphView.GraphView;
#endif

public class InGameProgressManager : InGameManager
{
    InGameProgressData inGameProgressData;

    // 여기에 데이터 필드 놓지 마세요.
    // 저장되는 데이터는 inGameProgressData에 적재시켜야지 DataManager에서 저장할 때, 누락되지 않고 저장합니다.

    public override IEnumerator InitInGame()
    {
        inGameProgressData = (InGameProgressData)ManagerObj.DataManager.RuntimeData[SaveDataCategory.InGameProgress];
        if (CurrentDay == 0) // InGameProgressData의 Day가 초기 상태인 0인 경우 새로 시작한 경우이기 때문에 InitializeDayData() 해준다.
            InitializeDayData();

        //VisitingCharacter = null;
        //VisitingFacility = null;

        activityHpCost = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.ActivityHpCost)[0];

        distressCalculator = DistressCalculator.CreateDefault(); // 곤란도 계산기 생성
        scriptCategoryRoulette = new ScriptCategoryRoulette(ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.EventCategoryRouletteWeights)); // 이벤트 스크립트 룰렛 생성

        allTriggerContents = new();
        List<string> triggerSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.TriggerInfos] as List<string>;
        foreach (string triggerSource in triggerSources)
        {
            using (var reader = new StringReader(triggerSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "TriggerContent" 이름으로 접근 가능)

                while (csv.Read())
                {
                    string content = csv.GetField("TriggerContent");
                    if (!string.IsNullOrEmpty(content))
                        allTriggerContents.Add(content);
                }
            }
        }

        killTargetInfos = new();
        List<string> killTargetSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.KillTargetConditionInfos] as List<string>;
        foreach (string killTargetSource in killTargetSources)
        {
            using (var reader = new StringReader(killTargetSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "TriggerContent" 이름으로 접근 가능)

                while (csv.Read())
                {
                    string key = csv.GetField("Key");
                    string conditions = csv.GetField("Conditions");
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(conditions))
                        killTargetInfos[key] = conditions;
                }
            }
        }

        eliminationInfos = new();
        List<string> eliminationInfosSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.EliminationInfos] as List<string>;
        foreach (string eliminationInfosSource in eliminationInfosSources)
        {
            using (var reader = new StringReader(eliminationInfosSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "Key" 이름으로 접근 가능)

                while (csv.Read())
                {
                    string key = csv.GetField("Key");
                    string attacker = csv.GetField("Attacker");
                    string targets = csv.GetField("Targets");
                    string condition = csv.GetField("Condition");
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(targets) && !string.IsNullOrEmpty(attacker))
                        eliminationInfos.Add(new EliminationInfo(key, attacker, targets, condition));
                }
            }
        }

        autoEventByDayInfos = new();
        List<string> autoEventByDayInfosSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.AutoEventByDayInfos] as List<string>;
        foreach (string autoEventByDayInfosSource in autoEventByDayInfosSources)
        {
            using (var reader = new StringReader(autoEventByDayInfosSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "Key" 이름으로 접근 가능)

                while (csv.Read())
                {
                    string key = csv.GetField("Key");
                    string daysStr = csv.GetField("Days");
                    string requiredCharacters = csv.GetField("RequiredCharacters");
                    string contentsStr = csv.GetField("Contents");
                    string condition = csv.GetField("Condition");
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(daysStr) && !string.IsNullOrEmpty(contentsStr))
                        autoEventByDayInfos.Add(new AutoEventByDay(key, daysStr, requiredCharacters, contentsStr, condition));
                }
            }
        }

        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        inGameProgressData = null;

        isInActivity = false;

        scriptCategoryRoulette = null;

        yield return null;
    }

    public void InitializeDayData()
    {
        CurrentDay++;
        CurrentActivePoints = GetTotalActivePoints;
        ManagerObj.CharacterManager.InitCharacterItemExchangeInfos();
        ManagerObj.FacilityManager.InitFacilityUsageInfos();

        inGameProgressData.DialogeLogData[CurrentDay] = new(); // 로그 데이터에 새 일자 추가

        ManagerObj.MissionManager.SetupForNextDay();

        ManagerObj.DataManager.SaveData();
    }

    public void SetStartScreen()
    {
        /*if (IsNightActivity)
        {
            VisitingFacility = ManagerObj.FacilityManager.GetFacilityData(FacilityID.Room);
            ManagerObj.FacilityManager.ConfigureFacility(FacilityID.Room);
        }*/
        if(VisitingCharacter != null)
        {
            ManagerObj.CharacterManager.ConfigureCharacter(VisitingCharacter.CharacterID);
        }
        else if(VisitingFacility != null)
        {
            ManagerObj.FacilityManager.ConfigureFacility(VisitingFacility.FacilityID);
        }

        StartCoroutine(ProcessPendingMatchedScripts());

        IEnumerator ProcessPendingMatchedScripts() // inGameProgressData.MatchedScriptInfoQueue에 아직 남아있는 스크립트 정보가 있을때, 이를 처리해주는 메서드
        {
            // 1. inGameProgressData.TempItemList를 먼저 처리해준다. (스크립트 플레이 도중 인벤토리 편집창이 나왔는데 처리하지 않은 경우)
            // 2. 남아있는 inGameProgressData.MatchedScriptInfoQueue들을 순차적으로 처리해준다.
            yield return null;
            yield return new WaitUntil(() => !isDayTransitioning);

            StartCoroutine(PlayBGMByFacility());
            ManagerObj.PossessionManager.MergeTempItemList();
            yield return new WaitUntil(() => InventoryEditor.Instance == null);

            if (CurrentScriptRequest != null)
            {
                StartCoroutine(ManagerObj.ScriptManager.PlayScript(CurrentScriptRequest));
                yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);
                ShowActivityButtonPanel();
            }

            if (IsPlayingConversationTopic)
            {
                IsPlayingConversationTopic = false; 
                MatchEventScriptsWithRandom();
            }
            else
            {
                ManagerObj.ScriptManager.PlayMatchedEventScripts(); // 여기서 CurrentActivePoints == 0일 경우 ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.EnterNightActivity, ScriptCategory.Etc) 자동 실행
            }

            yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingEventScripts);

            if (IsNightActivity) // ManagerObj.ScriptManager.IsPlayingEventScripts가 false가 되자마자 IsNightActivity인지 확인.
            {
                if (!HasDoneNightActivity) { ShowActivityButtonPanel(); }
                else { StartCoroutine(SettingAfterNightActivityMatchedScripts()); } 
            }
        }
    }

    public ActivityCategory SelectedNightActivity {get; set;}
    public void NextDay(PlayerControlledScriptCategory _playerControlledScriptCategory)
    {
        StartCoroutine(NextDayCoroutine(_playerControlledScriptCategory));

        IEnumerator NextDayCoroutine(PlayerControlledScriptCategory playerControlledScriptCategory)
        {
            if (!ManagerObj.ScriptManager.IsPlayingScript) // Script 플레이 과정 중 DialogueEvent - DayEnd로 다음 날로 넘어간 것이 아닌 경우에만 실행
            {
                ManagerObj.ScriptManager.PlayScript(playerControlledScriptCategory, ScriptCategory.Etc); // 해당하는 NightActivity 실행후 진행
                yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);
                Destroy(ActivityButtonPanel.Instance != null ? ActivityButtonPanel.Instance.gameObject : null); // 만일 버튼 패널이 활성화되어 있으면 파괴
                yield return new WaitForSeconds(1f);
            }

            // 전달받은 매개변수에 따라 체력/멘탈을 회복시켜줌
            int lifeStatRecovery_StayAwake = 0;
            switch (SelectedNightActivity)
            {
                case ActivityCategory.StayAwake: lifeStatRecovery_StayAwake = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.NightActivity_LifeStatRecovery_StayAwake)[0]; break;
                case ActivityCategory.Sleep: lifeStatRecovery_StayAwake = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.NightActivity_LifeStatRecovery_Sleep)[0]; break;
                case ActivityCategory.None: Debug.LogError($"InGameProgressManager : NextDay에서 SelectedNightActivity가 None입니다."); break;
            }

            lifeStatRecovery_StayAwake += ManagerObj.PossessionManager.GetBadgeInherentEffect_Int(InherentEffectBadgeCategory.NightWatch);

            ManagerObj.StatusManager.UpdateStatus(StatusCategory.Health, lifeStatRecovery_StayAwake);
            ManagerObj.StatusManager.UpdateStatus(StatusCategory.Mental, lifeStatRecovery_StayAwake);

            Destroy(ActivityButtonPanel.Instance != null ? ActivityButtonPanel.Instance.gameObject : null); // 만일 버튼 패널이 활성화되어 있으면 파괴

            HasDoneNightActivity = true;

            ManagerObj.MissionManager.SetupForNextDay();

            StartCoroutine(ExitNightActivityAfterEventScriptsEnd());
        }

        IEnumerator ExitNightActivityAfterEventScriptsEnd()
        {
            yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript); // InitializeDayData()는 NightActivity 스크립트가 끝난 뒤 실행한다.

            InitializeDayData();

            EliminationData eliminationData = GetCurrentEliminationData();
            if(eliminationData != null)
                eliminationData = eliminationData.Targets.Contains(StatusManager.GetMeID) ? eliminationData : null; // Target이 Me 인 경우만 넣어줌.
            yield return DayTransition(eliminationData); // 여기서 "Me"가 타겟인 탈락 데이터를 넘겨준다. (없으면 null이 들어감)

            yield return new WaitForSeconds(1f);

            ManagerObj.ScriptManager.MatchEventScripts(ScriptCategory.AfterNightActivity); // 여기서 세이브함
            yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingEventScripts);

            if(ManagerObj.OptionManager.IsInGame)
                StartCoroutine(SettingAfterNightActivityMatchedScripts());
        }
    }

    IEnumerator SettingAfterNightActivityMatchedScripts()
    {
        if (ManagerObj.FacilityManager.ConfiguredFacilityID != FacilityID.Room) // 이벤트 스크립트 끝나고 room 아닐경우 room으로 변경
        {
            yield return ManagerObj.DisplayManager.GlobalFadeIn(1.5f);
            ManagerObj.FacilityManager.ConfigureFacility(FacilityID.Room);
            yield return new WaitForSeconds(2.5f);
            yield return ManagerObj.DisplayManager.GlobalFadeOut(1.5f);
        }

        StartCoroutine(ExitNightActivity());
    }


    bool isDayTransitioning;
    public Coroutine DayTransition(EliminationData eliminationData = null)
    {
        return StartCoroutine(Transition());

        IEnumerator Transition()
        {
            isDayTransitioning = true;

            while (ManagerObj.SceneFlowManager.IsSceneLoading) // 씬 로딩 중이라면 대기
                yield return null;

            float fadeDuration = 1.5f;
#if UNITY_EDITOR
            fadeDuration = 0.1f;
#endif

            if (!ManagerObj.DisplayManager.GlobalFadeImg.gameObject.activeSelf)
                yield return ManagerObj.DisplayManager.GlobalFadeIn(fadeDuration); // 만일 GlobalFadeImage가 비활성화되어있다면 활성화
            else
                yield return new WaitForSeconds(1f); // 활성화 되어있다면 1초 대기

            yield return ShowDayElapsedText();

            // eliminationData의 Target이 Me가 되어서 게임 오버된 경우
            if (eliminationData != null && eliminationData.Targets.Contains(StatusManager.GetMeID)) // 혹시 모르니 Me 판정 한 번더
            {
                yield return ManagerObj.SceneFlowManager.LoadGameOverScene(eliminationData.Key); // 씬 로드가 완료되면 자연스럽게 GlobalFadeImage도 fadeout됨
            }
            else
            {
                BookUI.Instance?.ClosedBook.SetBookText();
                yield return ManagerObj.DisplayManager.GlobalFadeOut(fadeDuration); // 만일 GlobalFadeImage가 비활성화되어있다면 활성화
            }

            isDayTransitioning = false;
        }

        IEnumerator ShowDayElapsedText()
        {
            string dayElapsedStr = ManagerObj.DataManager.GetEtcText("DaysElapsed", ManagerObj.InGameProgressManager.CurrentDay.ToString());

            TMP_Text dayElapse = ManagerObj.PrefabLoader.GetPrefab(OverlaysPrefabCategory.DaysElapsed, ManagerObj.DisplayManager.GlobalFadeImg.transform).GetComponent<TMP_Text>();
            dayElapse.text = "";
            for (int i = 0; i < dayElapsedStr.Length; i++)
            {
                dayElapse.text += dayElapsedStr[i];
                ManagerObj.SoundManager.PlaySFX("Typing");
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(1.5f);
        }
    }

    public void ShowActivityButtonPanel()
    {
        ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.ActivityButtonPanel); // ActivityButtonPanel 생성, 생성할 때 조건을 확인하고 바로 파괴하도록 구현했으니 걱정 안해도됨
    }

    public void ActiveSelectCharacter(Action<Character> selectAction)
    {
        StartCoroutine(WaitUntilSelect());

        IEnumerator WaitUntilSelect()
        {
            GameObject choicePaper = ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.ChoicePaper);
            choicePaper.GetComponent<ChoicePaper>().EnableSelectCharacter();

            Character selectedCharacter = null;
            while (true)
            {
                if (SelectCharacterLayer.ChoicePaper_SelectedCharacter != null)
                {
                    selectedCharacter = SelectCharacterLayer.ChoicePaper_SelectedCharacter;
                    break;
                }
                else if (choicePaper == null)
                {
                    yield break;
                }
                else
                {
                    yield return null;
                }
            }

            if(selectAction != null)
                selectAction(selectedCharacter);
        }
    }

    public Character VisitingCharacter
    {
        get => inGameProgressData.VisitingCharacter;
        set => inGameProgressData.VisitingCharacter = value;
    }

    public Facility VisitingFacility 
    {
        get => inGameProgressData.VisitingFacility;
        set => inGameProgressData.VisitingFacility = value;
    }
    public bool IsVisitingInteractableEntity { get; set; } // 이건 '방문중'인지를 판단하는게 아니라 '방문하고있는중'인지를 판단하는 변수

    public void VisitFacility(FacilityID facilityID)
    {
        VisitInteractableEntity(facilityID);
    }

    public void VisitCharacter(CharacterID characterID)
    {
        VisitInteractableEntity(characterID);
    }

    void VisitInteractableEntity(Enum entityEnum)
    {
        if (entityEnum is CharacterID characterID && VisitingCharacter != null && VisitingCharacter.CharacterID == characterID) // 같은 캐릭터를 방문 중이라면 방문 중단
            return;
        if (entityEnum is FacilityID facilityID && VisitingFacility != null && VisitingFacility.FacilityID == facilityID) // 같은 시설을 방문 중이라면 방문 중단
            return;

        StartCoroutine(Visit(entityEnum));

        IEnumerator Visit(Enum entityEnum)
        {
            IsVisitingInteractableEntity = true;

            yield return new WaitForSeconds(0.5f);

            float fadeDuration = 0.5f;

            yield return ManagerObj.DisplayManager.GlobalFadeIn(fadeDuration);
            ManagerObj.SoundManager.PlaySFX("Visit_Walk");

            VisitingCharacter = null;
            ManagerObj.CharacterManager.DisableCharacterObj();
            VisitingFacility = null;
            ManagerObj.FacilityManager.DisableFacilityObj();

            switch (entityEnum)
            {
                case CharacterID characterID:
                    VisitingCharacter = ManagerObj.CharacterManager.GetCharacterData(characterID);
                    ManagerObj.CharacterManager.ConfigureCharacter(characterID);
                    break;
                case FacilityID facilityID:
                    VisitingFacility = ManagerObj.FacilityManager.GetFacilityData(facilityID);
                    ManagerObj.FacilityManager.ConfigureFacility(facilityID);
                    break;
                default: Debug.LogError($"InGameProgressManager : VisitInteractableEntity에 entityEnum에 잘못된 Enum이 들어옴 entityEnum : {entityEnum.GetType().Name}"); break;
            }

            StartCoroutine(PlayBGMByFacility());

            yield return new WaitForSeconds(2f);

            yield return ManagerObj.DisplayManager.GlobalFadeOut(fadeDuration);

            yield return new WaitForSeconds(0.5f);

            BookUI.Instance?.ClosedBook.SetBookText();

            if (VisitingCharacter != null && VisitingCharacter.Reliability.ReliabilityCategory == ReliabilityCategory.Mistrust) // 불신이면 상호작용 안됨
            {
                ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.InteractionRefusal, ScriptCategory.Etc);
                yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);
                VisitingCharacter = null;
                ManagerObj.CharacterManager.DisableCharacterObj();
            }
            else
            {
                ScriptCategory[] scArray = null;
                if (VisitingFacility != null && VisitingFacility.FacilityID == FacilityID.Room)
                    scArray = new[] { ScriptCategory.BeforeNightActivity };
                else
                    scArray = new[] { ScriptCategory.MainStory, ScriptCategory.SideStory };
                ManagerObj.ScriptManager.MatchEventScripts(scArray);

                ShowActivityButtonPanel();
            }

            IsVisitingInteractableEntity = false;
        }
    }

    public IEnumerator PlayBGMByFacility()
    {
        if (!ManagerObj.OptionManager.IsInGame)
            yield break;

        int trackNum = (int)ManagerObj.FacilityManager.ConfiguredFacilityID;

        if (ManagerObj.SoundManager.CurrentBGMTrackNum == trackNum) // 같은 트랙이 실행중이라면 종료
            yield break;

        ManagerObj.SoundManager.StopBGM();

        while (IsVisitingInteractableEntity || ManagerObj.DisplayManager.GlobalFadeImg.gameObject.activeSelf)
            yield return null;

        ManagerObj.SoundManager.PlayBGM(trackNum);
    }

    bool isInActivity;
    public bool IsInActivity => isInActivity;
    int activityHpCost; // InitInGame에서 초기화해줌
    public void EnterActivity(ActivityCategory activityCategory)
    {
        isInActivity = IsPlayingConversationTopic = true;

        ConsumeActivePoints();

        switch (activityCategory)
        {
            case ActivityCategory.Talk: ManagerObj.DataManager.SaveData(); StartCoroutine(Activity_Talk()); break;
            default: break;
        }

        IEnumerator Activity_Talk()
        {
            yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);

            if (ManagerObj.CharacterManager.ConfiguredCharacterID == CharacterID.None)
                VisitingCharacter = null;
            else if (VisitingCharacter != null && VisitingCharacter.Reliability.ReliabilityCategory == ReliabilityCategory.Mistrust) // 대화 도중 불신이 되어버린 경우, visiting 해제
            {
                yield return ManagerObj.DisplayManager.GlobalFadeIn(1f);

                VisitingCharacter = null;
                ManagerObj.CharacterManager.DisableCharacterObj();

                yield return new WaitForSeconds(1f);
                yield return ManagerObj.DisplayManager.GlobalFadeOut(1f);
            }

            ExitActivity();

            ManagerObj.MissionManager.UpdateAcquireCondition(MissionGoalCategory.CountConversationTalk, 1);
        }
    }

    public void ExitActivity()
    {
        StartCoroutine(MatchEventScriptsAfterInventoryEditorDestroyed());

        IEnumerator MatchEventScriptsAfterInventoryEditorDestroyed()
        {
            yield return null;
            yield return new WaitUntil(() => !ManagerObj.InputManager.IsInventoryEditorEnabled);

            isInActivity = IsPlayingConversationTopic = false;

            MatchEventScriptsWithRandom();

            ManagerObj.MissionManager.CheckMissionData();
        }
    }

    ScriptCategoryRoulette scriptCategoryRoulette;
    DistressCalculator distressCalculator;
    public void MatchEventScriptsWithRandom()
    {
        float distress01 = distressCalculator.Compute();
        RouletteResult rouletteResult = scriptCategoryRoulette.Spin(distress01);

        Debug.Log(scriptCategoryRoulette.ShowRouletteResult(rouletteResult));

        if (ManagerObj.OptionManager.IsInGame)
        {
            ManagerObj.ScriptManager.MatchConversationTopicScripts();
            ManagerObj.ScriptManager.CheckScriptCanceled();
            ManagerObj.ScriptManager.MatchEventScripts(ScriptCategory.MainStory, ScriptCategory.SideStory);

            if(rouletteResult.Category != ScriptCategory.None) // 긍정/부정/중립 이벤트 중 하나가 나온 경우 스크립트 매치함.
                ManagerObj.ScriptManager.MatchEventScripts(rouletteResult.Category);
        }
    }

    public IEnumerator EnterNightActivity()
    {
        ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.EnterNightActivity, ScriptCategory.Etc); // NightActivity 진입
        yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);

        if (!ManagerObj.OptionManager.IsInGame)
            yield break;

        ManagerObj.InGameProgressManager.VisitFacility(FacilityID.Room);

        IsNightActivity = true;
        HasDoneNightActivity = false;
    }

    public IEnumerator ExitNightActivity()
    {
        if (!EliminationData.ContainsKey(CurrentDay))
            CheckEliminatedCharacterResult();

        ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.ExitNightActivity, ScriptCategory.Etc); // NightActivity 탈출 -> 이건 AfterNightActivity 이후 방송임
        yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);

        if (!ManagerObj.OptionManager.IsInGame)
            yield break;

        IsNightActivity = HasDoneNightActivity = false;

        // 여기에 나중에 #if Demo 붙여줄거임
        if (ManagerObj.CharacterManager.CountEliminatedCharacter() > 0)
        {
            // 데모 버전에서는 사망자가 0 이상이면 윈도우 띄우면서 게임 종료
            GameOverSetting();
            ManagerObj.DataManager.ClearRuntimeData();

            ManagerObj.DisplayManager.ShowWindow(new WindowInfo(WindowCategory.DemoEnd));
            yield break;
        }
        // #endif

        SelectedNightActivity = ActivityCategory.None;
        ManagerObj.InGameProgressManager.VisitFacility(FacilityID.Lobby);

        ManagerObj.MissionManager.CheckMissionData();

        yield return new WaitUntil(() => !ManagerObj.InGameProgressManager.IsVisitingInteractableEntity);

        StartCoroutine(DayStart());

        void CheckEliminatedCharacterResult()
        {
            CharacterManager characterManager = ManagerObj.CharacterManager;

            EliminationData[CurrentDay] = GetCurrentEliminationData(); // PlayerControlledScriptCategory.ExitNightActivity 플레이 하기 전에 탈락자 체크
            if(EliminationData[CurrentDay] != null)
            {
                if(EliminationData[CurrentDay].Attacker == StatusManager.GetMeID)
                {
                    foreach (CharacterID targetID in ParsingTargetIDs(EliminationData[CurrentDay].Targets))
                    {
                        ManagerObj.StatusManager.UpdateStatus(StatusCategory.CashReward, characterManager.GetCharacterData(targetID).CashReward);
                        characterManager.EliminateCharacter(targetID);
                    }
                }
                else
                {
                    if(!Enum.TryParse<CharacterID>(EliminationData[CurrentDay].Attacker, true, out CharacterID attackerID))
                    {
                        Debug.LogError($"InGameProgressManager : CheckEliminationResult() 에서 targetIDs 중에 CharacterID로 파싱하지 못하는 요소가 있습니다. EliminationData[CurrentDay].Attacker : {EliminationData[CurrentDay].Attacker}");
                        return;
                    }

                    foreach (CharacterID targetID in ParsingTargetIDs(EliminationData[CurrentDay].Targets))
                    {
                        characterManager.UpdateCashReward(attackerID, characterManager.GetCharacterData(targetID).CashReward);
                        characterManager.EliminateCharacter(targetID);
                    }
                }
            }

            ManagerObj.DataManager.SaveData();
        }

        List<CharacterID> ParsingTargetIDs(List<string> targetIDs)
        {
            List<CharacterID> returnValue = new();
            foreach (string targetID in EliminationData[CurrentDay].Targets)
            {
                if (Enum.TryParse<CharacterID>(targetID, true, out CharacterID characterID))
                    returnValue.Add(characterID);
                else
                    Debug.LogError($"InGameProgressManager : ParsingTargetIDs() 에서 targetIDs 중에 CharacterID로 파싱하지 못하는 요소가 있습니다. targetID : {targetID}");
            }

            return returnValue;
        }
    }

    IEnumerator DayStart()
    {
        // 여기에 PossessionEffect WhenDayStart 적용해주고, 에디터 실행되어 있다면 대기
        ManagerObj.PossessionManager.ApplyDayStartEffect();

        if (ManagerObj.StatusManager.IsLifeStatDepleted)
            ManagerObj.StatusManager.ProcessGameOverByLifeStat();
        else
        {
            ManagerObj.PossessionManager.MergeTempItemList();
            yield return null;
            yield return new WaitUntil(() => !ManagerObj.InputManager.IsInventoryEditorEnabled);

            CheckAutoEventByDay();
            ManagerObj.ScriptManager.MatchConversationTopicScripts();
            ManagerObj.ScriptManager.CheckScriptCanceled();
        }
    }

    List<AutoEventByDay> autoEventByDayInfos;
    public void CheckAutoEventByDay()
    {
        List<string> eventContents = new();

        foreach (AutoEventByDay autoEvent in autoEventByDayInfos)
        {
            if (!autoEvent.Days.Contains(CurrentDay)) // 현재 일 을 포함하고 있는지 체크
                continue;
            if (ManagerObj.CharacterManager.IsRequiredCharacterEliminated(autoEvent.RequiredCharacters)) // 플레이하는데 필수인 캐릭터가 없으면 플레이하지 않는다.
                continue;

            if (ManagerObj.ConditionDispatcher.GetDispatchedResult(autoEvent.Condition))
                eventContents.AddRange(autoEvent.Contents);
        }

        if (eventContents.Count > 0)
        {
            MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.AutoEventActivated)); // 메시지 보드 띄우기 "당신이 모르는 사이에 어떤 일이 일어났습니다."
            ManagerObj.DialogueEventDispatcher.SetupDispatch_NotSubstring1(eventContents);
        }
    }

    public void ConsumeActivePoints()
    {
        inGameProgressData.CurrentActivePoints--;
        ManagerObj.StatusManager.UpdateStatus(StatusCategory.Health, -activityHpCost);
        BookUI.Instance?.ClosedBook.SetBookText();
    }

    List<string> allTriggerContents;
    public void AddTrigger(List<string> triggerStrs)
    {
        if (triggerStrs == null) return;

        foreach (string triggerStr in triggerStrs)
        {
            if(CheckTriggerValidate(triggerStr))
                TriggerList.Add(new Trigger(triggerStr));
        }

        ManagerObj.ScriptManager.MatchConversationTopicScripts();
        ManagerObj.ScriptManager.CheckScriptCanceled();
    }

    public bool CheckTriggerValidate(string triggerContent)
    {
        if (allTriggerContents.Contains(triggerContent))
            return true;
        else
        {
            Debug.LogError($"InGameProgressManager : CheckTriggerValidate에서 트리거 목록에 없는 트리거가 요청이 들어왔습니다. triggerContent : {triggerContent}");
            return false;
        }
    }

    Dictionary<string, string> killTargetInfos;
    public bool CheckKillTargetConditionInfos(string infoKey)
    {
        if (!killTargetInfos.ContainsKey(infoKey))
        {
            Debug.LogError($"InGameProgressManager : CheckKillTargetConditionInfos에서 killTargetInfos에 infoKey가 없습니다.(false 반환) 전달받은 infoKey : {infoKey}");
            return false;
        }

        return ManagerObj.ConditionDispatcher.GetDispatchedResult(killTargetInfos[infoKey]);
    }

    List<EliminationInfo> eliminationInfos; // 앞 순서부터 체크하는 우선순위가 높은 거고, 시트도 그렇게 구성해놨음.
    public EliminationData GetCurrentEliminationData()
    {
        EliminationData returnData = null;

        foreach (EliminationInfo eliminationInfo in eliminationInfos)
        {
            if (Enum.TryParse<CharacterID>(eliminationInfo.Attacker, true, out CharacterID attackerID) // 공격자가 이미 탈락한 경우 continue
                && ManagerObj.CharacterManager.IsCharacterEliminated(attackerID))
                continue;

            List<string> targetIDs = eliminationInfo.Targets // 타겟 중에서 이미 탈락한 사람은 제외하고 새로운 리스트 만들기, 조건 맞는게 없으면 빈 리스트가 들어감 , onlyCheckMeTarget 체크해서 집어넣음
                .Where(id => id == StatusManager.GetMeID || (Enum.TryParse<CharacterID>(id, true, out CharacterID targetID) && !ManagerObj.CharacterManager.IsCharacterEliminated(targetID)))
                .ToList();
            if (targetIDs.Count == 0) // 타겟이 비어있는 경우 continue
                continue;

            if (ManagerObj.ConditionDispatcher.GetDispatchedResult(eliminationInfo.ConditionsStr))
            {
                returnData = new EliminationData(eliminationInfo.Key, eliminationInfo.Attacker, targetIDs);
                break;
            }
        }

        return returnData;
    }

    public (int currentActivePoints, int totalActivePoints) GetActivePointInfo => (ManagerObj.InGameProgressManager.CurrentActivePoints, ManagerObj.InGameProgressManager.GetTotalActivePoints);

    public static int LastDayOfGame => 31;

    public void GameOverSetting() // 세팅 외에 저장은 GameOver에서 진행해줘야함
    {
        GameOverOverlay.SetGameOverData(); // 게임 오버 오버레이에 보여줄 데이터 세팅

        ManagerObj.DataManager.SaveData(); // 데이터 저장

        ManagerObj.OptionManager.ExitInGame(); // isInGame false로 전환

        ExitActivity();
        inGameProgressData.TempItemList.Clear(); // TempItemList 클리어
        inGameProgressData.MatchedScriptInfoQueue.Clear(); // 매칭된 스크립트 모두 클리어

        Destroy(ItemEditor.Instance != null ? ItemEditor.Instance.gameObject : null);
        ManagerObj.DisplayManager.InitWindow();

        ManagerObj.DataManager.ClearRuntimeData(); // 런타임 데이터 초기화
    }

    public DifficultyCategory Difficulty
    {
        get => inGameProgressData.Difficulty;
        set => inGameProgressData.Difficulty = value;
    }

    public int CurrentDay
    {
        get => inGameProgressData.Day; 
        set => inGameProgressData.Day = value; 
    }

    public int GetTotalActivePoints => ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.ActivePoints)[0];

    public int CurrentActivePoints
    {
        get => inGameProgressData.CurrentActivePoints;
        set => inGameProgressData.CurrentActivePoints = value;
    }

    public List<Item> TempItemList
    {
        get => inGameProgressData.TempItemList;
        set => inGameProgressData.TempItemList = value;
    }

    public Dictionary<int, EliminationData> EliminationData
    {
        get => inGameProgressData.EliminationData;
        set => inGameProgressData.EliminationData = value;
    }

    public List<ScriptPlayData> GetPlayedScriptList => inGameProgressData.PlayedScriptList;
    public List<ScriptPlayData> GetCompletedScriptList => inGameProgressData.CompletedScriptList; 
    public List<ScriptPlayData> GetCanceledScriptList => inGameProgressData.CanceledScriptList;
    public ScriptRequest CurrentScriptRequest { get => inGameProgressData.CurrentScriptRequest; set => inGameProgressData.CurrentScriptRequest = value; }
    public Queue<EventScriptInfo> GetMatchedScriptInfoQueue => inGameProgressData.MatchedScriptInfoQueue;
    public bool IsNightActivity { get => inGameProgressData.IsNightActivity; set => inGameProgressData.IsNightActivity = value; }
    public bool HasDoneNightActivity { get => inGameProgressData.HasDoneNightActivity; set => inGameProgressData.HasDoneNightActivity = value; }
    public bool IsPlayingConversationTopic { get => inGameProgressData.IsPlayingConversationTopic; set => inGameProgressData.IsPlayingConversationTopic = value; }
    public List<MissionData> MissionDatas { get => inGameProgressData.MissionDatas; set => inGameProgressData.MissionDatas = value; }
    public List<string> CompletedMissions { get => inGameProgressData.CompletedMissions; set => inGameProgressData.CompletedMissions = value; }
    public List<string> CanceledMissions { get => inGameProgressData.CanceledMissions; set => inGameProgressData.CanceledMissions = value; }
    public Dictionary<int, List<ReadDialogueBlockLines>> DialogeLogData => inGameProgressData.DialogeLogData;
    public List<Trigger> TriggerList => inGameProgressData.TriggerList;
}