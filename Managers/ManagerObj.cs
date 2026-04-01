using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManagerObj : MonoBehaviour
{
    private static ManagerObj instance = null;

    [SerializeField] static GameObject globalManagers, inGameManagers, dispatchers;

    void Awake()
    {
        if (instance)
        {
            Destroy(this.gameObject); // 새로 생성된 중복 오브젝트 제거
            return;
        }

        instance = this; // 최초 인스턴스 설정
        DontDestroyOnLoad(gameObject); // 씬 이동 후에도 유지

        globalManagers = GameObject.FindGameObjectWithTag(GameObjectTagCategory.GlobalManagers.ToString());
        globalManagers.SetActive(true);

        inGameManagers = GameObject.FindGameObjectWithTag(GameObjectTagCategory.InGameManagers.ToString());
        inGameManagers.SetActive(true);

        dispatchers = GameObject.FindGameObjectWithTag(GameObjectTagCategory.Dispatchers.ToString());
        dispatchers.SetActive(true);

        if (globalManagers.GetComponent<DataManager>() == null) globalManagers.AddComponent<DataManager>();
        if (globalManagers.GetComponent<DisplayManager>() == null) globalManagers.AddComponent<DisplayManager>();
        if (globalManagers.GetComponent<OptionManager>() == null) globalManagers.AddComponent<OptionManager>();
        if (globalManagers.GetComponent<PrefabLoader>() == null) globalManagers.AddComponent<PrefabLoader>();
        if (globalManagers.GetComponent<SceneFlowManager>() == null) globalManagers.AddComponent<SceneFlowManager>();
        if (globalManagers.GetComponent<ScriptManager>() == null) globalManagers.AddComponent<ScriptManager>();
        if (globalManagers.GetComponent<SoundManager>() == null) globalManagers.AddComponent<SoundManager>();

        if (inGameManagers.GetComponent<StatusManager>() == null) inGameManagers.AddComponent<StatusManager>();
        if (inGameManagers.GetComponent<CharacterManager>() == null) inGameManagers.AddComponent<CharacterManager>();
        if (inGameManagers.GetComponent<FacilityManager>() == null) inGameManagers.AddComponent<FacilityManager>();
        if (inGameManagers.GetComponent<PossessionManager>() == null) inGameManagers.AddComponent<PossessionManager>();
        if (inGameManagers.GetComponent<MissionManager>() == null) inGameManagers.AddComponent<MissionManager>();
        if (inGameManagers.GetComponent<InputManager>() == null) inGameManagers.AddComponent<InputManager>();
        if (inGameManagers.GetComponent<InGameProgressManager>() == null) inGameManagers.AddComponent<InGameProgressManager>();
        if (inGameManagers.GetComponent<InGameProgressManager>() == null) inGameManagers.AddComponent<InGameProgressManager>();

        if (dispatchers.GetComponent<DialogueEventDispatcher>() == null) dispatchers.AddComponent<DialogueEventDispatcher>();
        if (dispatchers.GetComponent<EffectDispatcher>() == null) dispatchers.AddComponent<EffectDispatcher>();
        if (dispatchers.GetComponent<ConditionDispatcher>() == null) dispatchers.AddComponent<ConditionDispatcher>();
    }

    public static ManagerObj Instance
    {
        get { return instance; }
    }

    public IEnumerator InitInGameManagers(bool isInGame)
    {
        foreach (InGameManager inGameManagerWithInterface in gameObject.GetComponentsInChildren<InGameManager>(true))
        {
            if (isInGame)
                yield return inGameManagerWithInterface.InitInGame();
            else
                yield return inGameManagerWithInterface.InitOutOfGame();
        }
    }

    // GlobalManagers
    public static DataManager DataManager => globalManagers.GetComponent<DataManager>();
    public static DisplayManager DisplayManager => globalManagers.GetComponent<DisplayManager>();
    public static OptionManager OptionManager => globalManagers.GetComponent<OptionManager>();
    public static PrefabLoader PrefabLoader => globalManagers.GetComponent<PrefabLoader>();
    public static SceneFlowManager SceneFlowManager => globalManagers.GetComponent<SceneFlowManager>();
    public static ScriptManager ScriptManager => globalManagers.GetComponent<ScriptManager>();
    public static SoundManager SoundManager => globalManagers.GetComponent<SoundManager>();

    // InGameManager
    public static StatusManager StatusManager => inGameManagers.GetComponent<StatusManager>();
    public static CharacterManager CharacterManager => inGameManagers.GetComponent<CharacterManager>();
    public static FacilityManager FacilityManager => inGameManagers.GetComponent<FacilityManager>();
    public static PossessionManager PossessionManager => inGameManagers.GetComponent<PossessionManager>();
    public static MissionManager MissionManager => inGameManagers.GetComponent<MissionManager>();
    public static InGameProgressManager InGameProgressManager => inGameManagers.GetComponent<InGameProgressManager>();
    public static InputManager InputManager => inGameManagers.GetComponent<InputManager>();

    // Dispatcher
    public static DialogueEventDispatcher DialogueEventDispatcher => dispatchers.GetComponent<DialogueEventDispatcher>();
    public static EffectDispatcher EffectDispatcher => dispatchers.GetComponent<EffectDispatcher>();
    public static ConditionDispatcher ConditionDispatcher => dispatchers.GetComponent<ConditionDispatcher>();
}
