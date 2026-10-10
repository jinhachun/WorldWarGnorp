using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GnorpWar
{
    // 보관함 상자(사용자 10/9: 백팩배틀즈처럼 하단 상자에 자유롭게 던져 놓는다 · 10/10: 회전도 하면서 자유분방하게) — 상자 안 아무 데나
    // 놓으면 끌던 속도 그대로 던져져, 돌면서 떨어지고 벽·바닥·다른 조각에 부딪혀 구르다 멈춘다(기울어진 채 멈추기도 한다).
    // 데이터는 그대로 RunState.Storage(칸 번호) — 조각의 자리·각도만 UI가 기억한다(라운드마다 씬이 다시 떠도 유지).
    // 조각 = 보관함 BoardView의 칸(SlotView, 층 조각 그림). 이 상자 오브젝트가 그 칸들의 부모이고, 칸 자리·각도를 매 프레임 여기서 정한다.
    // 물리는 이 상자만의 2D 물리 공간(따로 만든 씬의 PhysicsScene2D)에서 돈다 — 전투 물리와 서로 닿지 않고, 손으로 스텝을 밟아
    // Time.timeScale(배속·일시정지)과 무관하다. 조각이 모두 잠들면 스텝도 칸 갱신도 하지 않는다
    public class StorageBox : MonoBehaviour, IDropHandler
    {
        // 물리 공간 1 = 상자 100px (조각 120px = 1.2 — 2D 물리가 다루기 좋은 크기)
        private const float Unit = 0.01f;
        private const float Step = 1f / 60f;
        // 전역 중력(-9.81)의 배수 — 상자 높이를 0.4초 안팎에 떨어진다
        private const float GravityScale = 3f;
        // 던지는 속도 상한(px/초)
        private const float MaxThrow = 2500f;
        // 옆으로 던진 속도(px/초)당 도는 속도(도/초) · 가만히 놓아도 살짝 도는 폭
        private const float SpinPerSpeed = 0.3f;
        private const float SpinWobble = 180f;
        // 거의 바로 선(90도 단위) 조각은 딱 맞춰 그린다 — 0.x도 기울면 픽셀 가장자리에 계단 한 칸이 생긴다
        private const float FlatAngle = 1f;
        // 2D 물리는 맞닿은 두 면 사이를 1.6px쯤 띄워 둔다(실측) — 몸체를 면마다 이만큼(px) 줄여 그림끼리는 딱 붙게
        private const float Skin = 0.8f;

        [SerializeField] private ShopPanel _shop;
        [Tooltip("상자 벽·바닥 두께(px) — 조각은 이 안쪽에만 놓인다")]
        [SerializeField] private float _wall = 12f;
        [Tooltip("「보관함 n/6」 — 꽉 차면 「가득」")]
        [SerializeField] private Text _label;

        private struct Pose
        {
            public bool Set;
            public Vector2 Position;   // 물리 공간(상자 로컬 px × Unit), 조각 가운데
            public float Angle;
        }

        // [보관함 칸] = 그 조각이 놓인 자리·각도. 빈 칸은 Set = false
        private static Pose[] _poses;

        private PhysicsScene2D _physics;
        private Rigidbody2D[] _bodies;
        private float _pending;
        // 물리가 잠들어 있어도 칸 자리를 한 번 써야 한다(씬이 다시 뜬 직후)
        private bool _sync;

        private RectTransform Rect => (RectTransform)transform;

        public static int FirstEmpty()
        {
            for (int i = 0; i < RunState.Storage.Length; i++)
                if (OwnedBuilding.IsEmpty(RunState.Storage[i]))
                    return i;
            return -1;
        }

        public void OnDrop(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position, null, out Vector2 local);
            _shop.DropOnBox(this, local);
        }

        // 그 칸의 조각을 이 자리(포인터, 상자 로컬)에서 끌던 속도(화면 px/초)로 던져 넣는다
        public void Throw(int index, Vector2 local, Vector2 pieceSize, Vector2 screenVelocity)
        {
            Rect r = Rect.rect;
            Vector2 half = pieceSize * 0.5f;
            local.x = Mathf.Clamp(local.x, r.xMin + _wall + half.x, r.xMax - _wall - half.x);
            local.y = Mathf.Clamp(local.y, r.yMin + _wall + half.y, r.yMax - half.y);
            Launch(index, local, Vector2.ClampMagnitude(screenVelocity / Rect.lossyScale.x, MaxThrow));
        }

        private void Launch(int index, Vector2 local, Vector2 velocity)
        {
            EnsurePoses();
            Rigidbody2D body = _bodies[index];
            body.simulated = true;
            body.position = local * Unit;
            body.rotation = 0f;
            body.linearVelocity = velocity * Unit;
            body.angularVelocity = -velocity.x * SpinPerSpeed + Random.Range(-SpinWobble, SpinWobble);
            body.WakeUp();
            _poses[index].Set = true;
        }

        // 조각 자리 — 빈 칸은 자리를 지우고, 자리 없는 조각(클릭 구매·맞바꿈·새 런)은 칸 순서대로 상자 위쪽에서 떨어뜨린다
        public void Place(SlotView[] views)
        {
            EnsurePoses();
            Rect r = Rect.rect;
            if (_bodies == null)
                Build(((RectTransform)views[0].transform).rect.size);

            int count = 0;
            bool awake = false;
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody2D body = _bodies[i];
                if (i >= views.Length || OwnedBuilding.IsEmpty(RunState.Storage[i]))
                {
                    _poses[i].Set = false;
                    if (body.simulated)
                        body.simulated = false;   // 빠진 조각에 얹혀 있던 조각은 물리가 깨워 다시 떨어진다
                    continue;
                }
                count++;
                if (!_poses[i].Set)
                {
                    float column = (r.width - 2f * _wall) / _bodies.Length;
                    float height = ((RectTransform)views[i].transform).rect.height;
                    Launch(i, new Vector2(r.xMin + _wall + column * (i + 0.5f), r.yMax - height * 0.5f), Vector2.zero);
                }
                else if (!body.simulated)
                {
                    // 씬이 다시 뜬 뒤 — 기억한 자리·각도에 잠든 채로 놓는다
                    body.simulated = true;
                    body.position = _poses[i].Position;
                    body.rotation = _poses[i].Angle;
                    body.Sleep();
                    _sync = true;
                }
                awake |= body.IsAwake();
            }

            if (awake)
            {
                _pending += Mathf.Min(Time.unscaledDeltaTime, 4f * Step);
                while (_pending >= Step)
                {
                    _physics.Simulate(Step);
                    _pending -= Step;
                }
            }
            if (awake || _sync)
            {
                _sync = false;
                for (int i = 0; i < _bodies.Length; i++)
                {
                    if (!_poses[i].Set)
                        continue;
                    Rigidbody2D body = _bodies[i];
                    _poses[i].Position = body.position;
                    _poses[i].Angle = body.rotation;
                    float angle = body.rotation;
                    float square = Mathf.Round(angle / 90f) * 90f;
                    bool flat = Mathf.Abs(angle - square) < FlatAngle;
                    Vector2 local = body.position / Unit;
                    Transform view = views[i].transform;
                    view.localPosition = flat ? new Vector3(Mathf.Round(local.x), Mathf.Round(local.y), 0f) : (Vector3)local;
                    view.localRotation = Quaternion.Euler(0f, 0f, flat ? square : angle);
                }
            }

            if (_label != null)
            {
                int filled = count;
                _label.text = filled >= RunState.Storage.Length ? $"보관함 {filled}/{RunState.Storage.Length} 가득" : $"보관함 {filled}/{RunState.Storage.Length}";
            }
        }

        // 상자만의 물리 공간 — 벽 넷(왼쪽·오른쪽·바닥·뚜껑: 조각이 상자 밖으로 못 나간다)과 칸 수만큼의 조각 몸체.
        // 이 씬(LoadScene 단일 모드)이 다시 뜰 때 함께 내려간다
        private void Build(Vector2 pieceSize)
        {
            Scene scene = SceneManager.CreateScene("StorageBoxPhysics" + GetInstanceID(), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physics = scene.GetPhysicsScene2D();
            var material = new PhysicsMaterial2D { friction = 0.5f, bounciness = 0.25f };

            Rect r = Rect.rect;
            const float thick = 200f;   // 빠르게 던진 조각이 뚫지 못하게 두껍게
            var walls = new GameObject("Walls");
            SceneManager.MoveGameObjectToScene(walls, scene);
            walls.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            float inner = r.xMin + _wall - Skin, outer = r.xMax - _wall + Skin, floor = r.yMin + _wall - Skin, lid = r.yMax + Skin;
            AddBox(walls, new Vector2(inner - thick * 0.5f, r.center.y), new Vector2(thick, r.height + 2f * thick), material);
            AddBox(walls, new Vector2(outer + thick * 0.5f, r.center.y), new Vector2(thick, r.height + 2f * thick), material);
            AddBox(walls, new Vector2(r.center.x, floor - thick * 0.5f), new Vector2(r.width + 2f * thick, thick), material);
            AddBox(walls, new Vector2(r.center.x, lid + thick * 0.5f), new Vector2(r.width + 2f * thick, thick), material);

            _bodies = new Rigidbody2D[RunState.Storage.Length];
            for (int i = 0; i < _bodies.Length; i++)
            {
                var piece = new GameObject("Piece" + i);
                SceneManager.MoveGameObjectToScene(piece, scene);
                Rigidbody2D body = piece.AddComponent<Rigidbody2D>();
                body.simulated = false;
                body.gravityScale = GravityScale;
                body.linearDamping = 0.2f;
                body.angularDamping = 0.5f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                AddBox(piece, Vector2.zero, pieceSize - 2f * Skin * Vector2.one, material);
                _bodies[i] = body;
            }
        }

        // 가운데·크기는 상자 로컬 px
        private static void AddBox(GameObject on, Vector2 center, Vector2 size, PhysicsMaterial2D material)
        {
            BoxCollider2D box = on.AddComponent<BoxCollider2D>();
            box.offset = center * Unit;
            box.size = size * Unit;
            box.sharedMaterial = material;
        }

        private static void EnsurePoses()
        {
            if (_poses == null || _poses.Length != RunState.Storage.Length)
                _poses = new Pose[RunState.Storage.Length];
        }
    }
}
