using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public class Dispatcher : MonoBehaviour
{
    public static (T key, List<string> details) SplitKeyAndDetails<T>(string str) where T : struct, System.Enum
    {
        if (string.IsNullOrEmpty(str))
        {
            Debug.LogError($"Dispatcher : SplitKeyAndDetails에서 str이 비어 있습니다.");
            return (default, new List<string>());
        }

        str = PlaceholderResolver.RenderWithTernary(str);
        string[] datas = str.Split(",");

        if (!System.Enum.TryParse<T>(datas[0], true, out T key))
        {
            Debug.LogError($"Dispatcher : SplitKeyAndDetails에서 datas[0]를 {typeof(T)}으로 변환하지 못했습니다. key에는 ParsingFailed가 적재됩니다. datas[0] : {datas[0]}");
        }

        List<string> details = datas.Length > 1 ? datas.Skip(1).ToList() : new List<string>();

        return (key, details);
    }

    public virtual void SetupDispatch(List<string> datas) { // 상속 받는 디스패처 쪽에서 반드시 base를 실행시키도록 코드 추가.
        if (datas == null) 
            return;

        datas.RemoveAll(string.IsNullOrWhiteSpace); // datas에 ""(띄어쓰기만 있는거 포함)이나 null을 제거
    }

    // Dispatcher 별로 Dispatch 정의하는 이유는 반드시 구현하라는 것을 나타내기 위해서. abstract로 구현하지 않는 이유는 반환값이 있기 때문
    // EffectDispatcher
    protected virtual void Dispatch_Effect(DispatchType_Effect eventID, List<string> details) { }

    // DialogueEventDispatcher
    protected virtual IEnumerator Dispatch_DialogueEvent_NonSkip(DispatchType_DialogueEvent eventID, List<string> details) { yield return null; }
    protected virtual IEnumerator Dispatch_DialogueEvent_Skip(DispatchType_DialogueEvent eventID, List<string> details) { yield return null; }

    // ConditionDispatcher
    protected virtual bool Dispatch_Condition(DispatchType_Condition eventID, List<string> details) { return false; }

    /* Dispatcher끼리 공유하는 메서드의 경우 상위 Dispatcher에 구현해주세요 */
    protected void UpdateStatusData(List<string> details, bool isShowViewLine = true)
    {
        if (StatusCategory.TryParse(details[0], out StatusCategory statusCategory))
        {
            if (statusCategory == StatusCategory.Reputation)
            {
                float value = 0.2f;
                if (!float.TryParse(details[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    Debug.LogWarning($"[UpdateStatusData] details[1]를 float로 파싱 실패, value는 0.2f로 설정 details[1] : {details[1]}");

                if (isShowViewLine)
                    UpdatedInfoPanel.Instance.ShowViewLine(statusCategory, value);
                ManagerObj.StatusManager.UpdateStatus(statusCategory, value);
            }
            else
            {
                int value = 2;
                if (!int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    Debug.LogWarning($"[UpdateStatusData] details[1]를 int로 파싱 실패, value는 2로 설정 details[1] : {details[1]}");

                if (isShowViewLine)
                    UpdatedInfoPanel.Instance.ShowViewLine(statusCategory, value);
                ManagerObj.StatusManager.UpdateStatus(statusCategory, value);
            }
        }
        else if (details[0] == "RandomStat")
        {
            List<StatusCategory> categories = new List<StatusCategory> { StatusCategory.Health, StatusCategory.Mental, StatusCategory.Strength, StatusCategory.Charisma, StatusCategory.Reputation, StatusCategory.Stress };
            statusCategory = categories[UnityEngine.Random.Range(0, categories.Count)];

            float value = 1f;
            if (!float.TryParse(details[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                Debug.LogWarning($"[UpdateStatusData] details[1]를 float로 파싱 실패, value는 1로 설정 details[1] : {details[1]}");

            ManagerObj.StatusManager.UpdateStatus(statusCategory, ManagerObj.StatusManager.ParseValueByStatus(statusCategory, value));
        }
        else if (details[0].Contains("ActivePoint"))
        {
            int value = 1;
            if (!int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                Debug.LogWarning($"[UpdateStatusData] details[1]를 int로 파싱 실패, value는 1로 설정 details[1] : {details[1]}");

            ManagerObj.InGameProgressManager.CurrentActivePoints -= value;

            if (isShowViewLine)
                UpdatedInfoPanel.Instance.ShowViewLine("ActivePoint", value);

            BookUI.Instance?.ClosedBook.SetBookText();
        }
        else
        {
            List<Possession> displayPossessionList = new();

            string possessionID = details[0];
            if (ManagerObj.PossessionManager.GetItemClone(possessionID) is Item clonedItem)
            {
                if (ManagerObj.PossessionManager.Inventory.Count + 1 > ManagerObj.PossessionManager.CurrentMaxInventorySize)
                    ManagerObj.PossessionManager.TempItemList.Add(clonedItem);
                else
                    ManagerObj.PossessionManager.AddPossessionOnStorage(clonedItem);

                displayPossessionList.Add(clonedItem);
            }
            else if (possessionID == "RandomItem")
            {
                int getValue = 1;
                List<float> gradeProbabilities = new();

                if (int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out getValue))
                {
                    gradeProbabilities = ManagerObj.DataManager.GetGameBalanceData(GameBalanceKeyCategory.Character_PossedItemGradeProbabilities);
                }
                else if (Enum.TryParse(details[1], true, out Item_Grade itemGrade))
                {
                    switch (itemGrade)
                    {
                        case Item_Grade.Normal: gradeProbabilities = new List<float> { 100, 0, 0, 0 }; break;
                        case Item_Grade.Rare: gradeProbabilities = new List<float> { 0, 100, 0, 0 }; break;
                        case Item_Grade.Epic: gradeProbabilities = new List<float> { 0, 0, 100, 0 }; break;
                        case Item_Grade.Cursed: gradeProbabilities = new List<float> { 0, 0, 0, 100 }; break;
                    }

                    if (!int.TryParse(details[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out getValue))
                    {
                        Debug.LogWarning($"[UpdateStatusData] RandomItem 획득 도중 details[2]를 int로 파싱 실패, getValue 1로 설정 details[2] : {details[2]}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[UpdateStatusData] RandomItem 획득 도중 details[1]를 int로 파싱 실패, getValue 1로 설정, 아이템은 모든 등급 가능 details[1] : {details[1]}");
                    gradeProbabilities = ManagerObj.DataManager.GetGameBalanceData(GameBalanceKeyCategory.Character_PossedItemGradeProbabilities);
                }

                List<Item> randomItems = new();
                for (int i = 0; i < getValue; i++)
                    randomItems.Add(ManagerObj.PossessionManager.GetRandomItem(gradeProbabilities));

                if ((ManagerObj.PossessionManager.Inventory.Count + randomItems.Count > ManagerObj.PossessionManager.CurrentMaxInventorySize) && ManagerObj.ScriptManager.IsPlayingScript)
                    ManagerObj.PossessionManager.TempItemList.AddRange(randomItems);
                else
                    ManagerObj.PossessionManager.AddPossessionsOnStorage(randomItems);

                displayPossessionList.AddRange(randomItems);
            }
            else if (ManagerObj.PossessionManager.GetBadgeInfo(possessionID) is Badge badgeInfo)
            {
                float getCount = 1;
                if(details.Count > 1 && float.TryParse(details[1], NumberStyles.Float, CultureInfo.InvariantCulture, out getCount))
                {
                    if(getCount < 0)
                    {
                        for(int i = 0; i < -1 * getCount; i++)
                            ManagerObj.PossessionManager.RemoveBadgeByID(badgeInfo.PossessionID);

                        return;
                    }
                }

                for (int i = 0; i < getCount; i++)
                {
                    Badge cloneBadge = ManagerObj.PossessionManager.GetBadgeClone(badgeInfo.PossessionID);
                    ManagerObj.PossessionManager.AddPossessionOnStorage(cloneBadge);
                    displayPossessionList.Add(cloneBadge);
                }
            }

            if (isShowViewLine)
            {
                foreach (Possession posesssion in displayPossessionList)
                    UpdatedInfoPanel.Instance.ShowViewLine(posesssion);
            }
        }
    }
}
