using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.TextCore.Text;


#if UNITY_EDITOR
using UnityEditor.U2D.Animation;
#endif

public class FacilityManager : InGameManager
{
    List<Facility> facilityDatas;
    public override IEnumerator InitInGame()
    {
        facilityDatas = (List<Facility>)ManagerObj.DataManager.RuntimeData[SaveDataCategory.Facility];

        facilityFadeSprite = transform.Find("FacilityFadeSprite").GetComponent<SpriteRenderer>();
        if(facilityFadeSprite == null)
        {
            Debug.LogError("InGameManagers 아래에 FacilityFadeSprite 이름의 SpriteRenderer를 가진 오브젝트가 없습니다.");
        }
        facilityFadeSprite.gameObject.SetActive(false);
        facilityFadeSprite.color = new Color(1, 1, 1, 0);
        facilityFadeSprite.gameObject.SetActive(false);

        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        facilityDatas = null;
        facilityFadeSprite.color = new Color(1, 1, 1, 0);
        facilityFadeSprite.gameObject.SetActive(false);

        yield return null;
    }

    public static FacilityID[] GetAllFacilityID => (FacilityID[])System.Enum.GetValues(typeof(FacilityID));

    public Facility GetFacilityData(FacilityID facilityID)
    {
        return facilityDatas.FirstOrDefault(c => c.FacilityID == facilityID);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ManagerObj.SceneFlowManager.CurrentCategory == SceneCategory.MainGameScene)
        {
            Destroy(GameObject.Find(FacilityID.Lobby.ToString()));

            DisableFacilityObj();
            StartCoroutine(ManagerObj.InGameProgressManager.PlayBGMByFacility());
        }
    }

    GameObject facilityObj;
    public FacilityID ConfiguredFacilityID { get; set; }
    public void ConfigureFacility(string facilityID)
    {
        GameObject loadedFacilityObj = null;
        if (System.Enum.TryParse(facilityID, out FacilityID resultID))
            loadedFacilityObj = ManagerObj.PrefabLoader.GetPrefab(resultID, null, new Vector2(0.95f, 0.95f));

        if (loadedFacilityObj == null) // 만일 로드된 시설 오브젝트가 없다면 종료
            return;

        Destroy(facilityObj);
        facilityObj = loadedFacilityObj; // 로드된 시설 오브젝트 등록
        ConfiguredFacilityID = resultID;
        if (facilityObj.transform.childCount > 0) // 
            facilityObj.transform.GetChild(0).gameObject.SetActive(false);  // SurveySpotGroup 오브젝트 비활성화

        facilityObj.GetComponent<SpriteRenderer>().sortingOrder = ManagerObj.DisplayManager.GetSortingOrder("FacilityObj");
    }

    public void ConfigureFacility(FacilityID facilityID)
    {
        ConfigureFacility(facilityID.ToString());
    }

    public void DisableFacilityObj()
    {
        ConfigureFacility(FacilityID.Lobby);
    }

    public void InitFacilityUsageInfos()
    {
        foreach (Facility facility in facilityDatas)
        {
            facility.CanUse = true;
        }
    }

    public void Activity_UseFacility()
    {
        int loopCount = 0;
        StartCoroutine(UseCoroutine());

        IEnumerator UseCoroutine()
        {
            InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
            ManagerObj.ScriptManager.PlayScript(PlayerControlledScriptCategory.FacilityUsage, ScriptCategory.Etc);
            yield return new WaitUntil(() => !ManagerObj.ScriptManager.IsPlayingScript);

            ManagerObj.InputManager.BlockOpenBook = true;
            BookUI.Instance?.ActiveClosedBook();
            yield return new WaitForSeconds(0.5f);
            yield return ManagerObj.DisplayManager.GlobalFadeIn(0.5f);
            ManagerObj.CharacterManager.DisableCharacterObj();
            yield return new WaitForSeconds(1f);
            yield return ManagerObj.DisplayManager.GlobalFadeOut(0.5f);
            yield return new WaitForSeconds(0.5f);

            List<float> usageRewardNumProbability = ManagerObj.DataManager.GetGameBalanceData(GameBalanceKeyCategory.Facility_UsageRewardNumProbability);
            int usageRewardNum = ManagerObj.DataManager.GetIndexByComputedProbability(usageRewardNumProbability) + 1; // usageReward의 개수, GetIndexByComputedProbability는 확률 별로 0,1,2 주고 usageRewardNum는 1~3이니까 +1 해줌

            List<string> rewardIDs = inGameProgressManager.VisitingFacility.UsageRewardIDs;
            List<StatusCategory> rewards = new();
            List<GameObject> rewardObjs = new();

            for(int i = 0;i< usageRewardNum;i++)
            {
                StatusCategory reward = ManagerObj.StatusManager.ParseStatusCategory(rewardIDs.OrderBy(x => UnityEngine.Random.value).First());
                if (rewards.Contains(reward))
                {
                    i--;
                    loopCount++;

                    if (loopCount >= 10) // 만일 똑같은 값이 10번 연속 나왔다면 그냥 진행
                        break;
                }
                else
                {
                    loopCount = 0;

                    rewards.Add(reward);

                    GameObject usageRewardLine = ManagerObj.PrefabLoader.GetPrefab(ElementsPrefabCategory.UsageRewardResult);
                    rewardObjs.Add(usageRewardLine);

                    (float min, float max) values = ManagerObj.StatusManager.GetUsageRewardValueInfo(reward);
                    float changedValue = Mathf.Round(UnityEngine.Random.Range(values.min, values.max) * 10f) / 10f;

                    List<float> additionalValueOnBadge = null;
                    switch (inGameProgressManager.VisitingFacility.FacilityID)
                    {
                        case FacilityID.Library: additionalValueOnBadge = ManagerObj.PossessionManager.GetBadgeInherentEffect_FloatList(InherentEffectBadgeCategory.Bookworm); break;
                        case FacilityID.Cafe: additionalValueOnBadge = ManagerObj.PossessionManager.GetBadgeInherentEffect_FloatList(InherentEffectBadgeCategory.CoffeeManiac); break;
                        case FacilityID.Gym: additionalValueOnBadge = ManagerObj.PossessionManager.GetBadgeInherentEffect_FloatList(InherentEffectBadgeCategory.Bodybuilder); break;
                        case FacilityID.Arcade: additionalValueOnBadge = ManagerObj.PossessionManager.GetBadgeInherentEffect_FloatList(InherentEffectBadgeCategory.ProfessionalGamer); break;
                        default: additionalValueOnBadge = null; break;
                    }
                    if(additionalValueOnBadge != null && additionalValueOnBadge.Count > 0)
                    {
                        if (additionalValueOnBadge.Count != 3)
                        {
                            Debug.LogError($"FacilityManager : Activity_UseFacility에서 배지에서 불러온 additionalValueOnBadge의 additionalValueOnBadge.Count가 3이 아닙니다. additionalValueOnBadge.Count : {additionalValueOnBadge.Count}");
                        }
                        else
                        {
                            float additionalValue = 0;
                            switch (reward)
                            {
                                case StatusCategory.Health:
                                case StatusCategory.Mental: additionalValue = additionalValueOnBadge[0]; break;

                                case StatusCategory.Strength:
                                case StatusCategory.Charisma: additionalValue = additionalValueOnBadge[1]; break;

                                case StatusCategory.Stress: additionalValue = -additionalValueOnBadge[1]; break;

                                case StatusCategory.Reputation: additionalValue = additionalValueOnBadge[2]; break;
                            }
                            changedValue += additionalValue;
                        }
                    }

                    usageRewardLine.GetComponent<InfoUpdateViewLine>().SetInfo(reward, changedValue); // 먼저 정보 띄워주는 라인 만들어주고
                    ManagerObj.StatusManager.UpdateStatus(reward, changedValue); // 실제 스탯 업데이트 실행
                }
            }

            ManagerObj.DisplayManager.ShowWindow(new WindowInfoWithObjs(WindowCategory.FacilityUsageResult, rewardObjs));

            ManagerObj.InputManager.BlockOpenBook = false;
        }
    }

    public void Activity_SurveyFacility()
    {
        StartCoroutine(SurveyCoroutine());

        IEnumerator SurveyCoroutine()
        {
            yield return ManagerObj.DisplayManager.GlobalFadeIn(0.5f);
            ManagerObj.CharacterManager.DisableCharacterObj();
            ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.FacilitySurvey);
            EnableSurveySpotGroup();
            yield return new WaitForSeconds(1f);
            yield return ManagerObj.DisplayManager.GlobalFadeOut(0.5f);
        }
    }

    public int GetTotalSurveyCount
    {
        get
        {
            int count = 0;
            foreach (FacilityID facilityID in GetAllFacilityID)
            {
                if (GetFacilityData(facilityID) is Facility facility)
                {
                    count += facility.SurveyCount;
                }
            }

            return count;
        }
    }

    public void EnableSurveySpotGroup()
    {
        if(facilityObj.transform.childCount == 0)
        {
            Debug.Log($"FacilityManager : EnableSurveySpotGroup()에서 facilityObj의 하위에 SurveySpotGroup 오브젝트가 없습니다. facilityObj.name : {facilityObj.name}");
            return;
        }

        Transform surveySpotGroup = facilityObj.transform.GetChild(0);
        surveySpotGroup.gameObject.SetActive(true);

        foreach (Transform child in surveySpotGroup)
            child.gameObject.SetActive(false);

        int childCount = surveySpotGroup.childCount;

        int minActive = Mathf.CeilToInt(childCount * (2f / 3f)); // 최소 활성 개수 = 전체의 2/3
        int targetActive = UnityEngine.Random.Range(minActive, childCount + 1); // 랜덤 활성 개수 (minActive ~ childCount)

        List<Transform> children = new List<Transform>(); // 모든 자식 리스트 가져오기
        for (int i = 0; i < childCount; i++)
            children.Add(surveySpotGroup.GetChild(i));

        List<Transform> shuffled = children.OrderBy(x => Random.value).ToList(); // 섞기 (LINQ OrderBy + Random.value 활용)
        for (int i = 0; i < childCount; i++) // 활성화 / 비활성화 적용
            shuffled[i].gameObject.SetActive(i < targetActive);
    }

    public void DisableSurveySpotGroup()
    {
        if (facilityObj.transform.childCount == 0)
        {
            Debug.Log($"FacilityManager : DisableSurveySpotGroup()에서 facilityObj의 하위에 SurveySpotGroup 오브젝트가 없습니다. facilityObj.name : {facilityObj.name}");
            return;
        }

        facilityObj.transform.GetChild(0).gameObject.SetActive(false);
    }

    SpriteRenderer facilityFadeSprite;
    bool isFacilityFading;
    public IEnumerator EnableFadeIn(bool onCharacter, float beforeDuration, float fadingDuration, float afterDuration)
    {
        if (onCharacter)
        {
            facilityFadeSprite.sortingOrder = ManagerObj.DisplayManager.GetSortingOrder("CharacterController_Shadow") + 1; // 캐릭터 그림자보다 크게 하면 캐릭터는 무조건 가려짐
        }
        else
        {
            facilityFadeSprite.sortingOrder = ManagerObj.DisplayManager.GetSortingOrder("FacilityFade");
        }
        yield return new WaitForSeconds(beforeDuration);

        facilityFadeSprite.gameObject.SetActive(true); 
        yield return FacilityFading(fadingDuration, 0, 1);

        yield return new WaitForSeconds(afterDuration);
    }

    public IEnumerator EnableFadeOut(float beforeDuration, float fadingDuration, float afterDuration)
    {
        if (!facilityFadeSprite.gameObject.activeSelf)
        {
            //Debug.LogError("EnableFadeOut : facilityFadeSprite가 켜지지 않았는데 DisableFacilityShadow가 실행되었습니다.");
            yield break;
        }

        yield return new WaitForSeconds(beforeDuration);

        yield return FacilityFading(fadingDuration, 1, 0);
        facilityFadeSprite.gameObject.SetActive(false);

        yield return new WaitForSeconds(afterDuration);
    }

    IEnumerator FacilityFading(float duration, float start, float target)
    {
        facilityFadeSprite.color = new Color(1, 1, 1, start);

        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            facilityFadeSprite.color = new Color(1, 1, 1, Mathf.Lerp(start, target, t));

            yield return null; // 프레임 넘기기
        }

        facilityFadeSprite.color = new Color(1, 1, 1, target);
    }

    public bool IsFacilityFading
    {
        get{ return isFacilityFading; }
    }
}
