using UnityEngine;

namespace GnorpWar
{
    // 적의 빌드 방향 하나(기획서 §8 아키타입) — 어떤 기물을 얼마나 원하는지와 상점 성향. 런을 시작할 때 하나를 뽑는다(BattleConfig의 목록).
    // 사는 판단은 Run/EnemyShopper가 이 값으로 한다
    [CreateAssetMenu(menuName = "GnorpWar/Enemy Build Profile")]
    public class EnemyBuildProfile : ScriptableObject
    {
        [System.Serializable]
        public struct Pick
        {
            public BuildingDefinition Building;
            [Tooltip("이 기물의 값 — 클수록 먼저 산다")]
            public float Weight;
        }

        [SerializeField] private string _displayName = "";
        [Tooltip("이 방향의 핵심 기물과 값 — 이 방향에서 쓰는 생산 기물도 여기에")]
        [SerializeField] private Pick[] _picks = new Pick[0];
        [Tooltip("위 목록에 없는 생산 기물(싸우는 유닛을 내는 기물)의 값")]
        [SerializeField] private float _producerValue = 1f;
        [Tooltip("탑의 기물 중 싸우는 유닛을 내는 기물이 이 비율보다 적으면 생산 기물을 가장 먼저 산다. 채웠으면 목록에 없는 생산 기물은 새로 사지 않는다")]
        [SerializeField] private float _producerShare = 0.5f;
        [Tooltip("위 목록에 없는 공용 기물(생산 기물 제외)의 값 — 다른 세력의 기물은 사지 않는다")]
        [SerializeField] private float _commonValue = 0.5f;
        [Tooltip("이미 가진 기물이 진열에 나오면(합쳐서 등급을 올린다) 값에 곱한다")]
        [SerializeField] private float _ownedScale = 1.5f;
        [Tooltip("리롤 성향 — 살 것이 없을 때, 리롤하고도 이만큼의 골드가 남으면 리롤한다(작을수록 많이 돌린다)")]
        [SerializeField] private int _rerollReserve = 6;
        [Tooltip("층 올리기 성향 — 켜면 탑이 꽉 찼을 때 골드가 되는 대로 층을 미리 올린다. 끄면 놓을 기물을 살 때에만 올린다")]
        [SerializeField] private bool _raisesFloorAhead = true;

        public string DisplayName => _displayName;
        public float ProducerValue => _producerValue;
        public float ProducerShare => _producerShare;
        public float CommonValue => _commonValue;
        public float OwnedScale => _ownedScale;
        public int RerollReserve => _rerollReserve;
        public bool RaisesFloorAhead => _raisesFloorAhead;

        // 목록에 없으면 0
        public float WeightOf(BuildingDefinition building)
        {
            foreach (Pick pick in _picks)
                if (pick.Building == building)
                    return pick.Weight;
            return 0f;
        }
    }
}
