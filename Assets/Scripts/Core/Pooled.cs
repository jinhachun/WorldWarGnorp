using System.Collections.Generic;
using UnityEngine;

namespace GnorpWar
{
    // 자주 만들고 버리는 것(유닛·투사체·이펙트)을 다시 쓰는 풀. Instantiate 대신 Get, Destroy 대신 SetActive(false).
    // 꺼지는 순간(OnDisable) 스스로 제 프리팹의 풀로 돌아간다 — 다시 꺼낼 때 상태는 각자 OnEnable에서 처음 값으로 되돌린다
    public class Pooled : MonoBehaviour
    {
        private static readonly Dictionary<GameObject, Stack<Pooled>> Free = new Dictionary<GameObject, Stack<Pooled>>();

        private GameObject _prefab;

        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            if (Free.TryGetValue(prefab.gameObject, out Stack<Pooled> stack))
            {
                while (stack.Count > 0)
                {
                    Pooled pooled = stack.Pop();
                    // 씬을 다시 불러오면 쉬던 오브젝트는 파괴된 채 남는다 — 버리고 다음 것
                    if (pooled == null)
                        continue;
                    pooled.transform.SetPositionAndRotation(position, rotation);
                    pooled.gameObject.SetActive(true);
                    return pooled.GetComponent<T>();
                }
            }

            T made = Instantiate(prefab, position, rotation);
            made.gameObject.AddComponent<Pooled>()._prefab = prefab.gameObject;
            return made;
        }

        private void OnDisable()
        {
            if (!Free.TryGetValue(_prefab, out Stack<Pooled> stack))
                Free[_prefab] = stack = new Stack<Pooled>();
            stack.Push(this);
        }
    }
}
