using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

public class PlaceholderResolver
{
    private static readonly Regex placeholderRegex = new Regex(@"(?<!\\)\{([^{}]+)\}", RegexOptions.Compiled);
    public static Regex PlaceholderRegex => placeholderRegex;

    public static string RemovePlaceholders(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return placeholderRegex.Replace(input, ""); // placeholderRegex에 매칭되는 부분을 빈 문자열로 교체
    }

    public static string RenderWithKeys(string text)
    {
        return placeholderRegex.Replace(text, match =>
        {
            string inside = match.Groups[1].Value.Trim();
            if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) // 1) 숫자인 경우 (RenderWithOrder용 플레이스홀더) 건드리지 말고 원래 그대로 둠 -> 나중에 RenderWithOrder에서 처리
                return match.Value;
            else
                return ManagerObj.DataManager.GetEtcText(inside);
        });
    }

    public static string RenderWithOrder(string text, params string[] replaceValues)
    {
        // 사용 예시 1 : "{1}는 {0}이다", "동물", "토끼" -> 결과: "토끼는 동물이다"
        // 사용 예시 2 : "{0} + {0} = {1}", "2", "4" -> 결과: "2 + 2 = 4"
        // 사용 예시 3 :  "{2}번째 값", "a", "b" -> 값이 부족함, 결과: "{2}번째 값" (그대로 둠)

        return placeholderRegex.Replace(text, match =>
        {
            string inside = match.Groups[1].Value.Trim();

            if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) // int로 파싱 가능한 경우
            {
                if (index >= 0 && index < replaceValues.Length)
                    return replaceValues[index];
                else
                    return match.Value; // 값이 없으면 그대로 둠
            }

            return match.Value;  // 숫자가 아니면 그대로 둠
        });
    }

    private static readonly Regex ternaryInnermostRegex = new Regex(@"(?<!\\)\{([^{}?:]+)\?([^{}?:]*):([^{}?:]*)\}", RegexOptions.Compiled);
    /*
    public static string RenderWithTernary(string text)
    {
        const int MaxPasses = 20; // 중첩 하드캡
        int passes = 0;

        while (passes++ < MaxPasses) // 1회전마다 “가장 안쪽 삼항들”이 한 번에 처리됩니다.
        {
            bool changed = false; // 이번 회전에서 실제 치환이 일어났는지 추적합니다.

            string replaced = ternaryInnermostRegex.Replace(text, m => // 매칭된 단일 삼항 덩어리에 대해:
            {
                changed = true;

                string conditionExpr = m.Groups[1].Value.Trim();
                string trueValue = m.Groups[2].Value.Trim();
                string falseValue = m.Groups[3].Value.Trim();

                bool result = ManagerObj.ConditionDispatcher.GetDispatchedResult(conditionExpr);
                return result ? trueValue : falseValue;
            });

            if (!changed) break; // 더 이상 치환할 "가장 안쪽" 삼항이 없으면 종료

            text = replaced; // text = replaced;
        }

        return text;
    }*/
    public static string RenderWithTernary(string text) // 위에거는 오리지널, 이건 우선순위연산자, and/or 연산자 추가 버전
    {
        const int MaxPasses = 20;
        for (int pass = 0; pass < MaxPasses; pass++)
        {
            bool changed = false;
            text = ternaryInnermostRegex.Replace(text, m =>
            {
                changed = true;
                string cond = m.Groups[1].Value.Trim();
                string tVal = m.Groups[2].Value.Trim();
                string fVal = m.Groups[3].Value.Trim();
                return ManagerObj.ConditionDispatcher.GetDispatchedResult(cond) ? tVal : fVal;
            });
            if (!changed) break;
        }
        return text;
    }
}

    