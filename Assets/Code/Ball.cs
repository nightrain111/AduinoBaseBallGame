using System.Collections;
using UnityEngine;

// 투수 마운드 → 홈플레이트 궤적을 GameManager 투구 타이머에 맞춰 재생하고,
// 타격 결과에 따라 날아가거나 그 자리에 멈추는 공.
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [Header("궤적 기준점")]
    [SerializeField] private Transform pitchOrigin;      // 투수 마운드 위치
    [SerializeField] private Transform strikeZoneCenter; // 스트존 중앙 (홈플레이트 부근, forward = 투수 방향)

    [Header("높이 보정 (기준 오브젝트들 루트가 보통 발밑이라 위로 띄워줘야 함)")]
    [SerializeField] private float strikeZoneHeight   = 1.0f; // 스트존 높이 (타자 루트 기준)
    [SerializeField] private float releaseHeight       = 1.6f; // 투수 릴리즈 높이 (pitchOrigin 루트 기준)

    [Header("존 크기 (볼일 때 존 밖으로 벗어나는 정도)")]
    [SerializeField] private float ballOffsetRange = 0.6f;

    [Header("구종별 궤적 변화")]
    [SerializeField] private float curveballDrop = 0.5f;
    [SerializeField] private float sliderSide     = 0.3f;
    [SerializeField] private float changeupDrop   = 0.25f;

    [Header("스트라이크 후 사라지기")]
    [SerializeField] private float strikeVanishDelay = 2f; // 스트라이크 판정 후 이 시간 뒤에 공 숨김

    [Header("타구 비행 세기")]
    [SerializeField] private float foulForce    = 5f;
    [SerializeField] private float outForce     = 6f;
    [SerializeField] private float singleForce  = 10f;
    [SerializeField] private float doubleForce  = 14f;
    [SerializeField] private float tripleForce  = 17f;
    [SerializeField] private float homerunForce = 24f;

    private Rigidbody rb;
    private Vector3 pitchTarget;
    private bool inFlight;

    private Vector3 OriginPos => pitchOrigin.position + Vector3.up * releaseHeight;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogWarning("[Ball] GameManager.Instance 없음"); return; }

        gm.OnPitchStart += BeginPitch;
        gm.OnResult     += HandleResult;
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnPitchStart -= BeginPitch;
        gm.OnResult     -= HandleResult;
    }

    // ── 투구 시작: 목표 지점 정하고 원점으로 리셋 ──────────
    void BeginPitch()
    {
        if (pitchOrigin == null || strikeZoneCenter == null) return;

        gameObject.SetActive(true); // 이전에 스트라이크로 숨겨졌을 수 있으니 다시 켬

        Vector3 target = strikeZoneCenter.position + Vector3.up * strikeZoneHeight;
        if (!GameManager.Instance.IsStrikePitch)
        {
            target += strikeZoneCenter.right * Random.Range(-ballOffsetRange, ballOffsetRange)
                    + strikeZoneCenter.up    * Random.Range(-ballOffsetRange, ballOffsetRange);
        }
        pitchTarget = target;

        rb.isKinematic     = true; // kinematic 상태에서는 velocity 설정 불가 — 다음 Launch()에서 항상 새로 덮어씀
        transform.position = OriginPos;
        inFlight = true;
    }

    // ── 투구 중: GameManager 타이머 비율에 맞춰 직접 이동 ──
    void Update()
    {
        if (!inFlight) return;

        var gm = GameManager.Instance;
        float t = Mathf.Clamp01(gm.PitchTimer / gm.PitchDuration);

        // TimingTarget 시점에 스트존 도착 → 그 이후엔 지나쳐서 계속 날아감 (LerpUnclamped)
        float arrival = t / Mathf.Max(gm.TimingTarget, 0.0001f);
        Vector3 straightPos = Vector3.LerpUnclamped(OriginPos, pitchTarget, arrival);
        transform.position = straightPos + GetCurveOffset(gm.CurrentPitch, t);

        if (t >= 1f) inFlight = false;
    }

    Vector3 GetCurveOffset(PitchType pitch, float t)
    {
        float curveT = t * t; // 갈수록 더 크게 휘어짐
        return pitch switch
        {
            PitchType.Curveball => Vector3.down          * curveballDrop * curveT,
            PitchType.Slider    => strikeZoneCenter.right * sliderSide    * curveT,
            PitchType.Changeup  => Vector3.down          * changeupDrop  * curveT,
            _                    => Vector3.zero
        };
    }

    // ── 결과 반영: 맞은 공은 날아가고, 아니면 그 자리에 멈춤 ─
    void HandleResult(BatterResult result)
    {
        inFlight = false;

        switch (result)
        {
            case BatterResult.Out:      Launch(outForce);              break;
            case BatterResult.Single:   Launch(singleForce);           break;
            case BatterResult.Double:   Launch(doubleForce);           break;
            case BatterResult.Triple:   Launch(tripleForce);           break;
            case BatterResult.HomeRun:  Launch(homerunForce);          break;
            case BatterResult.Foul:     Launch(foulForce, wide: true); break;

            case BatterResult.Strike:
                StartCoroutine(VanishAfterDelay(strikeVanishDelay));
                break;
            // Ball: 스트존 통과 지점에 그대로 멈춤
        }
    }

    IEnumerator VanishAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }

    void Launch(float force, bool wide = false)
    {
        rb.isKinematic = false;

        Vector3 dir = strikeZoneCenter.forward + Vector3.up * 0.7f;
        float sideRange = wide ? 1.2f : 0.4f;
        dir += strikeZoneCenter.right * Random.Range(-sideRange, sideRange);

        rb.linearVelocity = dir.normalized * force;
    }
}
