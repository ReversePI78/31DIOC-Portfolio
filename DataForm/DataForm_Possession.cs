using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public abstract class Possession
{
    protected string possessionID;
    protected string identifier;
    protected bool conditional;
    protected List<string> effect_Added;
    protected List<string> effect_DayStart;
    protected List<string> effect_Removed;
    protected string possessionName;
    protected string possessionDescription;

    public abstract List<string> GetEffect(PossessionEffectCategory possessionEffectCategory);

    public abstract Possession Clone();
    public string PossessionID { get => possessionID; set => possessionID = value; }
    public string Identifier { get => identifier; set => identifier = value; }
    public bool Conditional { get => conditional; set => conditional = value; }

    // Effect들은 데이터 저장/로드 를 위해서 Set 꼭 필요함!
    public List<string> Effect_Added { get => effect_Added; set => effect_Added = value; } 
    public List<string> Effect_DayStart { get => effect_DayStart; set => effect_DayStart = value; }
    public List<string> Effect_Removed { get => effect_Removed; set => effect_Removed = value; }
    public string PossessionName { get => possessionName; set => possessionName = value; }
    public string PossessionDescription { get => possessionDescription; set => possessionDescription = value; }

    protected Possession SetBasicCloneInfo(Possession possession)
    {
        possession.possessionID = this.PossessionID;
        possession.identifier = ManagerObj.DataManager.GetRandomKey;

        possession.effect_Added = new List<string>(this.effect_Added);
        possession.effect_DayStart = new List<string>(this.effect_DayStart);
        possession.effect_Removed = new List<string>(this.effect_Removed);

        possession.possessionName = this.possessionName;
        possession.possessionDescription = this.possessionDescription;

        return possession;
    }

    protected virtual List<string> ParseEffect(string effectStr)
    {
        return !string.IsNullOrEmpty(effectStr) ? effectStr.Split("/").ToList() : new();
    }

    public override bool Equals(object obj)
    {
        if (obj is Possession other)
        {
            return this.possessionID == other.possessionID &&
                   this.identifier == other.identifier;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(possessionID, identifier);
    }

    public static bool operator ==(Possession left, Possession right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Possession left, Possession right)
    {
        return !(left == right);
    }
}

public class Item : Possession
{
    Item_Grade grade;
    Item_Type type;

    List<string> effect_Use;

    public override List<string> GetEffect(PossessionEffectCategory possessionEffectCategory)
    {
        switch (possessionEffectCategory)
        {
            case PossessionEffectCategory.Added: return effect_Added;
            case PossessionEffectCategory.DayStart: return effect_DayStart;
            case PossessionEffectCategory.Removed: return effect_Removed;
            case PossessionEffectCategory.Use: return effect_Use;
            default:
                UnityEngine.Debug.LogError($"Item :GetEffect에서 잘못된 possessionEffectCategory가 들어왔습니다. possessionEffectCategory : {possessionEffectCategory}");
                return new List<string>();
        }
    }

    public Item(string _itemID, string _conditional, string _grade, string _type, string _effect_Added, string _effect_DayStart, string _effect_Removed, string _effect_Use, string _name, string _description)
    {
        possessionID = _itemID;

        if(!bool.TryParse(_conditional, out conditional))
        {
            UnityEngine.Debug.LogError($"Item 생성자에서 _conditional를 bool로 tryParse하는데 실패했습니다.(false 적재) 전달받은 itemID : {_itemID} _conditional :{_conditional}");
            conditional = false;
        }

        if(!Enum.TryParse<Item_Grade>(_grade, true, out grade))
        {
            UnityEngine.Debug.LogError($"Item 생성자에서 _grade를 Item_Grade로 tryParse하는데 실패했습니다.(normal 적재) 전달받은 itemID : {_itemID} _grade :{_grade}");
            grade = Item_Grade.Normal;
        }

        if (!Enum.TryParse<Item_Type>(_type, true, out type))
        {
            UnityEngine.Debug.LogError($"Item 생성자에서 _type를 Item_Type로 tryParse하는데 실패했습니다.(common 적재) 전달받은 itemID : {_itemID} _type :{_type}");
            type = Item_Type.Common;
        }

        effect_Added = ParseEffect(_effect_Added);
        effect_DayStart = ParseEffect(_effect_DayStart);
        effect_Removed = ParseEffect(_effect_Removed);
        effect_Use = ParseEffect(_effect_Use);

        possessionName = _name;
        possessionDescription = _description;
    }

    public Item() { }

    public override Possession Clone()
    {
        Item clone = (Item)SetBasicCloneInfo(new Item());

        clone.grade = this.Grade;
        clone.type = this.Type;
        clone.effect_Use = new List<string>(this.effect_Use);

        return clone;
    }

    public Item_Grade Grade { get => grade; set => grade = value; }
    public Item_Type Type { get => type; set => type = value; }

    public List<string> Effect_Use { get => effect_Use; set => effect_Use = value; }
}

public class Badge : Possession
{
    int currentLevel;
    int maxLevel;
    Badge_Category category;

    List<string> effect_Inherent;

    public override List<string> GetEffect(PossessionEffectCategory possessionEffectCategory)
    {
        List<string> selectedEffect = null;
        switch (possessionEffectCategory)
        {
            case PossessionEffectCategory.Added: selectedEffect = effect_Added; break;
            case PossessionEffectCategory.DayStart: selectedEffect = effect_DayStart; break;
            case PossessionEffectCategory.Removed: selectedEffect = effect_Removed; break;
            default:
                UnityEngine.Debug.LogError($"Badge :GetEffect에서 잘못된 possessionEffectCategory가 들어왔습니다. possessionEffectCategory : {possessionEffectCategory}");
                selectedEffect = new();
                break;
        }

        return GetEffectContentByLevel(selectedEffect).Split('/').Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    public object GetInherentEffect()
    {
        string content = GetEffectContentByLevel(effect_Inherent);

        if (int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            return i;

        if (float.TryParse(content, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return f;

        if (content is string str && !string.IsNullOrEmpty(str))
            return str;

        return null;
    }

    string GetEffectContentByLevel(List<string> selectedEffect)
    {
        if (selectedEffect == null || selectedEffect.Count == 0)
            return "";

        int index = currentLevel - 1;

        if (index < 0)
        {
            UnityEngine.Debug.LogError($"Badge :GetEffect에서 currentLevel - 1이 selectedEffect의 최소 인덱스 값보다 작습니다. effect.Count : {selectedEffect.Count} badgeLevel-1 : {currentLevel - 1}");
            index = 0;
        }
        else if (index >= selectedEffect.Count)
        {
            UnityEngine.Debug.LogError($"Badge :GetEffect에서 currentLevel - 1이 selectedEffect의 최대 인덱스 값보다 큽니다. effect.Count : {selectedEffect.Count} badgeLevel-1 : {currentLevel - 1}");
            index = selectedEffect.Count - 1;
        }

        return selectedEffect[index];
    }

    protected override List<string> ParseEffect(string effectStr)
    {
        return PlaceholderResolver.PlaceholderRegex.Matches(effectStr)
    .Cast<Match>()
    .Select(m => m.Groups[1].Value)
    .ToList();
    }

    public Badge(string _badgeID, string _category, string _conditional, string _maxLevel, string _effect_Added, string _effect_DayStart, string _effect_Removed, string _effect_Inherent, string _name, string _description)
    {
        possessionID = _badgeID;

        currentLevel = 1;

        if (!bool.TryParse(_conditional, out conditional))
        {
            UnityEngine.Debug.LogError($"Badge 생성자에서 _conditional를 bool로 tryParse하는데 실패했습니다.(false 적재) 전달받은 badgeID : {_badgeID} _conditional :{_conditional}");
            conditional = false;
        }

        if (!Enum.TryParse<Badge_Category>(_category, true, out category))
        {
            UnityEngine.Debug.LogError($"Badge 생성자에서 _conditional를 Badge_Category로 tryParse하는데 실패했습니다.(Benefit 적재) 전달받은 badgeID : {_badgeID} _category :{_category}");
            category = Badge_Category.Benefit;
        }

        if (!int.TryParse(_maxLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxLevel))
        {
            UnityEngine.Debug.LogError($"Badge 생성자에서 _maxLevel을 int로 tryParse하는데 실패했습니다.(maxLevel = 1 적재) 전달받은 badgeID : {_badgeID} _maxLevel :{_maxLevel}");
            maxLevel = 1;
        }

        effect_Added = ParseEffect(_effect_Added);
        effect_DayStart = ParseEffect(_effect_DayStart);
        effect_Removed = ParseEffect(_effect_Removed);

        effect_Inherent = ParseEffect(_effect_Inherent);
        if (!string.IsNullOrEmpty(_effect_Inherent) && !Enum.TryParse(_badgeID, true, out InherentEffectBadgeCategory ieb)) // InherentEffect가 enum에 설정되어 있는지 체크용 if문 (없어도 effect_Inherent에는 빈 리스트 들어감)
        {
            UnityEngine.Debug.LogError($"Badge 정보 생성 도중, Effect_Inherent가 설정되었지만 InherentEffectBadgeCategory에 기재되지 않은 배지가 있습니다. BadgeID : {_badgeID}");
        }

        possessionName = _name;
        possessionDescription = _description;
    }

    public Badge() { }

    public override Possession Clone()
    {
        Badge clone = (Badge)SetBasicCloneInfo(new Badge());

        clone.currentLevel = this.currentLevel;
        clone.maxLevel = this.maxLevel;
        clone.category = this.Category;
        clone.effect_Inherent = new List<string>(this.effect_Inherent);

        return clone;
    }

    public int CurrentLevel { get => currentLevel; set => currentLevel = value; }
    public int MaxLevel { get => maxLevel; set => maxLevel = value; }
    public Badge_Category Category { get => category; set => category = value; }

    public List<string> Effect_Inherent { get => effect_Inherent; set => effect_Inherent = value; }
}