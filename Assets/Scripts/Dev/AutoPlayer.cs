using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 밸런스 측정용 자동 플레이어 — 누를 수 있는 소환 버튼 중 하나를 무작위로 계속 누른다.
    // 씬에는 두지 않는다. 측정할 때만 플레이 중에 붙인다(HANDOFF 「밸런스 측정」)
    public class AutoPlayer : MonoBehaviour
    {
        private SummonButton[] _buttons;
        private readonly List<Button> _ready = new List<Button>();

        private void Start()
        {
            _buttons = FindObjectsByType<SummonButton>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            _ready.Clear();
            foreach (SummonButton summon in _buttons)
            {
                Button button = summon.GetComponent<Button>();
                if (button.interactable)
                    _ready.Add(button);
            }

            if (_ready.Count > 0)
                _ready[Random.Range(0, _ready.Count)].onClick.Invoke();
        }
    }
}
