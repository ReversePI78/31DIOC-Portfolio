using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusManager : InGameManager
{
    Status statusData;
    public Status StatusData => statusData;

    public override IEnumerator InitInGame()
    {
        statusData = (Status)ManagerObj.DataManager.RuntimeData[SaveDataCategory.Status];

        ManagerObj.PossessionManager.Inventory = statusData.Inventory;
        ManagerObj.PossessionManager.BadgeCase = statusData.BadgeCase;

        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        statusData = null;

        yield return null;
    }

    public float GetStatus(StatusCategory statusCategory)
    {
        switch (statusCategory)
        {
            case StatusCategory.CashReward: return statusData.CashReward;   

            case StatusCategory.Health: return statusData.Health;
            case StatusCategory.Mental: return statusData.Mental;

            case StatusCategory.Strength: return statusData.Strength;
            case StatusCategory.Charisma: return statusData.Charisma;
            case StatusCategory.Reputation: return statusData.Reputation;
            case StatusCategory.Stress: return statusData.Stress;
        }

        return 0; // 실행될 일 없음
    }

    public void UpdateStatus(StatusCategory statusCategory, float value)
    {
        float maxValue = GetStatusMaxValue(statusCategory);
        switch (statusCategory)
        {
            case StatusCategory.CashReward: statusData.CashReward = (int)Mathf.Clamp(statusData.CashReward + value, 0, maxValue); break;

            case StatusCategory.Health:
            case StatusCategory.Mental:
                if(statusCategory == StatusCategory.Health)
                    statusData.Health = (int)Mathf.Clamp(statusData.Health + value, 0, maxValue);
                else if(statusCategory == StatusCategory.Mental)
                    statusData.Mental = (int)Mathf.Clamp(statusData.Mental + value, 0, maxValue);

                if (IsLifeStatDepleted)
                {
                    int rebornValue = ManagerObj.PossessionManager.GetBadgeInherentEffect_Int(InherentEffectBadgeCategory.TheNecromancer);
                    if (rebornValue > 0)
                    {
                        statusData.Health = statusData.Mental = rebornValue;
                        ManagerObj.PossessionManager.RemoveBadgeByID(InherentEffectBadgeCategory.TheNecromancer.ToString());
                    }
                    else
                    {
                        if (!BlockGameOverWhenLifeDepleted)
                            ProcessGameOverByLifeStat();
                    }
                }
                break;

            case StatusCategory.Strength: statusData.Strength = (int)Mathf.Clamp(statusData.Strength + value, 0, maxValue); break;
            case StatusCategory.Charisma: statusData.Charisma = (int)Mathf.Clamp(statusData.Charisma + value, 0, maxValue); break;
            case StatusCategory.Reputation:
                value = Mathf.Round(value * 10f) / 10f;
                statusData.Reputation = Mathf.Clamp(statusData.Reputation + value, 0, maxValue); 
                break;
            case StatusCategory.Stress: statusData.Stress = (int)Mathf.Clamp(statusData.Stress + value, 0, maxValue); break;
        }

        ManagerObj.MissionManager.UpdateAcquireCondition(statusCategory, (int)value);
    }

    public bool BlockGameOverWhenLifeDepleted { get; set; } // DayStart에서 잠시 막아두기 위해서 넣어놨음
    public bool IsLifeStatDepleted => (statusData.Health <= 0 || statusData.Mental <= 0);
    public void ProcessGameOverByLifeStat()
    {
        if (ManagerObj.ScriptManager.IsPlayingScript)
            return;

        BookUI.Instance.SetLifeStatGaugePanel();
        ManagerObj.InGameProgressManager.GameOverSetting(); // 여기서 세이브함
        ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.GameOver_LifeStatDepleted, ScriptCategory.Etc);
    }

    public float GetStatusMaxValue(StatusCategory statusCategory)
    {
        switch (statusCategory)
        {
            case StatusCategory.CashReward: return 50;

            case StatusCategory.Health:
            case StatusCategory.Mental: return 100;

            case StatusCategory.Strength: 
            case StatusCategory.Charisma: 
            case StatusCategory.Stress: return 50;

            case StatusCategory.Reputation: return 10;
        }

        return 0; // 실행될 일 없음
    }

    public float ParseValueByStatus(StatusCategory sc, float value)
    {
        switch (sc)
        {
            case StatusCategory.Health:
            case StatusCategory.Mental: return value * 2;

            case StatusCategory.Strength:
            case StatusCategory.Charisma: return value;

            case StatusCategory.Reputation: return value / 5;

            case StatusCategory.Stress: return -value;

            default:
                Debug.LogError($"StatusManager : ParseValueByStatus에서 StatusCategory sc가 잘못되었습니다. sc : {sc}");
                return 0;
        }
    }

    public int GetStatusBlockValue(StatusCategory statusCategory)
    {
        return 0;
    }

    public StatusCategory ParseStatusCategory(string statusStr)
    {
        if (!Enum.TryParse(statusStr, out StatusCategory statusCategory))
        {
            Console.WriteLine($"StatusManager : ParseStatusCategory에서 statusStr가 StatusCategory에 포함되어 있지 않습니다. statusStr : {statusStr}");
            statusCategory = StatusCategory.Health;
        }

        return statusCategory;
    }

    public (float min, float max) GetUsageRewardValueInfo(StatusCategory statusCategory)
    {
        List<int> lifeStatValues = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.Facility_UsageRewardValue_LifeStats);
        List<int> abilityStatValues_Int = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.Facility_UsageRewardValue_AbilityStats_Int);
        switch (statusCategory)
        {
            case StatusCategory.Health:
            case StatusCategory.Mental:
                return (lifeStatValues[0], lifeStatValues[1]);

            case StatusCategory.Strength:
            case StatusCategory.Charisma:
                return (abilityStatValues_Int[0], abilityStatValues_Int[1]);

            case StatusCategory.Reputation:
                List<float> abilityStatValues_Float = ManagerObj.DataManager.GetGameBalanceData(GameBalanceKeyCategory.Facility_UsageRewardValue_AbilityStats_Float);
                return (abilityStatValues_Float[0], abilityStatValues_Float[1]);

            case StatusCategory.Stress:
                return (-abilityStatValues_Int[0], -abilityStatValues_Int[1]);

            default:
                return (0, 0); // 실행될 일 없음
        }
    }

    public static string GetStatusTextKey(StatusCategory statusCategory)
    {
        return "Stat_"+statusCategory.ToString();
    }

    public bool CanAttack
    {
        get => false;
    }

    public static string GetMeID => "Me";
}
