using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

public class EffectDispatcher : Dispatcher
{
    public override void SetupDispatch(List<string> datas)
    {
        base.SetupDispatch(datas);

        if (datas == null)
            return;

        foreach (string data in datas)
        {
            var parsedStr = SplitKeyAndDetails<DispatchType_Effect>(data);
            Dispatch_Effect(parsedStr.key, parsedStr.details);
        }
    }

    protected override void Dispatch_Effect(DispatchType_Effect effectID, List<string> details)
    {
        switch (effectID)
        {
            case DispatchType_Effect.UpdateStatusData : UpdateStatusData(details, false); break;

            default: Debug.LogError($"ConditionDispatcher : Dispatch_Condition에서 conditionID가 올바르지 않습니다.(false 반환) conditionID : {effectID.ToString()}"); break;
        }
    }
}
