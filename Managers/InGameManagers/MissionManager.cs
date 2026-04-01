using CsvHelper;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using System.Linq;
using System;

#if UNITY_EDITOR
using static UnityEditor.Progress;
#endif

public class MissionManager : InGameManager
{
    Dictionary<string, string> missionAddConditionInfos; // MissionID, AddCondition
    Dictionary<string, string> missionCancelConditionInfos; // MissionID, CancelCondition
    List<MissionInfo> missionInfos;

    public override IEnumerator InitInGame()
    {
        missionInfos = new();
        missionAddConditionInfos = new();
        missionCancelConditionInfos = new();
        //inGame = true;

        List<string> missionSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.MissionData] as List<string>;
        foreach (string missionSource in missionSources)
        {
            using (var reader = new StringReader(missionSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "MissionID" 이름으로 접근 가능)

                while (csv.Read())
                {
                    missionInfos.Add(new MissionInfo(
                        csv.GetField("MissionID"),
                        csv.GetField("Category"),
                        csv.GetField("CompletionCondition"),
                        csv.GetField("Reward"),
                        csv.GetField("Penalty"),

                        csv.GetField($"Name_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n"),
                        csv.GetField($"Description_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n")
                        ));

                    if (!string.IsNullOrEmpty(csv.GetField("AddCondition")))
                        missionAddConditionInfos[csv.GetField("MissionID")] = csv.GetField("AddCondition");

                    if (!string.IsNullOrEmpty(csv.GetField("CancelCondition")))
                        missionCancelConditionInfos[csv.GetField("MissionID")] = csv.GetField("CancelCondition");
                }
            }
        }

        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        missionInfos = null;
        missionAddConditionInfos = null;
        missionCancelConditionInfos = null;
        //inGame = false;

        yield return null;
    }

    /*bool inGame;
    float fallbackTimer;
    const float FALLBACK_INTERVAL = 1f;
    private void Update()
    {
        if (inGame)
        {
            fallbackTimer += Time.deltaTime;
            if (fallbackTimer >= FALLBACK_INTERVAL)
            {
                fallbackTimer = 0f; 
                CheckMissionData();
            }
        }
    }*/

    public void CheckMissionData()
    {
        CheckCompeletedMission();
        CheckFailedMission();
        CheckAddableMission();

        ManagerObj.DataManager.SaveData();
    }

    void CheckAddableMission()
    {
        foreach (string missionID in missionAddConditionInfos.Keys)
        {
            if (ManagerObj.InGameProgressManager.MissionDatas.Any(md => md.MissionID == missionID) // 이미 추가되어있는 미션 데이터거나
                || ManagerObj.InGameProgressManager.CompletedMissions.Contains(missionID) // 이미 성공했거나
                || ManagerObj.InGameProgressManager.CanceledMissions.Contains(missionID)) // 이미 실패했다면
            {
                continue;
            }

            string addCondition = missionAddConditionInfos[missionID];
            if (!string.IsNullOrEmpty(addCondition) && ManagerObj.ConditionDispatcher.GetDispatchedResult(addCondition))
            {
                MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.MissionAdded, missionID)); // 미션 추가 메시지 띄우기

                MissionInfo selectedMissionInfo = missionInfos.FirstOrDefault(mission => mission.MissionID == missionID);
                if (selectedMissionInfo == null)
                {
                    Debug.LogError($"MissionManager : AddMission에서 같은 ID를 가진 미션이 없음. missionID : {missionID}");
                    return;
                }

                ManagerObj.InGameProgressManager.MissionDatas.Add(selectedMissionInfo.CreateMissionData());
            }
        }
    }

    void CheckCompeletedMission()
    {
        List<MissionData> completedMission = new();
        foreach (MissionData md in ManagerObj.InGameProgressManager.MissionDatas)
        {
            if (md.IsCompleted) // 이미 성공한 미션은 체크하지 않음
                continue;

            bool result = false;
            if (md.MissionAcquireCondition == null)
            {
                MissionInfo mi = ManagerObj.MissionManager.GetMissionInfo(md.MissionID);

                if (mi == null)
                {
                    UnityEngine.Debug.LogError($"md.MissionID에 해당하는 MissionInfo가 존재하지 않습니다. md.MissionID : {md.MissionID}");
                    return;
                }
                else
                    if (!string.IsNullOrEmpty(mi.CompletionCondition))
                    result = ManagerObj.ConditionDispatcher.GetDispatchedResult(mi.CompletionCondition);
            }
            else
                result = md.MissionAcquireCondition.CheckCondition;

            if (result)
            {
                if (md.Category == MissionCategory.Timed)
                    md.RemainingDays = -1;

                md.IsCompleted = result;

                MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.MissionCompleted, md.MissionID)); // 미션 성공 메시지 띄우기
            }
        }
    }

    void CheckFailedMission()
    {
        foreach (MissionInfo mi in missionInfos)
        {
            if (!missionCancelConditionInfos.ContainsKey(mi.MissionID) // 추가 조건이 설정되어 있지 않거나
                || ManagerObj.InGameProgressManager.CompletedMissions.Contains(mi.MissionID) // 이미 성공했거나
                || ManagerObj.InGameProgressManager.CanceledMissions.Contains(mi.MissionID)) // 이미 실패했다면
                continue;

            if (ManagerObj.ConditionDispatcher.GetDispatchedResult(missionCancelConditionInfos[mi.MissionID]))
                ManagerObj.InGameProgressManager.CanceledMissions.Add(mi.MissionID);
        }

        List<MissionData> failedMission = new();
        foreach (MissionData md in ManagerObj.InGameProgressManager.MissionDatas)
        {
            if (md.RemainingDays == 0)
                failedMission.Add(md);
            else if (ManagerObj.InGameProgressManager.CanceledMissions.Contains(md.MissionID))
                failedMission.Add(md);
        }

        for (int i = failedMission.Count - 1; i >= 0; i--)
        {
            MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.MissionFailed, failedMission[i].MissionID));// 실패 메시지 띄우기

            RemoveMissionData(failedMission[i], true); // 여기서 세이브
        }
    }

    public void RemoveMissionData(MissionData missionData, bool isFailed)
    {
        ManagerObj.InGameProgressManager.MissionDatas.Remove(missionData);

        if (isFailed)
        {
            if(missionData.MissionFailurePanelty is MissionFailurePanelty mfp)
            {
                switch (mfp.MissionFailurePaneltyCategory)
                {
                    case MissionFailurePaneltyCategory.Stat:
                        StatusCategory sc = StatusCategory.Mental;
                        if (Enum.TryParse<StatusCategory>(mfp.Detail, true, out sc))
                            ManagerObj.StatusManager.UpdateStatus(sc, mfp.Value);
                        else if (Enum.TryParse<MissionFailurePaneltyCategory>(mfp.Detail, true, out MissionFailurePaneltyCategory rsc) && rsc == MissionFailurePaneltyCategory.RandomStat)
                            sc = GetRandomCategory<StatusCategory>();
                        ManagerObj.StatusManager.UpdateStatus(sc, mfp.Value);
                        break;
                    case MissionFailurePaneltyCategory.Item:
                        if (Enum.TryParse<MissionFailurePaneltyCategory>(mfp.Detail, true, out MissionFailurePaneltyCategory ri) && ri == MissionFailurePaneltyCategory.RandomItem)
                            ManagerObj.PossessionManager.RemoveRandomPossessioinsOnStorage<Item>((int)mfp.Value);
                        break;
                    case MissionFailurePaneltyCategory.Badge:
                        if (Enum.TryParse<MissionFailurePaneltyCategory>(mfp.Detail, true, out MissionFailurePaneltyCategory rb) && rb == MissionFailurePaneltyCategory.RandomBadge)
                            ManagerObj.PossessionManager.RemoveRandomPossessioinsOnStorage<Badge>((int)mfp.Value);
                        break;
                }
            }

            AddInGameDataIfNotDaily(missionData.Category, missionData.MissionID, isFailed);
        }
        else
            AddInGameDataIfNotDaily(missionData.Category, missionData.MissionID, isFailed);

        ManagerObj.DataManager.SaveData();

        T GetRandomCategory<T>() where T : System.Enum
        {
            T[] values = (T[])System.Enum.GetValues(typeof(T));
            return values[UnityEngine.Random.Range(0, values.Length)];
        }

        void AddInGameDataIfNotDaily(MissionCategory mc, string missionID, bool isFailed)
        {
            if (mc == MissionCategory.Daily) 
                return;

            if(isFailed)
                ManagerObj.InGameProgressManager.CanceledMissions.Add(missionID);
            else
                ManagerObj.InGameProgressManager.CompletedMissions.Add(missionID);
        }
    }

    public void UpdateAcquireCondition(MissionGoalCategory mgc, int value)
    {
        foreach (MissionData md in ManagerObj.InGameProgressManager.MissionDatas)
        {
            md.UpdateAcquireCondition(mgc, value);
        }

        CheckMissionData();
    }

    public void UpdateAcquireCondition(StatusCategory sc, int value)
    {
        string key = sc.ToString();

        MissionGoalCategory mgc = MissionGoalCategory.None;
        foreach (MissionGoalCategory category in Enum.GetValues(typeof(MissionGoalCategory)))
        {
            if (category.ToString().Contains(key))
            {
                mgc = category;
                break;
            }
        }
        if (mgc == MissionGoalCategory.None)
            return;

        foreach (MissionData md in ManagerObj.InGameProgressManager.MissionDatas)
        {
            md.UpdateAcquireCondition(mgc, value);
        }

        CheckMissionData();
    }

    readonly Dictionary<MissionGoalCategory, GameBalanceKeyCategory> mgcBalanceDic = new Dictionary<MissionGoalCategory, GameBalanceKeyCategory>
    {
        {MissionGoalCategory.GetStat_Health, GameBalanceKeyCategory.RandomMission_GetStat_Life },
        {MissionGoalCategory.GetStat_Mental, GameBalanceKeyCategory.RandomMission_GetStat_Life },

        {MissionGoalCategory.GetStat_Strength, GameBalanceKeyCategory.RandomMission_GetStat_Ability },
        {MissionGoalCategory.GetStat_Charisma, GameBalanceKeyCategory.RandomMission_GetStat_Ability },
        {MissionGoalCategory.GetStat_Stress, GameBalanceKeyCategory.RandomMission_GetStat_Ability },

        {MissionGoalCategory.GetReliability, GameBalanceKeyCategory.RandomMission_GetReliability },

        {MissionGoalCategory.CountFacilityUsage, GameBalanceKeyCategory.RandomMission_CountFacilityUsage },

        {MissionGoalCategory.CountItemAcquisitions, GameBalanceKeyCategory.RandomMission_CountItemAcquisitions },

        {MissionGoalCategory.CountConversationTalk, GameBalanceKeyCategory.RandomMission_CountConversationTalk },

        {MissionGoalCategory.CountExchangeItems, GameBalanceKeyCategory.RandomMission_CountExchangeItems }
    }; 
    public MissionAcquireCondition GetRandomMissionAcquireCondition
    {
        get
        {
            List<MissionGoalCategory> mgcValues = Enum.GetValues(typeof(MissionGoalCategory))
                     .Cast<MissionGoalCategory>()
                     .Where(v => v != MissionGoalCategory.None)
                     .ToList();

            MissionGoalCategory randomCategory = MissionGoalCategory.None;
            while (randomCategory == MissionGoalCategory.None)
            {
                randomCategory = mgcValues[UnityEngine.Random.Range(0, mgcValues.Count)];
                if (!mgcBalanceDic.ContainsKey(randomCategory))
                {
                    // Debug.LogError($"MissionManager : GetRandomMissionAcquireCondition에서 mgcBalanceDic의 Key로 포함되지 않은 MissionAcquireConditionCategory가 존재합니다.(다른 값 적재) 누락된 MissionAcquireConditionCategory : {randomCategory}");
                    randomCategory = MissionGoalCategory.None;
                }
            }

            List<int> balanceValues = ManagerObj.DataManager.GetGameBalanceData_Int(mgcBalanceDic[randomCategory]);
            int randomStandardValue = UnityEngine.Random.Range(balanceValues[0], balanceValues[1] + 1); // 최대값 미포함이므로 +1

            if (randomCategory == MissionGoalCategory.GetStat_Stress)
                randomStandardValue = -randomStandardValue;

            return new MissionAcquireCondition(randomCategory, randomStandardValue);
        }
    }

    public void SetupForNextDay()
    {
        List<MissionData> missionDatas = ManagerObj.InGameProgressManager.MissionDatas;

        foreach (MissionData md in missionDatas)
        {
            if (md.RemainingDays > 0)
                md.RemainingDays--;
        }

        foreach(MissionData dailyMD in missionDatas.Where(m => m.Category == MissionCategory.Daily).ToList())
        {
            RemoveMissionData(dailyMD, !dailyMD.IsCompleted);
        }

        // Daily 미션 제작
        for (int i = 0; i < ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.DailyMissionNum)[0]; i++) // 밸런싱된 갯수 만큼 추가
        {
            MissionInfo dailyMissionInfo = missionInfos.FirstOrDefault(m => m.Category == MissionCategory.Daily.ToString());
            if (dailyMissionInfo == null)
            {
                Debug.LogError("MissionManager : SetupForNextDay()에서 Daily 카테고리의 미션을 찾는데 실패했습니다. (일일 미션 추가 X)");
                return;
            }

            MissionData dailyMissionData = dailyMissionInfo.CreateMissionData();
            dailyMissionData.MissionAcquireCondition = GetRandomMissionAcquireCondition;

            missionDatas.Insert(0, dailyMissionData);
        }
    }

    public string GetMissionName(string missionID)
    {
        MissionInfo mission = GetMissionInfo(missionID);
        if (mission != null) return mission.MissionName;

        Debug.LogError($"GetMissionName : missionID와 같은 이름의 Mission이 없습니다 missionID : {missionID}");
        return "None";
    }

    public string GetMissionDescription(string missionID)
    {
        MissionInfo mission = GetMissionInfo(missionID);
        if (mission != null) return mission.MissionDescription;

        Debug.LogError($"GetMissionDescription : missionID와 같은 이름의 Mission이 없습니다 missionID : {missionID}");
        return "None";
    }

    public MissionInfo GetMissionInfo(string missionID)
    {
        MissionInfo returnMission = missionInfos.FirstOrDefault(mission => mission.MissionID == missionID);

        if (returnMission == null)
            Debug.LogWarning($"MissionManager : GetMissionInfo에서 같은 ID를 가진 미션이 없음. missionID : {missionID}");

        return returnMission;
    }
}
