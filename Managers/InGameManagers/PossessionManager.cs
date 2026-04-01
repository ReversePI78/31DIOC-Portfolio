using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.IO;
using CsvHelper;
using System.Globalization;
using JetBrains.Annotations;


//using System;

#if UNITY_EDITOR
using static UnityEditor.Timeline.Actions.MenuPriority;
using static UnityEditor.Progress;
using UnityEditorInternal.VersionControl;
#endif

public class PossessionManager : InGameManager
{
    List<Item> itemDatas;
    List<Badge> badgeDatas;

    Dictionary<string, string> badgeRewardDatas;

    public override IEnumerator InitInGame()
    {
        itemDatas = new();
        List<string> itemSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.ItemData] as List<string>;

        foreach (string itemSource in itemSources) 
        {
            using (var reader = new StringReader(itemSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "ItemID" 이름으로 접근 가능)

                while (csv.Read())
                {
                    itemDatas.Add(new Item(
                        csv.GetField("ItemID"),
                        csv.GetField("Conditional"),
                        csv.GetField("Grade"),
                        csv.GetField("Type"),
                        csv.GetField("Effect_Added"),
                        csv.GetField("Effect_DayStart"),
                        csv.GetField("Effect_Removed"),
                        csv.GetField("Effect_Use"),

                        csv.GetField($"Name_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n"),
                        csv.GetField($"Description_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n")
                        ));
                }
            }
        }

        badgeDatas = new();
        List<string> badgeSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.BadgeData] as List<string>;

        foreach (string badgeSource in badgeSources)
        {
            using (var reader = new StringReader(badgeSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "BadgeID" 이름으로 접근 가능)

                while (csv.Read())
                {
                    badgeDatas.Add(new Badge(
                        csv.GetField("BadgeID"),
                        csv.GetField("Category"),
                        csv.GetField("Conditional"),
                        csv.GetField("MaxLevel"),
                        csv.GetField("Effect_Added"),
                        csv.GetField("Effect_DayStart"),
                        csv.GetField("Effect_Removed"),
                        csv.GetField("Effect_Inherent"),

                        csv.GetField($"Name_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n"),
                        csv.GetField($"Description_{ManagerObj.OptionManager.GetLanguageSetting()}").Replace("\\n", "\n")
                        ));
                }
            }
        }

        badgeRewardDatas = new();
        List<string> badgeRewardInfos = ManagerObj.DataManager.StaticDatas[StaticDataCategory.BadgeRewardInfos] as List<string>;
        foreach (string badgeRewardInfo in badgeRewardInfos)
        {
            using (var reader = new StringReader(badgeRewardInfo))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "Key, BadgeID" 이름으로 접근 가능)

                while (csv.Read())
                {
                    string key = csv.GetField("Key"), badgeID = csv.GetField("BadgeID");
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(badgeID)) // Key와 BadgeID가 모두 Null이 아니면
                    {
                        badgeRewardDatas[key] = badgeID;
                    }
                }
            }
        }

        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        itemDatas = null;
        badgeDatas = null;

        yield return null;
    }
    
    private void Awake()
    {
        inventory = new();
        badgeCase = new();
    }

    List<Item> inventory;
    public List<Item> Inventory { get => inventory; set => inventory = value; }
    public List<Item> TempItemList
    {
        get => ManagerObj.InGameProgressManager.TempItemList;
        set => ManagerObj.InGameProgressManager.TempItemList = value;
    }
    public int CurrentMaxInventorySize => 5 + GetBadgeInherentEffect_Int(InherentEffectBadgeCategory.MasterOfOrganization);

    List<Badge> badgeCase;
    public List<Badge> BadgeCase { get => badgeCase; set => badgeCase = value; }

    public void AddPossessionOnStorage<T>(T addedPossession) where T : Possession
    {
        if (addedPossession == null)
            return;

        if(addedPossession is Item addedItem)
        {
            if (!CanAddItem(new List<Item> { addedItem })) // 만일 추가할 수 없는 상황이라면 return
                return;

            inventory.Add(addedItem);
            CheckItemUpgrade(); // 추가 효과 적용 후 아이템 업그레이드 체크

            ManagerObj.MissionManager.UpdateAcquireCondition(MissionGoalCategory.CountItemAcquisitions, 1);
        }
        else if(addedPossession is Badge addedBadge)
        {
            foreach (Badge badge in badgeCase) // 새로 얻은 배지가 기존에 얻은 배지인지 확인한다.
            {
                if(badge.PossessionID == addedBadge.PossessionID) // 기존에 얻은 배지일 경우, MaxLevel에 도달하지 않았으면 레벨을 올려주고 종료
                {
                    if (badge.CurrentLevel < badge.MaxLevel)
                    {
                        badge.CurrentLevel++;
                    }
                    return;
                }
            }

            badgeCase.Add(addedBadge); // 기존에 없던 배지일 경우, 새로 추가해준다.
        }
        ManagerObj.EffectDispatcher.SetupDispatch(addedPossession.GetEffect(PossessionEffectCategory.Added));
    }

    public void AddPossessionOnStorage(string possessionID)
    {
        Item addedItem = GetItemClone(possessionID);
        AddPossessionOnStorage(addedItem); // 만일 addedItem == null 이라면 함수에서 걸러짐

        Badge addedBadge = GetBadgeClone(possessionID);
        AddPossessionOnStorage(addedBadge); // 만일  addedBadge == null 이라면 함수에서 걸러짐
    }

    public void AddPossessionsOnStorage<T>(List<T> addedPossessions) where T: Possession
    {
        if (addedPossessions is List<Item> addedItems && !CanAddItem(addedItems)) // 만일 추가할 수 없는 상황이라면 return
            return;

        foreach (Possession possession in addedPossessions)
        {
            AddPossessionOnStorage(possession);
        }
    }

    public void ApplyDayStartEffect()
    {
        ManagerObj.StatusManager.BlockGameOverWhenLifeDepleted = true;
        ManagerObj.PossessionManager.BlockShowOpenInventoryEditorWindow = true;

        foreach (Item item in inventory)
        {
            ManagerObj.EffectDispatcher.SetupDispatch(item.GetEffect(PossessionEffectCategory.DayStart));
        }

        foreach (Badge badge in badgeCase)
        {
            ManagerObj.EffectDispatcher.SetupDispatch(badge.GetEffect(PossessionEffectCategory.DayStart));
        }

        ManagerObj.StatusManager.BlockGameOverWhenLifeDepleted = false;
        ManagerObj.PossessionManager.BlockShowOpenInventoryEditorWindow = false;
    }

    public void RemovePossessionOnStorage<T>(T removedPossession) where T : Possession
    {
        if (removedPossession is Item removedItem)
        {
            inventory.Remove(removedItem);
        }
        else if (removedPossession is Badge removedBadge)
        {
            badgeCase.Remove(removedBadge);

            for (int i = inventory.Count - 1; i >= CurrentMaxInventorySize; i--)
            {
                RemovePossessionOnStorage(inventory[i]);
            }
        }

        ManagerObj.EffectDispatcher.SetupDispatch(removedPossession.GetEffect(PossessionEffectCategory.Removed));
    }

    public void RemovePossessionsOnStorage<T>(List<T> removedPossessions) where T : Possession
    {
        foreach (Possession possession in removedPossessions)
            RemovePossessionOnStorage(possession);
    }

    public void RemoveRandomPossessioinsOnStorage<T>(int count = -1) where T : Possession
    {
        for (int i = 0; i < (count < 0 ? count * -1 : count); i++)
        {
            if (typeof(T) == typeof(Item) && inventory.Count > 0)
            {
                RemovePossessionOnStorage<Item>(inventory[UnityEngine.Random.Range(0, inventory.Count)]);
            }
            else if (typeof(T) == typeof(Badge))
            {
                List<Badge> benefitBadges = badgeCase.Where(b => b.Category == Badge_Category.Benefit).ToList();
                if(benefitBadges.Count > 0)
                    RemovePossessionOnStorage<Badge>(benefitBadges[UnityEngine.Random.Range(0, benefitBadges.Count)]);
            }
        }
    }

    public void RemoveBadgeByID(string badgeID)
    {
        foreach (Badge badge in ManagerObj.PossessionManager.BadgeCase)
        {
            if (badge.PossessionID == badgeID)
            {
                if (badge.CurrentLevel > 1)
                    badge.CurrentLevel--;
                else
                    ManagerObj.PossessionManager.RemovePossessionOnStorage(badge);

                return;
            }
        }
    }

    public int GetBadgeInherentEffect_Int(InherentEffectBadgeCategory badgeCategory)
    {
        foreach (Badge badge in badgeCase)
        {
            if (badge.PossessionID == badgeCategory.ToString()) // 들어온 고유 효과 요청 배지와 동일한 아이디의 배지가 있는지 체크
            {
                object value = badge.GetInherentEffect();
                if (value == null)
                    return 0;

                if (value is int i)
                    return i;
                else if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                    return result;
                else
                {
                    Debug.LogError($"value에 int로 파싱할 수 없는 값이 들어왔습니다.(0 반환) value : {value}");
                    return 0;
                }
            }
        }

        return 0;
    }

    public List<float> GetBadgeInherentEffect_FloatList(InherentEffectBadgeCategory badgeCategory)
    {
        var result = new List<float>();

        foreach (Badge badge in badgeCase)
        {
            if (badge.PossessionID == badgeCategory.ToString()) // 들어온 고유 효과 요청 배지와 동일한 아이디의 배지가 있는지 체크
            {
                object value = badge.GetInherentEffect();
                if (value != null && value is string str && !string.IsNullOrWhiteSpace(str))
                {
                    foreach (var part in str.Split(','))
                    {
                        if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                        {
                            result.Add(f);
                        }
                        else
                        {
                            Debug.LogError($"PossessionManager : GetBadgeInherentEffect_FloatList에서 ,로 나눈 요소 중 float로 파싱하지 못하는 데이터 값이 있습니다.(데이터엔 0이 들어감) badgeCategory : {badgeCategory}");
                            result.Add(0);
                        }
                    }
                }
            }
        }

        return result;
    }

    public string GetPossessionName(string possessionID)
    {
        Item item = GetItemInfo(possessionID);
        if (item != null) return item.PossessionName;

        Badge badge = GetBadgeInfo(possessionID);
        if (badge != null) return badge.PossessionName;

        Debug.LogError($"GetPossessionName : possessionID와 같은 이름의 Possession이 없습니다 possessionID : {possessionID}");
        return "None";
    }

    public string GetPossessionDescription(string possessionID)
    {
        Item item = GetItemInfo(possessionID);
        if (item != null) return item.PossessionDescription;

        Badge badge = GetBadgeInfo(possessionID);
        if (badge != null) return badge.PossessionDescription;

        Debug.LogError($"GetPossessionDescription : possessionID와 같은 이름의 Possession이 없습니다 possessionID : {possessionID}");
        return "None";
    }

    public void MergeTempItemList()
    {
        if(TempItemList.Count > 0)
            AddPossessionsOnStorage(new List<Item>(TempItemList)); // CanAddItem에서 tempItemList = null;이 실행되기 때문에 새로운 List<Item> 으로 얕은 복사해서 넘겨준다.
    }

    public bool BlockShowOpenInventoryEditorWindow { get; set; } // DayStart에서 한번에 하기 위해서 만든 프로퍼티
    bool CanAddItem(List<Item> addedItems)
    {
        if (inventory.Count + addedItems.Count > CurrentMaxInventorySize) // 만일 추가된 아이템의 개수와 기존 인벤토리의 아이템의 개수의 합이 최대치를 넘어가면 false 반환
        {
            TempItemList = addedItems;
            if(!ManagerObj.StatusManager.IsLifeStatDepleted && !BlockShowOpenInventoryEditorWindow)
                ManagerObj.DisplayManager.ShowWindow(new WindowInfo(WindowCategory.OpenInventoryEditor));
            ManagerObj.DataManager.SaveData();
            return false;
        }

        TempItemList.Clear(); // 임시 아이템 리스트 초기화
        return true;
    }

    void CheckItemUpgrade()
    {
        List<int> upgradeCountStandard = ManagerObj.DataManager.GetGameBalanceData_Int(GameBalanceKeyCategory.ItemUpgradeNum);
        // upgradeCountStandard는 크기가 2인 리스트
        // 첫 번째 요소가 일반 아이템 카운트 기준, 두 번째 요소가 저주 아이템 카운트 기준.
        // UpgradableItemCategory에서 일반 아이템의 enum은 0으로, 저주 아이템은 1로 설정해놓아서 (int)upgradableItemCategory로 바로 접근 가능

        foreach (UpgradableItemCategory upgradableItemCategory in System.Enum.GetValues(typeof(UpgradableItemCategory)))
        {
            if (upgradableItemCategory == UpgradableItemCategory.Upgraded) 
                continue;

            List<Item> filteredItems  = inventory.Where(item => item.PossessionID.Contains(upgradableItemCategory.ToString()) && !item.PossessionID.Contains(UpgradableItemCategory.Upgraded.ToString())).ToList();
            // UpgradableItem 키를 가지면서 Upgraded 되지 않은 아이템만 추려냄

            int standardValue = (int)upgradableItemCategory > 0 ? upgradeCountStandard[0] : upgradeCountStandard[1];
            // UpgradableItemCategory의 value들은 일반은 양수, 저주는 음수

            if (filteredItems.Count >= standardValue) // 만일 뽑아낸 아이템 리스트 
            {
                AddBadgeOnBadgeCaseFromBadgeRewardData(upgradableItemCategory.ToString()); // 아이템 완성 배지 추가
                
                foreach(Item filteredItem in filteredItems)
                {
                    inventory[inventory.IndexOf(filteredItem)] = GetItemClone(filteredItem.PossessionID + "_" + UpgradableItemCategory.Upgraded);
                }
            }
        }
    }

    public void ReplaceInventory(List<Item> newInventory)
    {
        foreach (var item in inventory)
        {
            if (!newInventory.Contains(item))
                ManagerObj.EffectDispatcher.SetupDispatch(item.GetEffect(PossessionEffectCategory.Removed)); // 제거 효과 적용
        }

        foreach (var item in newInventory)
        {
            if (!inventory.Contains(item))
            {
                ManagerObj.EffectDispatcher.SetupDispatch(item.GetEffect(PossessionEffectCategory.Added)); // 추가 효과 적용

                ManagerObj.MissionManager.UpdateAcquireCondition(MissionGoalCategory.CountItemAcquisitions, 1);
            }
        }

        inventory.Clear();
        inventory.AddRange(newInventory); // inventory를 newInventory로 바꾸면 StatusData에서 참조하는 리스트와 다른 리스트를 참조하게 되어버림
        CheckItemUpgrade();
    }

    public Item GetItemInfo(string itemID) // 이건 아이템 정보만 넘겨주는 메서드, 이걸로 받은 아이템 객체는 내용을 변경하면 안된다.
    {
        Item returnItem = itemDatas.FirstOrDefault(item => item.PossessionID == itemID);

        if (returnItem == null)
            Debug.LogWarning($"PossessionManager : GetItemData에서 같은 ID를 가진 아이템이 없음. itemID : {itemID}");

        return returnItem;
    }

    public Item GetItemClone(string itemID)
    {
        Item returnItemClone = itemDatas.FirstOrDefault(item => item.PossessionID == itemID);

        if (returnItemClone == null)
        {
            if(!itemID.ToLower().Contains("random")) Debug.LogError($"PossessionManager : GetItemData에서 같은 ID를 가진 아이템이 없음. itemID : {itemID}");
            return null;
        }

        return (Item)returnItemClone.Clone();
    }

    public Item GetRandomItem(List<float> gradeDropRates, params List<string>[] excludedItemIDsList) // 노말, 레어, 에픽, 저주 등급. 만일 안나오길 원하는 등급이 있다면 해당 등급 부분은 0으로 만들면됨
    {
        Item returnItem = null;

        while (returnItem == null)
        {
            float rand = Random.Range(0, gradeDropRates.Sum()); // 0 이상 gradeDropRates의 모든 요소의 합 미만의 랜덤값

            Item_Grade selectedGrade = Item_Grade.Normal; // 전달받은 등급 확률 리스트에 따라 가져올 아이템의 등급 결정
            float cumulative = 0;
            for (int i = 0; i < gradeDropRates.Count; i++)
            {
                cumulative += gradeDropRates[i];
                if (rand < cumulative)
                {
                    switch (i)
                    {
                        case 0: selectedGrade = Item_Grade.Normal; break;
                        case 1: selectedGrade = Item_Grade.Rare; break;
                        case 2: selectedGrade = Item_Grade.Epic; break;
                        default: selectedGrade = Item_Grade.Cursed; break;
                    }
                    break;
                }
            }

            var filteredList = itemDatas.Where(i => !i.Conditional
            && i.Grade == selectedGrade
            && !(excludedItemIDsList?.Any(list => list.Contains(i.PossessionID)) ?? false)
            ).ToList();

            if (filteredList.Count > 0)
                returnItem = (Item)filteredList[UnityEngine.Random.Range(0, filteredList.Count)].Clone();
        }

        return returnItem;
    }

    public Badge GetBadgeInfo(string badgeID)
    {
        Badge returnBadge = badgeDatas.FirstOrDefault(badge => badge.PossessionID == badgeID);

        if (returnBadge == null)
            Debug.LogWarning($"PossessionManager : GetBadgeData에서 같은 ID를 가진 배지가이 없음. badgeID : {badgeID}");

        return returnBadge;
    }

    public Badge GetBadgeClone(string badgeID)
    {
        Badge returnBadgeInfo = badgeDatas.FirstOrDefault(badge => badge.PossessionID == badgeID);

        if (!badgeID.ToLower().Contains("random") && returnBadgeInfo == null)
        {
            Debug.LogError($"PossessionManager : GetBadgeData에서 같은 ID를 가진 배지가 없음. badgeID : {badgeID}");
            return null;
        }

        return (Badge)returnBadgeInfo.Clone();
    }

    public Badge GetRandomBadge(Badge_Category badgeCategory)
    // Unconditional만 반환함
    // badgeCategory는 베네핏/페널티 구분
    {
        Badge returnBadge = null;

        while (returnBadge == null)
        {
            var filteredList = badgeDatas.Where(b => !b.Conditional
            && b.Category == badgeCategory
            ).ToList();

            if (filteredList.Count > 0)
            {
                returnBadge = (Badge)filteredList[UnityEngine.Random.Range(0, filteredList.Count)].Clone();
            }
        }

        return returnBadge; 
    }

    public Badge AddBadgeOnBadgeCaseFromBadgeRewardData(string key)
    {
        if (GetBadgeClone(badgeRewardDatas[key]) is Badge badge)
        {
            AddPossessionOnStorage<Badge>(badge);
            UpdatedInfoPanel.Instance.ShowViewLine(badge);
            return badge;
        }
        else
        {
            Debug.LogError($"PossessionManager : GetBadgeFromBadgeRewardData에서 badgeRewardDatas[key]에 해당하는 배지가 없습니다. key/badgeRewardDatas[key] : {key}/{badgeRewardDatas[key]}");
            return null;
        }
    }

    public void ArrangePossessionSlots<T>(Transform slotParent, SlotCategory sc, List<T> possessionList, int _slotCount = -1) where T : Possession // _slotCount의 경우 아이템의 경우만 해당됩니다.
    {
        if (_slotCount > 0 && _slotCount < possessionList.Count)
        {
            Debug.LogError($"PossessionManager : ArrangeItemSlots에서 _slotCount가 지정되었지만 possessionList의 크기가 더 큽니다. _slotCount : {_slotCount} possessionList.Count : {possessionList.Count}");
            //return;
        }

        int slotCount = (_slotCount != -1) ? _slotCount : possessionList.Count; // 슬롯 개수 설정

        for (int i = slotParent.childCount - 1; i >= slotCount; i--) // 만일 slotParent.childCount가 slotCount를 초과할 경우, 하위의 자식 오브젝트부터 제거
        {
            Destroy(slotParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < slotCount; i++)
        {
            PossessionSlot currentPossessionSlot;

            // 슬롯 세팅
            if (i < slotParent.childCount)
            {
                currentPossessionSlot = slotParent.GetChild(i).GetComponent<PossessionSlot>();
                if (currentPossessionSlot == null)
                {
                    Debug.LogError($"PossessionManager : ArrangePossessionSlots에서 slotParent의 자식 오브젝트 중 PossessionSlot이 없는 자식이 있습니다. 인덱스 : {i}");
                    return;
                }
            }
            else
            {
                ElementsPrefabCategory loadSlotEnum = possessionList is List<Item> ? ElementsPrefabCategory.ItemSlot : ElementsPrefabCategory.BadgeSlot;
                GameObject slot = ManagerObj.PrefabLoader.GetPrefab(loadSlotEnum, slotParent);
                currentPossessionSlot = slot.GetComponent<PossessionSlot>();
            }
            //

            if (i < possessionList.Count)
                currentPossessionSlot.SetSlot(sc, possessionList[i]); // 슬롯에 포제션 레이어 설정하기
            else
                currentPossessionSlot.SetSlot(sc);

            StartCoroutine(AdjustLayerSizeLayerAfterWaitForEndOfFrame(currentPossessionSlot));
        }

        IEnumerator AdjustLayerSizeLayerAfterWaitForEndOfFrame(PossessionSlot slot)
        {
            var cg = slot.GetComponent<CanvasGroup>(); // CanvasGroup 그룹을 이용하면 slot의 하위 오브젝트들의 UI Image들을 모두 접근할 수 있다.
            if(cg == null)
            {
                Debug.LogError("PossessionSlot 프리팹에 CanvasGroup 컴포넌트가 없습니다. PossessionSlot의 Awake에서 CanvasGroup 컴포넌트를 추가했는지 확인해주세요.");
                yield break;
            }

            cg.alpha = 0; // 크기 조정할 때엔 이미지 안보이게.

            yield return new WaitForEndOfFrame(); // 슬롯이 slotParent의 GridLayoutGroup에 지정된 크기로 설정될때까지 대기
            slot.SetSlotRect(); // 슬롯 크기 적용
            slot.PossessionLayer?.AdjustLayerBySlotSize(); // 슬롯 크기가 정해진 뒤에는 슬롯에 있는 PossessionLayer의 크기 조정

            cg.alpha = 1; // 크기 조정 종료 후 다시 보이게
        }
    }
}
