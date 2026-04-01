using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TMPro;
using System.Linq;
using UnityEngine;
using static Character;
using System.IO;
using CsvHelper;
using System.Globalization;

#if UNITY_EDITOR
using Unity.VisualScripting;
using UnityEditor.U2D.Animation;
#endif

public class CharacterManager : InGameManager
{
    List<Character> characterDatas;

    public override IEnumerator InitInGame()
    {
        Task<(string key, TextAsset asset)> specialNotesCsvTask = ManagerObj.DataManager.LoadAssetByAddress<TextAsset>("SpecialNotes", "SpecialNotes", AddressableLabelCategory.Data, AddressableLabelCategory.StaticData, AddressableLabelCategory.DataInfos);
        yield return new WaitUntil(() => specialNotesCsvTask.IsCompleted);
        TextAsset specialNotesData = specialNotesCsvTask.Result.asset;
        Dictionary<CharacterID, List<(string noteID, string content)>> specialNotesdata = new();

        using (var reader = new StringReader(specialNotesData.text))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "Key" 이름으로 접근 가능)

            List<(string noteID, string content)> currentData = null;

            while (csv.Read())
            {
                if (Enum.TryParse<CharacterID>(csv.GetField("Key"), true, out CharacterID characterID))
                {
                    specialNotesdata[characterID] = new();
                    currentData = specialNotesdata[characterID];
                    continue;
                }

                (string key, string content) noteData = (csv.GetField("Key"), csv.GetField(ManagerObj.OptionManager.GetLanguageSetting()).Replace("\\n", "\n"));
                currentData.Add(noteData);
            }
        }

        ManagerObj.DataManager.ReleaseAddressableAssets(specialNotesCsvTask.Result.key);

        characterDatas = (List<Character>)ManagerObj.DataManager.RuntimeData[SaveDataCategory.Character];

        foreach (Character character in characterDatas) // 각 캐릭터 별로 SpecialNote 설정해주기. (기존 SpecialNote 리스트 중에 없는 새로운 스페셜 노트가 있으면 추가함)
        {
            List<SpecialNote> orderedNotes = specialNotesdata[character.CharacterID] // noteIDs 순서대로 specialNotes 정렬 + 없는 경우 새로 생성
                .Select(data =>
                {
                    string id = data.noteID;   // noteID 사용
                    SpecialNote currentNote = character.SpecialNotes.FirstOrDefault(n => n.NoteID == id) ?? new SpecialNote(id);
                    currentNote.Content = PlaceholderResolver.RenderWithKeys(data.content);
                    return currentNote;
                })
                .ToList();

            character.SpecialNotes = orderedNotes; // specialNotes 업데이트, 이 때 noteDatas에 없는 specialNote 객체는 자연스럽게 빠짐.
        }

        ConfiguredCharacterID = CharacterID.None;
    }

    public override IEnumerator InitOutOfGame()
    {
        characterDatas = null;

        yield return null;
    }

    public static CharacterID[] GetAllCharacterID => (CharacterID[])Enum.GetValues(typeof(CharacterID));

    public Character GetCharacterData(CharacterID characterID)
    {
        return characterDatas.FirstOrDefault(c => c.CharacterID == characterID);
    }

    public void OpenCharacterName(CharacterID characterID)
    {
        if(GetCharacterData(characterID) is Character currentCharacter)
        {
            currentCharacter.IsNameOpened = true;

            ManagerObj.MissionManager.CheckMissionData();
        }
        else
        {
            Debug.LogError($"CharacterManager : OpenCharacterName에서 GetCharacterData(characterID)가 실패했습니다. characterID : {characterID.ToString()}");
            return;
        }
    }

    public string GetCharacterName(CharacterID characterID)
    {
        if (GetCharacterData(characterID).IsNameOpened)
            return ManagerObj.DataManager.GetEtcText(characterID.ToString());
        else
            return "? ? ?";
    }

    public bool IsCharacterEliminated(CharacterID characterID)
    {
        return GetCharacterData(characterID).IsEliminated;
    }

    public int CountEliminatedCharacter()
    {
        return characterDatas.Count(c => c.IsEliminated);
    }

    public void EliminateCharacter(CharacterID characterID)
    {
        if (ManagerObj.CharacterManager.GetCharacterData(characterID) is Character character)
        {
            character.IsEliminated = true;
            ManagerObj.ScriptManager.CheckScriptCanceled();
        }

        ManagerObj.MissionManager.CheckMissionData();
    }

    public void OpenSpecialNote(CharacterID characterID, string noteID, bool isUnlocked = true)
    {
        Character character = GetCharacterData(characterID);
        SpecialNote note = character.SpecialNotes.FirstOrDefault(d => d.NoteID == noteID);

        if (note == null)
        {
            Debug.LogError($"CharacterManager : OpenSpecialNote에서 {characterID}에게 잘못된 noteID가 들어왔습니다. noteID : {noteID}");
            return;
        }

        note.IsUnlocked = isUnlocked;

        ManagerObj.MissionManager.CheckMissionData();
    }

    public bool CheckSpecialNoteOpened(CharacterID characterID, string noteID)
    {
        Character character = GetCharacterData(characterID);
        SpecialNote note = character.SpecialNotes.FirstOrDefault(d => d.NoteID == noteID);

        if (note == null)
        {
            Debug.LogError($"CharacterManager : CheckSpecialNoteOpened에서 {characterID}에게 잘못된 noteID가 들어왔습니다. noteID : {noteID}");
            return false;
        }
        else
            return note.IsUnlocked;
    }

    public Character GetLowestReputationCharacter()
    {
        return characterDatas
        ?.OrderBy(c => c.Reputation)
        .Where(c => c.CharacterID != CharacterID.Iseul && c.CharacterID != CharacterID.Gaeun)
        .FirstOrDefault();
    }

    public void InitCharacterItemExchangeInfos()
    {
        List<int> possessedItemCount = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.Character_PossessedItemCount); // 첫번째 요소는 기본 소지 개수, 두번째 요소는 1개씩 추가되는 day 주기

        foreach (Character character in characterDatas)
        {
            character.ItemExchangeInfo.CanExchange = true;
            character.ItemExchangeInfo.PossessionItems = new();

            for (int i=0;i< possessedItemCount[0] + (ManagerObj.InGameProgressManager.CurrentDay / possessedItemCount[1]); i++) // possessedItemCount를 계산해서 그 개수만큼 
            {
                List<float> gradeProbabilities = ManagerObj.DataManager.GetGameBalanceData(GameBalanceKeyCategory.Character_PossedItemGradeProbabilities);
                Item item = ManagerObj.PossessionManager.GetRandomItem(gradeProbabilities, character.UnobtainableItemIDs, character.NonPreferredItemIDs);
                character.ItemExchangeInfo.PossessionItems.Add(item);
            }
        }
    }

    GameObject characterObj;
    public CharacterController GetConfiguredCharacterController => characterObj != null && characterObj.GetComponent<CharacterController>() is CharacterController controller ? controller : null;
    public CharacterID ConfiguredCharacterID { get; set; }
    public void ConfigureCharacter(string characterID = "", Expression expression = null, SpecialEffect specialEffect = null)
    {
        if (ConfiguredCharacterID.ToString() != characterID)
        {
            GameObject loadedCharacterObj = null;
            if(Enum.TryParse(characterID, out CharacterID resultID))
                loadedCharacterObj = ManagerObj.PrefabLoader.GetPrefab(resultID);

            if (loadedCharacterObj == null) // 만일 로드된 캐릭터 오브젝트가 없다면 종료
                return;

            DisableCharacterObj();
            characterObj = loadedCharacterObj; // 로드된 캐릭터 오브젝트 등록
            ConfiguredCharacterID = resultID;
            SetCharacterExpression(new Expression("Normal", "Normal", "Normal")); // 처음에 캐릭터 띄울 때에는 기본 표정으로 만들기
        }

        SetCharacterExpression(expression);
        SetCharacterSpecialEffect(specialEffect);
    }

    public void ConfigureCharacter(CharacterID characterID, Expression expression = null, SpecialEffect specialEffect = null)
    {
        if(characterID == CharacterID.None)
        {
            DisableCharacterObj();
            return;
        }

        ConfigureCharacter(characterID.ToString(), expression, specialEffect);
    }

    public void DisableCharacterObj() // 디알로그이벤트에서 캐릭터 끄는 기능을 구현하기 위해서 따로 함수로 구현
    {
        Destroy(characterObj); // 기존 캐릭터 오브젝트 삭제
        ConfiguredCharacterID = CharacterID.None;
    }

    public void SetCharacterExpression(Expression expression)
    {
        if (IsCharacterLoaded && expression != null)
            characterObj.GetComponent<CharacterController>().SetExpression(expression);
    }

    public void SetCharacterSpecialEffect(SpecialEffect specialEffect)
    {
        if (IsCharacterLoaded && specialEffect != null)
            characterObj.GetComponent<CharacterController>().SetSpecialEffect(specialEffect);
    }

    public void AdjustCharacterScale(float scale)
    {
        if (IsCharacterLoaded)
            characterObj.transform.localScale = new Vector2(scale, scale);
    }

    public void MoveCharacterMouth(TMP_Text textArea)
    {
        if (IsCharacterLoaded)
            StartCoroutine(characterObj.GetComponent<CharacterController>().MoveMouth(textArea));
    }

    public Coroutine EnableCharacterShadow(bool disableBody, float duration)
    {
        if (IsCharacterLoaded)
            return StartCoroutine(characterObj.GetComponent<CharacterController>().EnableCharacterShadow(disableBody, duration));
        else
        {
            Debug.LogError($"CharacterManager의 EnableCharacterShadow : 캐릭터가 로드되지 않았습니다.");
            return null;
        }
    }

    public Coroutine DisableCharacterShadow(bool disableBody, float duration)
    {
        if (IsCharacterLoaded)
            return StartCoroutine(characterObj.GetComponent<CharacterController>().DisableCharacterShadow(disableBody, duration));
        else
        {
            return null;
        }
    }

    public CharacterID ParseStringToCharacterID(string inputID)
    {
        if (Enum.TryParse(inputID, ignoreCase: true, out CharacterID characterID))
        {
            return characterID;
        }
        else
        {
            Debug.Log($"CharacterManager : ParseStringToCharacterID에서 inputID에 characterID 외에 다른 값이 들어왔습니다. inputID : {inputID}");
            return CharacterID.None;
        }
    }

    public bool IsCharacterShadowing
    {
        get {
            if (IsCharacterLoaded)
                return characterObj.GetComponent<CharacterController>().IsCharacterShadowing;
            else
            {
                Debug.Log("IsCharacterShadowing : characterObj가 설정되어 있지 않습니다.");
                return false;
            }
        }
    }

    public void UpdateCashReward(CharacterID characterID, int value)
    {
        GetCharacterData(characterID).CashReward += value;

        ManagerObj.MissionManager.CheckMissionData();
    }

    [SerializeField] int maxReliabilityValue = 100;
    public int MaxReliabilityValue => maxReliabilityValue;
    public void UpdateReliability(CharacterID characterID, int value)
    {
        if (ManagerObj.CharacterManager.GetCharacterData(characterID) is Character character && CanReliabilityUpdate(characterID)) 
        {
            value += ManagerObj.PossessionManager.GetBadgeInherentEffect_Int(InherentEffectBadgeCategory.TheWitch);
            character.Reliability.Value = Mathf.Clamp(character.Reliability.Value + value, 0, maxReliabilityValue);

            List<int> reliabilityStandard = GetReliabilityStandard;
            int reliabilityValue = character.Reliability.Value;
            if (CanTrustElevation(character, reliabilityStandard[3])) // 신뢰 조건 : 여기에 조건 추가해주기
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Trust;
            else if (reliabilityValue > reliabilityStandard[2])
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Favorable;
            else if (reliabilityValue > reliabilityStandard[1])
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Indifference;
            else if (reliabilityValue > reliabilityStandard[0])
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Suspicion;
            else
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Mistrust;

            ManagerObj.ScriptManager.MatchConversationTopicScripts();
            ManagerObj.ScriptManager.CheckScriptCanceled();
        }

        ManagerObj.MissionManager.UpdateAcquireCondition(MissionGoalCategory.GetReliability, value);
    }

    public List<int> GetReliabilityStandard => ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.ReliabilityStandard); // 신뢰도 기준 : (불신/의심, 의심/무관심, 무관심/호의적, 호의적/신뢰)

    public void AllowTrustElevation(CharacterID characterID)
    {
        if(ManagerObj.CharacterManager.GetCharacterData(characterID) is Character character)
        {
            character.Reliability.CanElevateToTrust = true;

            if(CanTrustElevation(character))
                character.Reliability.ReliabilityCategory = ReliabilityCategory.Trust;
        }
        else
        {
            Debug.LogError($"AllowTrustElevation : 정확한 characterID를 넘겨주세요 characterID : {characterID}");
        }
    }

    bool CanTrustElevation(Character character, int trustStandard = -1)
    {
        if(trustStandard == -1) // 신뢰 기준값을 못받은 경우 직접 설정
            trustStandard = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.ReliabilityStandard)[3]; // 신뢰도 기준 : (불신/의심, 의심/무관심, 무관심/호의적, 호의적/신뢰)

        return character.Reliability.Value > trustStandard && character.Reliability.CanElevateToTrust; // 신뢰 조건 : 여기에 조건 추가해주기
    }

    public int GetCharacterReliabilityBlockValue(CharacterID characterID)
    {
        return 0;
    }

    public List<CharacterID> ParseCharactersStr(string charactersStr)
    {
        string[] _characterStrs = charactersStr.Split(",");
        List<CharacterID> parsedCharacterIDs = new();
        foreach (string _characterStr in _characterStrs)
        {
            if (string.IsNullOrEmpty(_characterStr)) continue;

            CharacterID parsedCharacterID = ParseStringToCharacterID(_characterStr);
            if (parsedCharacterID != CharacterID.None)
                parsedCharacterIDs.Add(parsedCharacterID);
            else
                Debug.LogError($"CharacterManager : ParseCharactersStr에서 _characterStrs 요소 중 CharacterID로 치환 안되는 요소가 있습니다. 전달받은 _characterStr : {_characterStr}");
        }
        return parsedCharacterIDs;
    }

    public bool IsRequiredCharacterEliminated(List<CharacterID> requiredCharacterIDs)
    {
        foreach (CharacterID requiredCharactger in requiredCharacterIDs) // 플레이하는데 필수인 캐릭터가 없으면 플레이하지 않는다.
        {
            if (requiredCharactger != CharacterID.None && ManagerObj.CharacterManager.IsCharacterEliminated(requiredCharactger))
                return true;
        }

        return false;
    }

    [SerializeField] float maxReputationValue = 10;
    public float MaxReputationValue => maxReputationValue;
    public void UpdateReputation(CharacterID characterID, float value)
    {
        if (ManagerObj.CharacterManager.GetCharacterData(characterID) is Character character && CanUpdate(characterID))
        {
            character.Reputation = Mathf.Clamp(character.Reputation + value, 0, maxReputationValue);
        }
    }

    public int GetCharacterReputationBlockValue(CharacterID characterID)
    {
        return 0;
    }

    public bool IsCharacterLoaded
    {
        get { return characterObj != null && characterObj.GetComponent<CharacterController>() != null; }
    }

    public CharacterID GetLoadedCharacterID
    {
        get { 
            if (IsCharacterLoaded) return characterObj.GetComponent<CharacterController>().CharacterID;
            else return CharacterID.None;
        }
    }

    public bool CanReliabilityUpdate(CharacterID characterID) // 신뢰/불신의 경우 조정X
    {
        Character character = GetCharacterData(characterID);
        if (!CanUpdate(characterID)
            || character.Reliability.ReliabilityCategory == ReliabilityCategory.Trust
            || character.Reliability.ReliabilityCategory == ReliabilityCategory.Mistrust)  // 신뢰도가 신뢰/불신의 경우 신뢰도 고정
        {
            return false;
        }

        return true;
    }

    public bool CanUpdate(CharacterID characterID)
    {
        Character character = GetCharacterData(characterID);
        if (character == null)
        {
            Debug.LogError($"CharacterManager : CanReliabilityUpdate에서 characterID가 none입니다.");
            return false;
        }
        else if (character.IsEliminated) // 탈락했으면 조정 X
        {
            return false;
        }

        return true;
    }
}
