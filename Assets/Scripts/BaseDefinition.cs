using UnityEngine;

namespace GnorpWar
{
    [CreateAssetMenu(menuName = "GnorpWar/Base Definition")]
    public class BaseDefinition : ScriptableObject
    {
        [SerializeField] private float _maxHp = 500f;

        public float MaxHp => _maxHp;
    }
}
