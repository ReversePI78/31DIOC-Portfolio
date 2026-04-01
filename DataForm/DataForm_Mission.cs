using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

public class MissionInfo
{
    string missionID;
    string categoryStr;
    string completionCondition;
    string reward;
    string penalty;

    string missionName;
    string missionDescription;

    public MissionInfo(string _missionID, string _categoryStr, string _completionCondition, string _reward, string _penalty, string _missionName, string _missionDescription)
    {
        missionID = _missionID;

        categoryStr = _categoryStr;

        completionCondition = _completionCondition;

        reward = _reward;

        penalty = _penalty;

        missionName = PlaceholderResolver.RenderWithKeys(_missionName);

        missionDescription = PlaceholderResolver.RenderWithKeys(_missionDescription);
    }

    MissionInfo() { }

    public MissionData CreateMissionData()
    {
        MissionData missionData = new();

        missionData.MissionID = missionID;

        missionData.Identifier = ManagerObj.DataManager.GetRandomKey;

        MissionCategory missionCategory = MissionCategory.Standard;
        int remainingDays = 0;
        if (categoryStr.Contains(MissionCategory.Timed.ToString()))
        {
            string[] timedData = categoryStr.Split(",");
            if (timedData.Length == 2 && int.TryParse(timedData[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out remainingDays))
            {
                missionCategory = MissionCategory.Timed;
            }
            else
            {
                missionCategory = MissionCategory.Standard;
                remainingDays = -1;
            }
        }
        else
        {
            System.Enum.TryParse<MissionCategory>(categoryStr, true, out missionCategory);
            remainingDays = -1;
        }
        missionData.Category = missionCategory;
        missionData.RemainingDays = remainingDays;

        missionData.ParseMissionAcquireCondition(completionCondition);

        if (reward.ToLower() == "randombadge" || missionData.Category == MissionCategory.Daily)
        {
            missionData.RewardBadgeID = ManagerObj.PossessionManager.GetRandomBadge(Badge_Category.Benefit).PossessionID;
        }
        else
        {
            if (ManagerObj.PossessionManager.GetBadgeInfo(reward) == null)
            {
                UnityEngine.Debug.LogError($"MissionInfo : CreateMissionData()에서 GetBadgeInfo(reward)가 null입니다. missionID : {missionID} reward : {reward}");
            }
            else
                missionData.RewardBadgeID = reward;
        }

        missionData.ParseMissionFailurePanelty(penalty);

        return missionData;
    }

    public string MissionID { get => missionID; set => missionID = value; }
    public string Category { get => categoryStr; set => categoryStr = value; }
    public string CompletionCondition { get => completionCondition; set => completionCondition = value; }
    public string MissionName { get => missionName; set => missionName = value; }
    public string MissionDescription { get => missionDescription; set => missionDescription = value; }
}

public class MissionData
{
    string missionID;
    string identifier;
    bool isCompleted;
    MissionCategory category;
    int remainingDays;
    MissionAcquireCondition missionAcquireCondition;
    string rewardBadgeID;
    MissionFailurePanelty missionFailurePanelty;

    public void ParseMissionAcquireCondition(string conditionStr) // 파싱 실패시 missionAcquireCondition = null이 들어감
    {
        string[] splitStr = conditionStr.Split(',');
        if (splitStr.Length >= 3
            && Enum.TryParse<MissionGoalCategory>(splitStr[1], true, out MissionGoalCategory missionGoalCategory) // splitStr[1]은 AcquireValueCategory 내용
            && int.TryParse(splitStr[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int standardValue))
        {
            missionAcquireCondition = new MissionAcquireCondition(missionGoalCategory, standardValue);
        }
        else
            missionAcquireCondition = null;
    }

    public void ParseMissionFailurePanelty(string dataStr) // 파싱 실패시 missionFailurePanelty = null이 들어감
    {
        if (string.IsNullOrEmpty(dataStr)) // 페널티 string이 설정되지 않은 경우
            missionFailurePanelty = null;
        else
            missionFailurePanelty = new MissionFailurePanelty(dataStr);
    }

    public void UpdateAcquireCondition(MissionGoalCategory missionGoalCategory, int value)
    {
        if (missionAcquireCondition == null)
        {
            // UnityEngine.Debug.LogError($"MissionData에서 missionAcquireCondition가 null 인데 접근하였습니다. missionID : {missionID}");
            return;
        }

        if (missionAcquireCondition.MissionGoalCategory == missionGoalCategory)
        {
            missionAcquireCondition.CurrentValue += value;
        }
    }

    public string MissionID { get => missionID; set => missionID = value; }
    public string Identifier { get => identifier; set => identifier = value; }
    public bool IsCompleted { get => isCompleted; set => isCompleted = value; }
    public string RewardBadgeID { get => rewardBadgeID; set => rewardBadgeID = value; }
    public MissionCategory Category { get => category; set => category = value; }
    public int RemainingDays { get => remainingDays; set => remainingDays = value; }
    public MissionAcquireCondition MissionAcquireCondition { get => missionAcquireCondition; set => missionAcquireCondition = value; }
    public MissionFailurePanelty MissionFailurePanelty { get => missionFailurePanelty; set => missionFailurePanelty = value; }

    public override bool Equals(object obj)
    {
        if (obj is MissionData other)
        {
            return this.missionID == other.missionID &&
                   this.identifier == other.identifier;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(missionID, identifier);
    }
}

public class MissionAcquireCondition
{
    MissionGoalCategory missionGoalCategory;
    int standardValue;
    int currentValue;

    public MissionAcquireCondition(MissionGoalCategory _missionGoalCategory, int _standardValue)
    {
        missionGoalCategory = _missionGoalCategory;
        standardValue = _standardValue;
        currentValue = 0;
    }

    public MissionAcquireCondition() { }

    public bool CheckCondition
    {
        get
        {
            if (standardValue < 0)
                return standardValue >= currentValue;
            else
                return standardValue <= currentValue;
        }
    }

    public MissionGoalCategory MissionGoalCategory { get => missionGoalCategory; set => missionGoalCategory = value; }
    public int StandardValue { get => standardValue; set => standardValue = value; }
    public int CurrentValue { get => currentValue; set => currentValue = value; }
}

public class MissionFailurePanelty
{
    MissionFailurePaneltyCategory mfpc;

    public MissionFailurePanelty(string dataStr)
    {
        Detail = "";
        Value = 0;

        if (string.IsNullOrEmpty(dataStr)) // 페널티가 설정되지 않은 경우
            return;

        string[] parsedData = dataStr.Split(",");

        if(parsedData.Length < 3)
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 생성자 도중 dataStr의 형식이 올바르지 않습니다.(parsedData의 Length가 3보다 작음) Mental,-15로 설정 dataStr : {dataStr}");
            BasicSetIfFailedParsing();
            return;
        }

        if (!Enum.TryParse<MissionFailurePaneltyCategory>(parsedData[0], true, out mfpc))
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 생성자 도중 parsedData[0]를 MissionFailurePaneltyCategory으로 파싱에 실패했습니다. Mental,-15로 설정 parsedData[0] : {parsedData[0]}");
            BasicSetIfFailedParsing();
            return;
        }

        if (mfpc == MissionFailurePaneltyCategory.Stat &&
            !((Enum.TryParse<StatusCategory>(parsedData[1], true, out StatusCategory sc) || 
            (Enum.TryParse<MissionFailurePaneltyCategory>(parsedData[1], true, out MissionFailurePaneltyCategory randomsc) && randomsc == MissionFailurePaneltyCategory.RandomStat)))
            )
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 생성자 도중 mfpc가 Stat인데 parsedData[1]를 StatusCategory로 파싱하지 못했습니다. Mental,-15로 설정 parsedData[1] : {parsedData[1]}");
            BasicSetIfFailedParsing();
            return;
        }

        if (mfpc == MissionFailurePaneltyCategory.Item && 
            !(Enum.TryParse<MissionFailurePaneltyCategory>(parsedData[1], true, out MissionFailurePaneltyCategory randomItem) && randomItem == MissionFailurePaneltyCategory.RandomItem)
            )
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 생성자 도중 mfpc가 Item인데 parsedData[1]가 RandomItem이 아닙니다. Mental,-15로 설정 parsedData[1] : {parsedData[1]}");
            BasicSetIfFailedParsing();
            return;
        }

        if (mfpc == MissionFailurePaneltyCategory.Badge && 
            !(Enum.TryParse<MissionFailurePaneltyCategory>(parsedData[1], true, out MissionFailurePaneltyCategory randomBadge) && randomBadge == MissionFailurePaneltyCategory.RandomBadge)
            )
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 생성자 도중 mfpc가 Badge인데 parsedData[1]가 RandomBadge가 아닙니다. Mental,-15로 설정 parsedData[1] : {parsedData[1]}");
            BasicSetIfFailedParsing();
            return;
        }

        if (!float.TryParse(parsedData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float _value))
        {
            UnityEngine.Debug.LogError($"MissionFailurePanelty의 parsedData[2]를 float.TryParse 하는데 실패했습니다. Mental,-15로 설정 parsedData[2] : {parsedData[2]}");
            BasicSetIfFailedParsing();
            return;
        }

        Detail = parsedData[1];
        Value = _value;

        void BasicSetIfFailedParsing()
        {
            mfpc = MissionFailurePaneltyCategory.Stat;
            Detail = StatusCategory.Mental.ToString();
            Value = -15;
        }
    }

    public MissionFailurePaneltyCategory MissionFailurePaneltyCategory { get => mfpc; set => mfpc = value; }
    public string Detail { get; set; }
    public float Value { get; set; }
}