using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.U2D.Animation;
#endif

public class ScriptInfo
{
    protected string scriptID;
    protected List<string> playCondition;
    protected List<string> cancelCondition;

    public ScriptInfo(string _scriptID, List<string> playConditions, List<string> cancelConditions)
    {
        scriptID = _scriptID;
        playCondition = playConditions;

        cancelCondition = cancelConditions;
    }

    public ScriptInfo() { }

    public string ScriptID { get => scriptID; set => scriptID = value; }
    public List<string> PlayCondition { get => playCondition; set => playCondition = value; }
    public List<string> CancelCondition => cancelCondition; 
}

public class EventScriptInfo : ScriptInfo
{
    ScriptCategory eventCategory;
    CharacterID initialCharacter;
    FacilityID initialFacility;
    bool blockIfPlayed;
    bool blockIfCompleted;
    List<CharacterID> requiredCharacters;

    public EventScriptInfo(string _scriptID, ScriptCategory _eventCategory, string _initialCharacter, string _initialFacility, string _blockIfPlayed, string _blockIfCompleted, string requiredCharactersStr, List<string> playConditions, List<string> cancelConditions) : base(_scriptID, playConditions, cancelConditions)
    {
        eventCategory = _eventCategory;

        if (!Enum.TryParse<CharacterID>(_initialCharacter, true, out initialCharacter))
        {
            Debug.LogError($"EventScriptInfo : 생성자에서 _initialCharacter를 CharacterID로 파싱하는데 실패했습니다. (None 적재) _initialCharacter : {_initialCharacter}");
            initialCharacter = CharacterID.None;
        }

        if (!Enum.TryParse<FacilityID>(_initialFacility, true, out initialFacility))
        {
            Debug.LogError($"EventScriptInfo : 생성자에서 _initialFacility를 FacilityID로 파싱하는데 실패했습니다. (Lobby 적재) _initialFacility : {_initialFacility}");
            initialFacility = FacilityID.Lobby;
        }

        if (!bool.TryParse(_blockIfPlayed, out blockIfPlayed))
        {
            Debug.LogError($"EventScriptInfo : 생성자에서 _blockIfPlayed bool로 파싱하는데 실패했습니다. (true 적재) _blockIfPlayed : {_blockIfPlayed}");
            blockIfPlayed = true;
        }

        if (!bool.TryParse(_blockIfCompleted, out blockIfCompleted))
        {
            Debug.LogError($"EventScriptInfo : 생성자에서 _blockIfCompleted bool로 파싱하는데 실패했습니다. (true 적재) _blockIfCompleted : {_blockIfCompleted}");
            blockIfCompleted = true;
        }

        requiredCharacters = ManagerObj.CharacterManager.ParseCharactersStr(requiredCharactersStr);

        /*
        requiredCharacters = new();
        string[] _requiredCharacters = requiredCharactersStr.Split("/");
        foreach (string _requiredCharacter in _requiredCharacters)
        {
            if (string.IsNullOrEmpty(_requiredCharacter)) continue;

            CharacterID requiredCharacter = ManagerObj.CharacterManager.ParseStringToCharacterID(_requiredCharacter);
            if (requiredCharacter != CharacterID.None)
                requiredCharacters.Add(requiredCharacter);
            else
                Debug.LogError($"EventScriptInfo : 생성자에서 requiredCharactersStr 요소 중 CharacterID로 치환 안되는 요소가 있습니다. 전달받은 _requiredCharacter : {_requiredCharacter}");
        }
        */
    }

    public EventScriptInfo() { }

    public ScriptCategory EventCategory => eventCategory;
    public CharacterID InitialCharacter => initialCharacter;
    public FacilityID InitialFacility => initialFacility;
    public bool BlockIfPlayed => blockIfPlayed;
    public bool BlockIfCompleted => blockIfCompleted;
    public List<CharacterID> RequiredCharacters => requiredCharacters;

    public static EventScriptInfo GetGameOverScriptInfo { get
        {
            EventScriptInfo returnV = new();
            returnV.scriptID = "GameOver_LifeStatDepleted";
            returnV.playCondition = new();
            returnV.eventCategory = ScriptCategory.Etc;
            returnV.initialCharacter = ManagerObj.InGameProgressManager.VisitingCharacter == null ? CharacterID.None : ManagerObj.InGameProgressManager.VisitingCharacter.CharacterID;
            returnV.initialFacility = ManagerObj.InGameProgressManager.VisitingFacility == null ? FacilityID.Lobby : ManagerObj.InGameProgressManager.VisitingFacility.FacilityID;
            return returnV;
        }
    }
}

public class ScriptRequest
{
    string scriptID;
    Enum[] labels;

    public ScriptRequest(string scriptID, params Enum[] labels)
    {
        this.scriptID = scriptID;
        this.labels = labels;
    }

    public ScriptRequest() // 직렬화/역직렬화 할 때 매개변수가 없는 생성자가 필요함
    {
    }

    public static ScriptRequest GetScriptRequest(string scriptID, Enum categoryLabel)
    {
        if (categoryLabel is CharacterID characterID)
            return new ScriptRequest(scriptID, ScriptCategory.Script, AddressableLabelCategory.Data, ScriptCategory.ConversationTopic, characterID);
        else if (categoryLabel is FacilityID facilityID)
            return new ScriptRequest(scriptID, ScriptCategory.Script, AddressableLabelCategory.Data, ScriptCategory.ConversationTopic, facilityID);
        else if (categoryLabel is ScriptCategory eventCategory)
        {
            if(eventCategory == ScriptCategory.Etc)
                return new ScriptRequest(scriptID, ScriptCategory.Script, AddressableLabelCategory.Data, ScriptCategory.Etc);
            else
                return new ScriptRequest(scriptID, ScriptCategory.Script, AddressableLabelCategory.Data, ScriptCategory.Event, eventCategory);
        }
        else if (categoryLabel is SceneCategory seneCategory && seneCategory == SceneCategory.CutScene) // 컷씬의 경우
            return new ScriptRequest(scriptID, SceneCategory.CutScene, ScriptCategory.Script, AddressableLabelCategory.Data);
        else
        {
            Debug.LogError($"ScriptRequest : GetScriptRequest 에서 categoryLabel이 올바르지 않습니다. categoryLabel 타입/값 : {categoryLabel.GetType()}/{categoryLabel.ToString()}");
            return null;
        }
    }

    public static ScriptRequest GetScriptRequest(string scriptID, string categoryStr)
    {
        Enum categoryLabel = null;
        if (Enum.TryParse<CharacterID>(categoryStr, true, out CharacterID characterID))
            categoryLabel = characterID;
        else if (Enum.TryParse<FacilityID>(categoryStr, true, out FacilityID facilityID))
            categoryLabel = facilityID;
        else if (Enum.TryParse<ScriptCategory>(categoryStr, true, out ScriptCategory eventCategory))
            categoryLabel = eventCategory;
        else
        {
            Debug.LogError($"ScriptRequest : GetScriptRequest 에서 string categoryStr을 적절한 Enum으로 파싱하지 못했습니다.이 올바르지 않습니다. categoryStr : {categoryStr}");
            return null;
        }

        return GetScriptRequest(scriptID, categoryLabel);
    }

    public static Enum[] GetConversationTopicLabels(SceneCategory sceneCategory)
    {
        if (sceneCategory == SceneCategory.CutScene) // 컷씬의 경우
            return new Enum[] { SceneCategory.CutScene, ScriptCategory.Script, AddressableLabelCategory.Data };
        else
        {
            Debug.LogError($"ScriptRequest : GetConversationTopicLabels(SceneCategory sceneCategory)에서 Cutscene외에 다른 sceneCategory가 들어왔습니다. sceneCategory 타입/값 : {sceneCategory.GetType()}/{sceneCategory}");
            return null;
        }
    }

    public string ScriptID {
        get => scriptID;
        set => scriptID = value;
    }

    [JsonIgnore] // Labels는 따로 직렬화/역직렬화 해준다.
    public Enum[] Labels => labels;

    public static readonly Dictionary<string, Type> EnumTypeMap = new()
    {
        { typeof(CharacterID).Name, typeof(CharacterID) },
        { typeof(FacilityID).Name, typeof(FacilityID) },
        { typeof(AddressableLabelCategory).Name, typeof(AddressableLabelCategory) },
        { typeof(ScriptCategory).Name, typeof(ScriptCategory) },
        { typeof(SceneCategory).Name, typeof(SceneCategory) }
    };

    [JsonProperty("Labels")] // 직렬화/역직렬화용 프록시
    private List<EnumRecord> LabelsForSerialization
    {
        get => labels?.Select(e => new EnumRecord
        {
            TypeName = e.GetType().Name,
            Value = e.ToString()
        }).ToList();
        set => labels = value?.Select(e => e.ToEnum()).ToArray();
    }

    public bool CheckLabelsContainThisRequest(List<string> labels)
    {
        List<string> stringLabels = this.labels.Select(e => e.ToString()).ToList();
        var stringLabelSet = new HashSet<string>(stringLabels);
        bool isSubset = labels.All(l => stringLabelSet.Contains(l));

        return isSubset;
    }

    public override bool Equals(object obj)
    {
        if (obj is not ScriptRequest other)
            return false;

        if (scriptID != other.scriptID)
            return false;

        // null 또는 빈 배열 처리
        if ((labels == null || labels.Length == 0) &&
            (other.labels == null || other.labels.Length == 0))
            return true; // 둘 다 null/empty면 같다

        if (labels == null || other.labels == null)
            return false;

        // 집합 비교 (순서 무시, 중복 무시)
        var thisSet = new HashSet<Enum>(labels);
        var otherSet = new HashSet<Enum>(other.labels);

        return thisSet.SetEquals(otherSet);
    }


    public override int GetHashCode()
    {
        int hash = scriptID?.GetHashCode() ?? 0;

        foreach (var label in labels.Distinct().OrderBy(l => l.ToString()))
        {
            hash ^= label?.GetHashCode() ?? 0;
        }

        return hash;
    }

    public static bool operator ==(ScriptRequest a, ScriptRequest b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(ScriptRequest a, ScriptRequest b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        string str = $"ScriptID : {ScriptID} Labels :";

        foreach (Enum label in labels)
            str += label.ToString();

        return str;
    }

    // 내부 직렬화 구조
    private class EnumRecord
    {
        public string TypeName;
        public string Value;

        public Enum ToEnum()
        {
            if (EnumTypeMap.TryGetValue(TypeName, out var type))
            {
                return (Enum)Enum.Parse(type, Value);
            }
            throw new JsonSerializationException($"Unknown enum type: {TypeName}");
        }
    }
}

public class ReadDialogueBlockLines
{
    ScriptRequest scriptRequest;
    List<int> readBlockLines;

    public ReadDialogueBlockLines(ScriptRequest scriptRequest)
    {
        this.scriptRequest = scriptRequest;
        readBlockLines = new();
    }

    public ReadDialogueBlockLines() { } // json 직렬화/역직렬화 하기 위해서 매개변수 없는 생성자 있어야함

    public bool IsSameRequest(string scriptID, Enum scriptCategory)
    {
        return scriptRequest.ScriptID == scriptID && scriptRequest.Labels.Contains(scriptCategory);
    }

    public void AddBlockLine(int lineNum)
    {
        if (lineNum > -1 && !readBlockLines.Contains(lineNum))
            readBlockLines.Add(lineNum);
    }

    public void MergeReadBlockLines(ReadDialogueBlockLines readDialogueBlockLines)
    {
        if (!ScriptRequest.Equals(readDialogueBlockLines.ScriptRequest))
        {
            Debug.LogError($"ReadDialogueBlockLines : MergeReadBlockLines에서 매개변수로 받은 readDialogueBlockLines.ScriptRequest와 기존 scriptRequest가 다릅니다.\n기존 scriptRequest : {scriptRequest.ToString()} readDialogueBlockLines.ScriptRequest : {readDialogueBlockLines.ScriptRequest.ToString()}");
            return;
        }

        readBlockLines.AddRange(readDialogueBlockLines.ReadBlockLines.Except(readBlockLines)); // 기존 scriptRequest에 없는 line만 추가됨
        readBlockLines.Sort(); // 오름차순 정렬
    }

    public ScriptRequest ScriptRequest {get => scriptRequest; set => scriptRequest = value; }
    public List<int> ReadBlockLines { get => readBlockLines; set => readBlockLines = value; }

    public override bool Equals(object obj)
    {
        if (obj is ReadDialogueBlockLines other)
        {
            return Equals(scriptRequest, other.scriptRequest);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return scriptRequest != null ? scriptRequest.GetHashCode() : 0;
    }

    public static bool operator ==(ReadDialogueBlockLines left, ReadDialogueBlockLines right)
    {
        // 둘 다 null이면 true
        if (ReferenceEquals(left, right))
            return true;

        // 하나만 null이면 false
        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(ReadDialogueBlockLines left, ReadDialogueBlockLines right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        string str = scriptRequest.ToString();
        foreach (int n in readBlockLines)
            str += " " + n;
        return str;
    }
}

public class DialogueBlock
{
    string blockID;
    string characterID;
    List<DialogueLine> dialogueLines;

    public DialogueBlock(string _blockID, string _characterID)
    {
        blockID = _blockID;
        characterID = _characterID;
        dialogueLines = new List<DialogueLine>();
    }

    public string BlockID
    {
        get { return blockID; }
    }

    public string CharacterID
    {
        get => characterID; 
        set => characterID = value;
    }

    public List<DialogueLine> DialogueLines
    {
        get { return dialogueLines; }
        set { dialogueLines = value; }
    }

    public bool Equals(DialogueBlock other)
    {
        if (other is null) return false;
        return blockID == other.blockID && characterID == other.characterID;
    }

    public override bool Equals(object obj)
    {
        if (obj is DialogueBlock other)
            return Equals(other);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(blockID, characterID);
    }

    public static bool operator ==(DialogueBlock left, DialogueBlock right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(DialogueBlock left, DialogueBlock right)
    {
        return !(left == right);
    }
}

public class DialogueLine
{
    string dialogue;
    Expression expression;
    SpecialEffect specialEffects;
    List<string> controls;

    public DialogueLine(string _dialogue, string _eye, string _eyebrows, string _mouth, string _specialEffect_1, string _specialEffect_2, string _specialEffect_3, List<string> _controls)
    {
        dialogue = _dialogue;
        expression = new Expression(_eye, _eyebrows, _mouth);
        specialEffects = new SpecialEffect(_specialEffect_1, _specialEffect_2, _specialEffect_3);
        controls = _controls;
    }

    public string Dialogue
    {
        get { return dialogue; }
    }

    public Expression Expression
    {
        get { return expression; }
    }

    public SpecialEffect SpecialEffects
    {
        get { return specialEffects; }
    }

    public List<string> Controls
    {
        get { return controls; }
        set { controls = value; }
    }
}

public class Expression
{
    string eye;
    string eyebrows;
    string mouth;

    public Expression(string _eye, string _eyebrows, string _mouth)
    {
        eye = _eye;
        eyebrows = _eyebrows;
        mouth = _mouth;
    }

    public string Eye { get { return eye; } }
    public string Eyebrows { get { return eyebrows; } }
    public string Mouth { get { return mouth; } }
}

public class SpecialEffect
{
    string specialEffect_1;
    string specialEffect_2;
    string specialEffect_3;

    public SpecialEffect(string _specialEffect_1, string _specialEffect_2, string _specialEffect_3) {
        specialEffect_1 = _specialEffect_1;
        specialEffect_2 = _specialEffect_2;
        specialEffect_3 = _specialEffect_3;
    }

    public string SpecialEffect_1 { get { return specialEffect_1; } }
    public string SpecialEffect_2 { get { return specialEffect_2; } }
    public string SpecialEffect_3 { get { return specialEffect_3; } }
}