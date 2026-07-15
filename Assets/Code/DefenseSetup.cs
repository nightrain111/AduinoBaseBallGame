using UnityEngine;

// 포수/투수/내야수/외야수를 홈플레이트 기준 표준 수비 위치에 배치한다.
// 판정/액션 없음 — 지정된 자리에 서 있게만 만드는 시각적 배치용 스크립트.
public class DefenseSetup : MonoBehaviour
{
    [Header("기준점")]
    [SerializeField] private Transform homePlate; // forward = 투수/센터필드 방향 (타자·공 스크립트와 동일 기준점 재사용 권장)

    [Header("높이 보정 (캡슐 등 기본 프리미티브는 피벗이 중앙이라 땅에 박힘)")]
    [SerializeField] private float fielderHeightOffset = 1f; // 모든 수비수 공통 위로 띄우는 높이

    [Header("구장 스케일 (실제 MLB 미터 수치가 이 구장 모델이랑 안 맞을 때 조정)")]
    [SerializeField] private float fieldScale = 1f; // 1보다 작으면 전체적으로 더 안쪽으로 배치됨

    [Header("수비수 (씬에 배치한 캐릭터를 연결)")]
    [SerializeField] private Transform catcher;
    [SerializeField] private Transform pitcher;
    [SerializeField] private Transform firstBase;
    [SerializeField] private Transform secondBase;
    [SerializeField] private Transform thirdBase;
    [SerializeField] private Transform shortstop;
    [SerializeField] private Transform leftField;
    [SerializeField] private Transform centerField;
    [SerializeField] private Transform rightField;

    void Start() => PlaceAll();

    [ContextMenu("Place All Fielders")]
    void PlaceAll()
    {
        if (homePlate == null) { Debug.LogWarning("[DefenseSetup] homePlate 없음"); return; }

        // 포수는 일단 생략 (모델/애니메이션 이슈로 보류)
        Place(pitcher,      18.4f,   0f);
        Place(firstBase,    27.4f,  45f);
        Place(thirdBase,    27.4f, -45f);
        Place(secondBase,   40f,    18f);
        Place(shortstop,    45f,   -18f);
        Place(leftField,    95f,   -27f);
        Place(centerField, 105f,    0f);
        Place(rightField,   95f,   27f);
    }

    void Place(Transform t, float distance, float angleDeg, bool faceOutward = false)
    {
        if (t == null) return;

        Quaternion rot = Quaternion.AngleAxis(angleDeg, homePlate.up);
        Vector3 dir    = rot * homePlate.forward;

        t.position = homePlate.position + dir * distance * fieldScale + Vector3.up * fielderHeightOffset;

        // 기본: 홈플레이트 쪽을 바라봄 (내야수/외야수/투수)
        // faceOutward: 홈플레이트를 등지고 투수 쪽을 바라봄 (포수)
        Vector3 facing = faceOutward ? dir : -dir;
        t.rotation = Quaternion.LookRotation(facing, homePlate.up);
    }
}
