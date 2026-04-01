using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.TextCore.Text;
using static DispatchType_Condition;
using System;
using System.Globalization;



#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

public class ConditionDispatcher : Dispatcher
{
    public bool GetDispatchedResult(List<string> datas)
    {
        datas.RemoveAll(data => string.IsNullOrEmpty(data));

        if (datas.Count == 0) // ConversationTopicInfo의 CancelCondition에 해당되지 않으면 false를 반환시키도록 해야하기 때문에 false return
            return false;

        foreach (string data in datas)
        {
            if (!GetDispatchedResultWithLogicalExpression(data))
                return false;
        }

        return true;
    }

    public bool GetDispatchedResult(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogError($"ConditionDispatcher : GetDispatchedResult에서 전달받은 data가 비어있습니다.");
            return false;
        }

        return GetDispatchedResultWithLogicalExpression(data);
    }

    // 논리식 파서로 여러 조건식을 계산할 수 있음
    bool GetDispatchedResultWithLogicalExpression(string expr) // --- 논리식 파서 ((), &&, ||) – 단축 평가, ! 미지원 ---
    {
        int i = 0; ReadWS();
        bool ParseOr()
        {
            bool v = ParseAnd();
            while (Try("||"))
            {
                if (v) { SkipToOrEnd(); return true; } // 단축평가
                v = ParseAnd();
            }
            return v;
        }
        bool ParseAnd()
        {
            bool v = ParsePrimary();
            while (Try("&&"))
            {
                if (!v) { SkipToAndEnd(); return false; } // 단축평가
                v = ParsePrimary();
            }
            return v;
        }
        bool ParsePrimary()
        {
            ReadWS();
            if (Try("("))
            {
                bool inner = ParseOr();
                ReadWS(); Expect(")");
                return inner;
            }
            string atom = ReadAtom();
            if (atom.Length == 0) throw new FormatException("유효한 조건 토큰이 필요합니다.");

            var parsedStr = SplitKeyAndDetails<DispatchType_Condition>(atom);
            return Dispatch_Condition(parsedStr.key, parsedStr.details);
        }

        // --- helpers ---
        void ReadWS() { while (i < expr.Length && char.IsWhiteSpace(expr, i)) i++; }
        bool Try(string tok)
        {
            ReadWS();
            if (i + tok.Length <= expr.Length && string.Compare(expr, i, tok, 0, tok.Length, StringComparison.Ordinal) == 0)
            { i += tok.Length; return true; }
            return false;
        }
        void Expect(string tok) { if (!Try(tok)) throw new FormatException($"누락된 토큰 `{tok}`"); }
        string ReadAtom()
        {
            ReadWS();
            int s = i;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '&' || c == '|') break;
                i++;
            }
            return expr.Substring(s, i - s).Trim();
        }
        void SkipUntil(Func<char, bool> stop)
        {
            while (i < expr.Length)
            {
                char c = expr[i];
                if (c == '(') { i++; int depth = 1; while (i < expr.Length && depth > 0) { if (expr[i] == '(') depth++; else if (expr[i] == ')') depth--; i++; } }
                else { if (stop(c)) break; i++; }
            }
        }
        void SkipToAndEnd() { SkipUntil(c => c == '|' || c == ')'); } // 다음 || 또는 ) 전까지
        void SkipToOrEnd() { SkipUntil(c => c == ')'); }            // 다음 ) 전까지

        bool result = ParseOr();
        ReadWS();
        if (i != expr.Length) throw new FormatException($"해석되지 않은 토큰: `{expr.Substring(i)}`");
        return result;
    }

    protected override bool Dispatch_Condition(DispatchType_Condition conditionID, List<string> details)
    {
        bool returnValue = false;

        switch (conditionID)
        {
            case DispatchType_Condition.CompareDay: returnValue = CompareDay(details); break;
            case DispatchType_Condition.CompareActivePoints: returnValue = CompareActivePoints(details); break;
            case DispatchType_Condition.IsNightActivity: returnValue = IsNightActivity(details); break;
            case DispatchType_Condition.HasDoneNightActivity: returnValue = HasDoneNightActivity(details); break;
            case DispatchType_Condition.CheckSelectedNightActivity: returnValue = CheckSelectedNightActivity(details); break;
            case DispatchType_Condition.CheckStatus: returnValue = CheckStatus(details); break;
            case DispatchType_Condition.CheckCharacter: returnValue = CheckCharacter(details); break;
            /*case DispatchType_Condition.CheckCharacterNameOpened: returnValue = CheckCharacterNameOpened(details); break;
            case DispatchType_Condition.CheckCharacterSpecialNoteOpened: returnValue = CheckCharacterSpecialNoteOpened(details); break;
            case DispatchType_Condition.CheckCharacterReliability: returnValue = CheckCharacterReliability(details); break;*/
            case DispatchType_Condition.CheckItem: returnValue = CheckItem(details); break;
            case DispatchType_Condition.CheckBadge: returnValue = CheckBadge(details); break;
            case DispatchType_Condition.CheckPlayedScript: returnValue = CheckPlayedScript(details); break;
            case DispatchType_Condition.CheckCompletedScript: returnValue = CheckCompletedScript(details); break;
            case DispatchType_Condition.CheckCanceledScript: returnValue = CheckCanceledScript(details); break;
            case DispatchType_Condition.CheckTrigger: returnValue = CheckTrigger(details); break;
            case DispatchType_Condition.CheckEliminated: returnValue = CheckEliminated(details); break;
            case DispatchType_Condition.CheckEliminatedCharacterToday: returnValue = CheckEliminatedCharacterToday(details); break;
            case DispatchType_Condition.CheckKillTarget: returnValue = CheckKillTarget(details); break;
            case DispatchType_Condition.CheckVisitingCharacter: returnValue = CheckVisitingCharacter(details); break;
            case DispatchType_Condition.CheckVisitingFacility: returnValue = CheckVisitingFacility(details); break;
            case DispatchType_Condition.CheckLowestReputationCharacter: returnValue = CheckLowestReputationCharacter(details); break;

            default: Debug.LogError($"ConditionDispatcher : Dispatch_Condition에서 conditionID가 올바르지 않습니다.(false 반환) conditionID : {conditionID.ToString()}"); break;
        }

        return returnValue;
    }

    bool CompareDay(List<string> details)
    {
        if (details.Count != 2)
        {
            Debug.LogError($"[CompareDay] details의 인덱스 개수가 2가 아닙니다. details.Count : {details.Count}");
            return false;
        }

        return ComparisonOperatorParser(ManagerObj.InGameProgressManager.CurrentDay, details[0], details[1]);
    }

    bool CompareActivePoints(List<string> details)
    {
        if (details.Count != 2)
        {
            Debug.LogError($"[CompareActivePoints] details의 인덱스 개수가 2가 아닙니다. details.Count : {details.Count}");
            return false;
        }

        return ComparisonOperatorParser(ManagerObj.InGameProgressManager.CurrentActivePoints, details[0], details[1]);
    }

    bool IsNightActivity(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[IsNightActivity] details의 인덱스 개수가 1이 아닙니다. details.Count : {details.Count}");
            return false;
        }

        if (!bool.TryParse(details[0], out bool isNightActivity))
        {
            Debug.LogError($"[IsNightActivity] details[0] 파싱 실패 details[0] : {details[0]}");
            return false;
        }

        return ManagerObj.InGameProgressManager.IsNightActivity == isNightActivity;
    }

    bool HasDoneNightActivity(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[HasDoneNightActivity] details의 인덱스 개수가 1이 아닙니다.(false 반환) details.Count : {details.Count}");
            return false;
        }

        if (!bool.TryParse(details[0], out bool hasDoneNightActivity))
        {
            Debug.LogError($"[HasDoneNightActivity] details[0] 파싱 실패 details[0] : {details[0]}");
            return false;
        }

        return ManagerObj.InGameProgressManager.HasDoneNightActivity == hasDoneNightActivity;
    }

    bool CheckSelectedNightActivity(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[CheckSelectedNightActivity] details의 인덱스 개수가 1이 아닙니다.(false 반환) details.Count : {details.Count}");
            return false;
        }

        if (!Enum.TryParse(details[0], true, out ActivityCategory nightActivityCategory))
        {
            Debug.LogError($"[CheckSelectedNightActivity] details[0] ActivityCategory으로 파싱 실패 details[0] : {details[0]}");
            return false;
        }

        return ManagerObj.InGameProgressManager.SelectedNightActivity == nightActivityCategory;
    }

    bool CheckStatus(List<string> details)
    {
        if (details.Count != 3)
        {
            Debug.LogError($"[HasDoneNightActivity] details의 인덱스 개수가 3이 아닙니다. details.Count : {details.Count}");
            return false;
        }

        if (System.Enum.TryParse<StatusCategory>(details[0], out StatusCategory statusID))
        {
            return ComparisonOperatorParser(ManagerObj.StatusManager.GetStatus(statusID), details[1], details[2]);
        }

        Debug.LogError($"[CheckStatus] details[0]가 StatusCategory로 파싱되지 못했습니다.(true 반환) details[1] : {details[1]}");
        return true;
    }

    bool CheckCharacter(List<string> _details)
    {
        if (_details.Count < 2)
        {
            Debug.LogError($"[CheckCharacter] _details의 인덱스 개수가 0입니다.. (true 반환) _details.Count : {_details.Count}");
            return true;
        }

        string checkKey = _details[0];
        List<string> details = _details.Skip(1).ToList();

        switch (checkKey)
        {
            case "NameOpened": return CheckCharacterNameOpened(details);
            case "SpecialNoteOpened": return CheckCharacterSpecialNoteOpened(details);
            case "Reliability": return CheckCharacterReliability(details);
            case "ReliabilityValue": return CheckCharacterReliabilityValue(details);
            case "Reputation": return CheckCharacterReputation(details);
            default:
                Debug.LogError("ConditionDispatcher : CheckCharacter에서 checkKey가 case에 속해 있지 않습니다.(true 반환)");
                return true;
        }

        bool CheckCharacterNameOpened(List<string> details)
        {
            if (!System.Enum.TryParse<CharacterID>(details[0], out CharacterID characterID))
            {
                Debug.LogError($"[CheckCharacterNameOpened] details[0]를 CharacterID로 파싱하지 못했습니다. details[0] (true 반환) : {details[0]}");
                return true;
            }

            if (details.Count == 1)
            {
                return ManagerObj.CharacterManager.GetCharacterData(characterID).IsNameOpened;
            }
            else if (details.Count == 2)
            {
                bool isOpened = true;
                if (!bool.TryParse(details[1], out isOpened))
                {
                    Debug.LogError($"ConditionDispatcher : CheckCharacterNameOpened에서 details[1]을 bool로 치환하지 못했습니다.(true 반환) details[1] : {details[1]}");
                    return true;
                }
                return ManagerObj.CharacterManager.GetCharacterData(characterID).IsNameOpened == isOpened;
            }
            else
            {
                Debug.LogError($"ConditionDispatcher : CheckCharacterNameOpened에서 details.Count가 1 또는 2가 아닙니다. (true 반환) details.Count : {details.Count}");
                return true;
            }
        }

        bool CheckCharacterSpecialNoteOpened(List<string> details)
        {
            if (details.Count != 2)
            {
                Debug.LogError($"[CheckCharacterSpecialNoteOpened] details의 인덱스 개수가 2이 아닙니다. (true 반환) details.Count : {details.Count}");
                return true;
            }

            if (!System.Enum.TryParse<CharacterID>(details[0], out CharacterID characterID))
            {
                Debug.LogError($"[CheckCharacterSpecialNoteOpened] details[0]를 CharacterID로 파싱하지 못했습니다. details[0] (true 반환) : {details[0]}");
                return true;
            }

            return ManagerObj.CharacterManager.CheckSpecialNoteOpened(characterID, details[1]);
        }

        bool CheckCharacterReliability(List<string> details)
        {
            if (details.Count != 3)
            {
                Debug.LogError($"[CheckCharacterReliability] details의 인덱스 개수가 3이 아닙니다. (true 반환) details.Count : {details.Count}");
                return true;
            }

            if (!System.Enum.TryParse<CharacterID>(details[0], out CharacterID characterID))
            {
                Debug.LogError($"[CheckCharacterNameOpened] details[0]를 CharacterID로 파싱하지 못했습니다. details[0] (true 반환) : {details[0]}");
                return true;
            }

            if (!System.Enum.TryParse<ReliabilityCategory>(details[2], out ReliabilityCategory standardReliability))
            {
                Debug.LogError($"[CheckCharacterNameOpened] details[2]를 ReliabilityCategory로 파싱하지 못했습니다. details[2] (true 반환) : {details[2]}");
                return true;
            }

            ReliabilityCategory characterReliability = ManagerObj.CharacterManager.GetCharacterData(characterID).Reliability.ReliabilityCategory;

            return ComparisonOperatorParser((int)characterReliability, details[1], (int)standardReliability);
        }

        bool CheckCharacterReliabilityValue(List<string> details)
        {
            if (details.Count != 3)
            {
                Debug.LogError($"[CheckCharacterReliability] details의 인덱스 개수가 3이 아닙니다. (true 반환) details.Count : {details.Count}");
                return true;
            }

            if (!System.Enum.TryParse<CharacterID>(details[0], out CharacterID characterID))
            {
                Debug.LogError($"[CheckCharacterNameOpened] details[0]를 CharacterID로 파싱하지 못했습니다. details[0] (true 반환) : {details[0]}");
                return true;
            }

            if (!System.Enum.TryParse<ReliabilityCategory>(details[2], out ReliabilityCategory standardReliability))
            {
                Debug.LogError($"[CheckCharacterNameOpened] details[2]를 ReliabilityCategory로 파싱하지 못했습니다. details[2] (true 반환) : {details[2]}");
                return true;
            }

            List<int> reliabilityStandard = ManagerObj.CharacterManager.GetReliabilityStandard;
            return ComparisonOperatorParser(ManagerObj.CharacterManager.GetCharacterData(characterID).Reliability.Value, details[1], reliabilityStandard[(int)standardReliability - 1]);
        }

        bool CheckCharacterReputation(List<string> details)
        {
            if (details.Count != 3)
            {
                Debug.LogError($"[CheckCharacterReputation] details의 인덱스 개수가 3가 아닙니다. (true 반환) details.Count : {details.Count}");
                return true;
            }

            if (!System.Enum.TryParse<CharacterID>(details[0], out CharacterID characterID))
            {
                Debug.LogError($"[CheckCharacterReputation] details[0]를 CharacterID로 파싱하지 못했습니다. details[0] (true 반환) : {details[0]}");
                return true;
            }

            if (!float.TryParse(details[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float standardValue))
            {
                Debug.LogError($"[CheckCharacterReputation] details[2]를 float로 파싱하지 못했습니다. (true 반환) details[2] : {details[2]}");
                return true;
            }

            return ComparisonOperatorParser(ManagerObj.CharacterManager.GetCharacterData(characterID).Reputation, details[1], standardValue);
        }
    }

    bool CheckItem(List<string> details)
    {
        if (details.Count != 1 && details.Count != 2)
        {
            Debug.LogError($"[CheckItem] details.Count가 1 또는 2가 아닙니다.(true 반환) details.Count : {details.Count}");
            return true;
        }

        bool shouldContain = true;
        if (details.Count == 2)
        {
            if (!bool.TryParse(details[1], out shouldContain))
            {
                Debug.LogError($"[CheckItem] shouldContain가 bool로 파싱되지 못했습니다.(true 자동 설정) details[1] : {details[1]}");
            }
        }

        PossessionManager possessionManager = ManagerObj.PossessionManager;

        string itemID = details[0];

        if (possessionManager.GetItemInfo(itemID) == null)
        {
            Debug.LogError($"[CheckItem] details에 잘못된 itemID가 있습니다.(true 반환) 잘못된 itemID : {itemID}");
            return true;
        }
        else if (!possessionManager.Inventory.Any(item => item.PossessionID == itemID) && shouldContain) // 인벤토리에 해당 itemID가 없는데 shouldContain가 true면 return false
        {
            return false;
        }
        else if (possessionManager.Inventory.Any(item => item.PossessionID == itemID) && !shouldContain) // 인벤토리에 해당 itemID가 있는데 shouldContain가 false면 return false
        {
            return false;
        }
        else
            return true;
    }

    bool CheckBadge(List<string> details)
    {
        if (details.Count != 1 && details.Count != 2)
        {
            Debug.LogError($"[CheckBadge] details.Count가 1 또는 2가 아닙니다.(true 반환) details.Count : {details.Count}");
            return true;
        }

        bool shouldContain = true;
        if (details.Count == 2)
        {
            if (!bool.TryParse(details[1], out shouldContain))
            {
                Debug.LogError($"[CheckBadge] shouldContain가 bool로 파싱되지 못했습니다.(true 자동 설정) details[1] : {details[1]}");
            }
        }

        PossessionManager possessionManager = ManagerObj.PossessionManager;

        string badgeID = details[0];

        if (possessionManager.GetBadgeInfo(badgeID) == null)
        {
            Debug.LogError($"[CheckBadge] details에 잘못된 badgeID가 있습니다.(true 반환) 잘못된 badgeID : {badgeID}");
            return true;
        }
        else if (!possessionManager.BadgeCase.Any(badge => badge.PossessionID == badgeID) && shouldContain) // 배지케이스에 해당 badgeID가 없는데 shouldContain가 true면 return false
        {
            return false;
        }
        else if (possessionManager.BadgeCase.Any(badge => badge.PossessionID == badgeID) && !shouldContain) // 배지케이스에 해당 badgeID가 있는데 shouldContain가 false면 return false
        {
            return false;
        }
        else
            return true;
    }

    bool CheckPlayedScript(List<string> details) // 구성 : scriptID, scriptCategory, ComparisonOperator, Count
    {
        if (details.Count == 2) // 스크립트가 Played 된 적이 있는지 확인
        {
            return ManagerObj.ScriptManager.GetScriptPlayCount(ScriptRequest.GetScriptRequest(details[0], details[1])) > 0;
        }
        else if (details.Count == 4) // 스크립트가 몇 회 Played 되었는지 확인
        {
            int count = ManagerObj.ScriptManager.GetScriptPlayCount(ScriptRequest.GetScriptRequest(details[0], details[1]));

            int standardValue = 1;
            if (!int.TryParse(details[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out standardValue))
            {
                Debug.LogError($"ConditionDispatcher : CountPlayedScript에서 details[3]를 int.TryParse하는데 실패했습니다.(standardValue = 1 설정) 전달받은 details[3] : {details[3]}");
            }

            return ComparisonOperatorParser(count, details[2], details[3]);
        }
        else if (details.Count == 3) // 스크립트가 마지막으로 플레이되고 몇 일이 지났는지 확인
        {
            ScriptPlayData searchedData = ManagerObj.ScriptManager.GetScriptPlayDataListByScriptRequest(ManagerObj.InGameProgressManager.GetPlayedScriptList, ScriptRequest.GetScriptRequest(details[0], details[1]))
            .OrderByDescending(x => x.Day)
            .FirstOrDefault();

            if (searchedData == null) // 플레이된 적이 없는 경우
                return false;

            int lastPlayedDay = searchedData.Day;

            int mustPassDay = 1;
            if (!int.TryParse(details[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out mustPassDay))
            {
                Debug.LogError($"ConditionDispatcher : CheckDaysPassedSinceScriptPlayed에서 details[2]를 int.TryParse하는데 실패했습니다.(standardValue = 1 설정) 전달받은 details[2] : {details[2]}");
            }

            return ManagerObj.InGameProgressManager.CurrentDay - lastPlayedDay >= mustPassDay;
        }
        else
        {
            Debug.LogError($"[CheckDaysPassedSinceScriptPlayed] details가 2 또는 3 또는 4가 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }
    }

    bool CheckCompletedScript(List<string> details)
    {
        if (details.Count == 2) // 스크립트가 Completed 되었는지 확인
        {
            return ManagerObj.ScriptManager.IsScriptCompleted(ScriptRequest.GetScriptRequest(details[0], details[1]));
        }
        else if (details.Count == 3) // 스크립트가 마지막으로 완료되고 몇 일이 지났는지 확인
        {
            if (bool.TryParse(details[2], out bool isCompleted))
            {
                return ManagerObj.ScriptManager.IsScriptCompleted(ScriptRequest.GetScriptRequest(details[0], details[1])) == isCompleted;
            }
            else
            {
                ScriptPlayData searchedData = ManagerObj.ScriptManager.GetScriptPlayDataListByScriptRequest(ManagerObj.InGameProgressManager.GetCompletedScriptList, ScriptRequest.GetScriptRequest(details[0], details[1]))
                .OrderByDescending(x => x.Day)
                .FirstOrDefault();

                if (searchedData == null) // 플레이된 적이 없는 경우
                    return false;

                int lastPlayedDay = searchedData.Day;

                int mustPassDay = 1;
                if (!int.TryParse(details[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out mustPassDay))
                {
                    Debug.LogError($"ConditionDispatcher : CheckDaysPassedSinceScriptCompleted에서 details[2]를 int.TryParse하는데 실패했습니다.(standardValue = 1 설정) 전달받은 details[2] : {details[2]}");
                }

                return ManagerObj.InGameProgressManager.CurrentDay - lastPlayedDay >= mustPassDay;
            }
        }
        else
        {
            Debug.LogError($"[CheckDaysPassedSinceScriptCompleted] details가 2또는 3이 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }
    }

    bool CheckCanceledScript(List<string> details)
    {
        if (details.Count == 2) // 스크립트가 Completed 되었는지 확인
        {
            return ManagerObj.ScriptManager.IsScriptCanceled(ScriptRequest.GetScriptRequest(details[0], details[1]));
        }
        else
        {
            Debug.LogError($"[CheckCanceledScript] details가 2가 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }
    }

    bool CheckTrigger(List<string> details)
    {
        if (details.Count == 0)
        {
            Debug.LogError($"CheckTrigger에서 details의 count가 0입니다.(true 반환)");
            return true;
        }

        string triggerContent = details[0];

        if (!ManagerObj.InGameProgressManager.CheckTriggerValidate(triggerContent))
        {
            Debug.LogError($"ManagerObj.InGameProgressManager.CheckTriggerValidate에서 false가 나왔습니다. (true 반환)");
            return true;
        }

        if (details.Count == 1) // 트리거가 추가된 적이 있는지
        {
            return ManagerObj.InGameProgressManager.TriggerList.Count(trigger => trigger.Content == triggerContent) > 0;
        }
        else if (details.Count == 3) // 트리거가 몇 회 추가되었는지 확인
        {
            int countValue = ManagerObj.InGameProgressManager.TriggerList.Count(trigger => trigger.Content == triggerContent);

            return ComparisonOperatorParser(countValue, details[1], details[2]);
        }
        else if (details.Count == 2) // 트리거가 추가되고 몇 일이 지났는지 확인
        {
            Trigger searchedTrigger = ManagerObj.InGameProgressManager.TriggerList
                .Where(x => x.Content == triggerContent)
                .OrderByDescending(x => x.Day)
                .FirstOrDefault();

            if (searchedTrigger == null) // 트리거가 추가된 적이 없는 경우
                return false;

            int lastPlayedDay = searchedTrigger.Day;

            int mustPassDay = 1;
            if (!int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out mustPassDay))
            {
                Debug.LogError($"ConditionDispatcher : CheckDaysPassedSinceTriggerAdded에서 details[2]를 int.TryParse하는데 실패했습니다.(standardValue = 1 설정) 전달받은 details[1] : {details[1]}");
            }

            return ManagerObj.InGameProgressManager.CurrentDay - lastPlayedDay >= mustPassDay;
        }
        else
        {
            Debug.LogError($"CheckTrigger : details가 1 또는 3 또는 2가 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }
    }

    bool CheckEliminated(List<string> details)
    {
        CharacterManager characterManager = ManagerObj.CharacterManager;

        if (details.Count == 1) // 누가 탈락했는지 (characterID)
        {
            return IsCharacterEliminated(details[0]);
        }
        else if (details.Count == 2) // 몇 명 탈락햇는지
        {
            if (System.Enum.TryParse<CharacterID>(details[0], true, out CharacterID characterID) && bool.TryParse(details[1], out bool isEliminated))
            {
                return IsCharacterEliminated(details[0], isEliminated);
            }
            else
            {
                int count = 1;
                if (!int.TryParse(details[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                {
                    Debug.LogError($"ConditionDispatcher : CheckEliminated에서 details[1]를 int로 파싱하는데 실패했습니다.(count에 1 적재) 전달받은 details[1] : {details[1]}");
                }
                return ComparisonOperatorParser(characterManager.CountEliminatedCharacter(), details[0], count);
            }
        }
        else
        {
            Debug.LogError($"ConditionDispatcher : CheckEliminated에서 details.Count가 1 또는 2가 아닙니다.(true 반환) details.Count : {details.Count}");
            return true;
        }

        bool IsCharacterEliminated(string characterStr, bool isEliminated = true)
        {
            CharacterID characterID = characterManager.ParseStringToCharacterID(characterStr);
            if (characterID != CharacterID.None)
            {
                return isEliminated == characterManager.GetCharacterData(characterID).IsEliminated;
            }
            else
            {
                Debug.LogError($"ConditionDispatcher : IsCharacterEliminated에서 characterStr를 CharacterID로 파싱하는데 실패했습니다.(true 반환) 전달받은 characterStr : {characterStr}");
                return true;
            }
        }
    }

    bool CheckEliminatedCharacterToday(List<string> details)
    {
        InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
        EliminationData eliminationData = inGameProgressManager.EliminationData[inGameProgressManager.CurrentDay];

        if (EliminationData.IsNullOrEmpty(eliminationData)) // 살인이 발생하지 않은 경우 그냥 false return
            return false;

        if (details.Count == 0) // 살인이 발생했는지 확인
        {
            return true; // 살인이 발생하지 않은 경우는 자동으로 false 반환이므로 그냥 true 반환
        }
        else if (details.Count == 1) // 공격자가 누구인지 확인
        {
            if (int.TryParse(details[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                return eliminationData.Targets.Count == count;
            }
            else if (details[0] == StatusManager.GetMeID || Enum.TryParse<CharacterID>(details[0], true, out CharacterID characterID))
            {
                return eliminationData.Attacker == details[0];
            }
            else
            {
                Debug.LogError($"ConditionDispatcher : CheckEliminatedCharacterToday에서 details[0]를 int로 파싱하지 못했으며, details[0]가 Me도 CharacterID도 아닙니다.(false 반환) 전달받은 details[0] : {details[0]}");
                return false;
            }
        }
        else
        {
            Debug.LogError($"ConditionDispatcher : CheckEliminatedCharacterToday에서 details.Count가 0 또는 1가 아닙니다.(false 반환) details.Count : {details.Count}");
            return false;
        }
    }

    bool CheckKillTarget(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"ConditionDispatcher : CheckKillTarget details.Count가 1 이 아닙니다.(true 반환) details.Count : {details.Count}");
            return true;
        }

        return ManagerObj.InGameProgressManager.CheckKillTargetConditionInfos(details[0]);
    }

    bool CheckVisitingCharacter(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[CheckVisitingCharacter] details가 1이 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }

        if (!System.Enum.TryParse(details[0], true, out CharacterID characterID))
        {
            Debug.LogError($"[CheckVisitingCharacter] details[0]를 CharacterID로 파싱하는데 실패했습니다. (true 반환) 잘못된 details[0] : {details[0]}");
            return true;
        }

        InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
        return inGameProgressManager.VisitingCharacter != null && inGameProgressManager.VisitingCharacter.CharacterID == characterID;
    }

    bool CheckVisitingFacility(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[CheckVisitingFacility] details가 1이 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }

        if (!System.Enum.TryParse(details[0], true, out FacilityID facilityID))
        {
            Debug.LogError($"[CheckVisitingFacility] details[0]를 FacilityID로 파싱하는데 실패했습니다. (true 반환) 잘못된 details[0] : {details[0]}");
            return true;
        }

        InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
        return (inGameProgressManager.VisitingFacility != null && inGameProgressManager.VisitingFacility.FacilityID == facilityID)
            || (inGameProgressManager.VisitingFacility == null && facilityID == FacilityID.Lobby);
    }

    bool CheckLowestReputationCharacter(List<string> details)
    {
        if (details.Count != 1)
        {
            Debug.LogError($"[CheckCharacterLowestReputation] details가 1이 아닙니다.(true 반환) 잘못된 details.Count : {details.Count}");
            return true;
        }
        else if (details[0] != StatusManager.GetMeID && !Enum.TryParse<CharacterID>(details[0], true, out CharacterID characterID))
        {
            Debug.LogError($"[CheckCharacterLowestReputation] details[0]가 StatusManager.GetMeID도, CharacterID도 아닙니다. (true 반환) details[0] : {details[0]}");
            return true;
        }

        string lowestReputationPerson = "";
        Character lowestReputationCharacter = ManagerObj.CharacterManager.GetLowestReputationCharacter();
        if (ManagerObj.StatusManager.GetStatus(StatusCategory.Reputation) <= lowestReputationCharacter.Reputation)
            lowestReputationPerson = StatusManager.GetMeID;
        else
            lowestReputationPerson = lowestReputationCharacter.CharacterID.ToString();

        return lowestReputationPerson == details[0];
    }

    public bool ComparisonOperatorParser(float comparisonValue, string operatorSymbol, string rValueInput)
    {
        if (!float.TryParse(rValueInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float rValue))
        {
            Debug.LogError($"ConditionDispatcher : ComparisonOperatorParser에서 rValueInput를 float.TryParse하는데 실패했습니다. 전달받은 rValueInput : {rValueInput}");
            return false;
        }

        return ComparisonOperatorParser(comparisonValue, operatorSymbol, rValue);
    }

    public bool ComparisonOperatorParser(float comparisonValue, string operatorSymbol, float rValue)
    {
        if (!IsValidComparisonOperator(operatorSymbol))
        {
            Debug.LogError($"ConditionDispatcher : ComparisonOperatorParser에서 operatorSymbol이 >,>=,==,!=,<,<=에 포함되어 있지 않습니다.(false 반환) 전달받은 operatorSymbol : {operatorSymbol}");
            return false;
        }

        switch (operatorSymbol)
        {
            case ">": return comparisonValue > rValue;
            case ">=": return comparisonValue >= rValue;
            case "==": return comparisonValue == rValue;
            case "!=": return comparisonValue != rValue;
            case "<": return comparisonValue < rValue;
            case "<=": return comparisonValue <= rValue;

            default: return false;
        }
    }

    public static bool IsValidComparisonOperator(string op) => new HashSet<string> { ">", ">=", "==", "!=", "<", "<=" }.Contains(op);
}
