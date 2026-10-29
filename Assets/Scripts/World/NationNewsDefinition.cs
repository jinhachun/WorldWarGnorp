using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 나라 소식 한 가지의 데이터(아웃게임기획 §3-2 표) — 제목 · 사연 · 고르기 둘. 규칙(언제 뜨는지 · 효과를 거는 것)은 RunState(Run/RunState.News)에 있다.
    // 효과는 골드 · 수입 · 상점 · 기물만이다(전투 수치를 건드리지 않는다). WorldConfig 「나라 소식」이 든다
    [CreateAssetMenu(menuName = "GnorpWar/Nation News")]
    public class NationNewsDefinition : ScriptableObject
    {
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _displayName = "";
        [Tooltip("제목 아래 한 줄 사연")]
        [HideIf(nameof(HideNonBalance))]
        [SerializeField] private string _story = "";
        [SerializeField] private NewsChoice _first = new NewsChoice();
        [SerializeField] private NewsChoice _second = new NewsChoice();

        private bool HideNonBalance => BalanceView.HideNonBalance;

        public string DisplayName => _displayName;
        public string Story => _story;
        public NewsChoice Choice(int index) => index == 0 ? _first : _second;
    }

    // 고르기 하나에 따라오는 기물 쪽 일 — 숫자 효과(골드 · 수입 · 리롤 · 칸 · 단계)와 같이 건다
    public enum NewsAction
    {
        None,
        HeroShop,            // 이번 턴 진열 · 리롤이 전부 영웅 이상
        PickRankUp,          // 탑의 기물 하나를 골라 한 등급
        RandomRankUp,        // 탑의 무작위 기물 하나 한 등급
        Fire,                // 탑의 무작위 기물 하나를 잃고, 그 자리에 한 등급 높은 무작위 기물
        Alchemy,             // 탑의 기물 하나를 골라 확률로 신화
        GainCommonProducer,  // 공용 생산 기물(쿨다운마다 유닛을 소환) 하나를 얻음
        GainLegendary,       // 전설 기물 하나를 얻음
    }

    [System.Serializable]
    public class NewsChoice
    {
        [Tooltip("고르기 글(한 줄 = 한 효과, CLAUDE.md §5-2) — {0} 골드 · {1} 턴 수입 · {2} 다음 턴 수입 · {3} 무료 리롤 · {4} 진열 칸 · {5} 확률(%) · {6} 상점 단계")]
        [HideIf(nameof(HideNonBalance))]
        [TextArea(2, 4)]
        [SerializeField] private string _text = "";
        [Tooltip("지금 골드에 더하는 값 — 음수면 내는 값(모자라면 고를 수 없다. 다른 효과가 없는 벌이면 0까지만 깎는다)")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _gold;
        [Tooltip("턴 수입 영구히 +N")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _income;
        [Tooltip("다음 턴 수입에 한 번 더하는 값")]
        [SuffixLabel("골드", true)]
        [SerializeField] private int _nextIncome;
        [Tooltip("다음 턴 수입에 한 번 곱하는 값(0.5 = 절반) — 승리 보너스는 곱하지 않는다")]
        [SerializeField] private float _nextIncomeScale = 1f;
        [Tooltip("이번 턴 무료 리롤 +N")]
        [SuffixLabel("회", true)]
        [SerializeField] private int _freeRerolls;
        [Tooltip("상점 진열 칸 영구히 +N")]
        [SuffixLabel("칸", true)]
        [SerializeField] private int _offerSlots;
        [Tooltip("상점 등급 단계를 영구히 N턴 앞당김")]
        [SuffixLabel("턴", true)]
        [SerializeField] private int _tierAdvance;
        [SerializeField] private NewsAction _action;
        [Tooltip("연금술 — 성공 확률(0.25 = 25%)")]
        [SerializeField] private float _chance;

        private bool HideNonBalance => BalanceView.HideNonBalance;

        public int Gold => _gold;
        public int Income => _income;
        public int NextIncome => _nextIncome;
        public float NextIncomeScale => _nextIncomeScale;
        public int FreeRerolls => _freeRerolls;
        public int OfferSlots => _offerSlots;
        public int TierAdvance => _tierAdvance;
        public NewsAction Action => _action;
        public float Chance => _chance;

        // 골드를 내기만 하는 벌(황충 ① · 대화재 ②) — 골드가 모자라도 고를 수 있고 0까지만 깎인다
        public bool PenaltyOnly => _gold < 0 && _income == 0 && _nextIncome == 0 && _nextIncomeScale == 1f && _freeRerolls == 0 && _offerSlots == 0 && _tierAdvance == 0 && _action == NewsAction.None;

        public string Describe() => string.Format(_text, _gold, _income, _nextIncome, _freeRerolls, _offerSlots, Mathf.RoundToInt(_chance * 100f), _tierAdvance);
    }
}
