using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class Batter : MonoBehaviour
{
    [Header("애니메이션 클립 (Inspector에서 연결)")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip hitClip;
    [SerializeField] private AnimationClip strikeClip;
    [SerializeField] private AnimationClip buntClip;
    [SerializeField] private AnimationClip stepUpClip;

    [Header("애니메이션 속도 (구간별)")]
    [SerializeField] private float windupSpeed  = 2.5f;  // 클립 앞부분(준비 동작) 재생 속도
    [SerializeField] private float contactSpeed = 1f;    // 클립 뒷부분(임팩트~마무리) 재생 속도
    [SerializeField, Range(0f, 1f)] private float windupPortion = 0.35f; // 클립의 앞부분 몇 %까지를 빠르게 재생할지
    [SerializeField, Range(0f, 1f)] private float swingCompleteThreshold = 0.6f; // 스윙 클립에서 여기까지만 재생하고 다음으로 넘어감 (클립 뒤에 붙은 달리기 동작 잘라내기용)

    [Header("진출(안타) 시 화면 밖으로 뛰어가기")]
    [SerializeField] private Transform runOffTarget; // 안타 났을 때 스윙 후 뛰어갈 목표 지점 (화면 밖/1루 방향)
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private float runOffSpeed = 5f;

    [Header("스윙 중 위치 드리프트 제한")]
    [SerializeField] private float maxSwingDrift = 0.15f; // Root Motion으로 스윙 중 원래 자리에서 벗어날 수 있는 최대 거리

    private bool constrainToHome = true; // RunOffScreen 중엔 false로 꺼서 자유롭게 이동하게 함

    // ── Animator Override ────────────────────────────────
    private Animator animator;
    private AnimatorOverrideController overrideCtrl;
    private List<KeyValuePair<AnimationClip, AnimationClip>> overridesList;
    private int idleSlotIndex = -1;

    private bool isAnimating;
    private bool pendingRunOff;
    private const string IDLE_STATE = "Baseball Idle";

    private Vector3    homePosition;
    private Quaternion homeRotation;

    private BatAttach batAttach;

    // ════════════════════════════════════════════════════
    void Start()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;

        batAttach = GetComponent<BatAttach>();

        SetupAnimator();
        PlayIdle();
    }

    void OnEnable()  => ArduinoSwingInput.OnSwingDetected += HandleArduinoSwing;
    void OnDisable() => ArduinoSwingInput.OnSwingDetected -= HandleArduinoSwing;

    // Root Motion을 직접 처리 — 발 움직임은 자연스럽게 살리되, 원래 자리(homePosition)에서
    // maxSwingDrift 이상 벗어나지 않게 제한 (스윙 중간에 위치가 크게 밀리는 것 방지)
    void OnAnimatorMove()
    {
        if (!constrainToHome) return; // RunOffScreen 중엔 코루틴이 위치를 직접 제어하므로 건너뜀

        Vector3 newPos = transform.position + animator.deltaPosition;
        Vector3 offset = newPos - homePosition;
        if (offset.magnitude > maxSwingDrift)
            newPos = homePosition + offset.normalized * maxSwingDrift;

        transform.position = newPos;
        transform.rotation *= animator.deltaRotation;
    }

    // 아두이노 배트 스윙 센서 입력 (블루투스 SPP) — 키보드 Z키와 동일하게 처리
    void HandleArduinoSwing(float intensity) => TrySwing();

    // GameManager에서 투구 시작 시 호출
    public void OnPitchBegin()
    {
        // 직전 타석에서 안타로 화면 밖에 나가있었을 수 있으니, 새 타석 시작 시 홈으로 복귀 + 휘두르기 전(대기) 자세로
        PlayIdle();
        batAttach?.AttachBat(); // 떨어뜨렸던 배트 다시 손에

        // 필요 시 타석 진입 애니 재생:
        // if (!isAnimating) PlayAction(stepUpClip, "StepUp");
    }

    void Update()
    {
        if (animator == null || idleSlotIndex < 0) return;

        // 액션 애니 진행 중: 앞부분/뒷부분 속도 다르게 적용 + 완료 시 Idle 복귀
        if (isAnimating)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0))
            {
                animator.speed = info.normalizedTime < windupPortion ? windupSpeed : contactSpeed;

                if (info.normalizedTime >= swingCompleteThreshold)
                {
                    isAnimating = false;
                    if (pendingRunOff) { pendingRunOff = false; StartCoroutine(RunOffScreen()); }
                    else PlayIdle();
                }
            }
        }

        HandleInput();
    }

    // ── 입력 처리 ─────────────────────────────────────────
    void HandleInput()
    {
        // 투구 중일 때만 스윙 입력 받음
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameState.Pitching) return;

        if      (Keyboard.current.zKey.wasPressedThisFrame) TrySwing();
        else if (Keyboard.current.cKey.wasPressedThisFrame) TryBunt();
    }

    void TrySwing()
    {
        var gm = GameManager.Instance;

        // GameManager 없을 때 테스트 모드
        if (gm == null) { PlayAction(hitClip, "Hit"); return; }

        BatterResult result = gm.RegisterSwing();
        if (result == BatterResult.None) return;

        switch (result)
        {
            case BatterResult.Strike:
                PlayAction(strikeClip, "Strike");
                break;

            case BatterResult.Foul:
            case BatterResult.Out:
                PlayAction(hitClip, "Hit"); // 맞추긴 했지만 진출 없음 — 스윙만 재생
                break;

            case BatterResult.Single:
            case BatterResult.Double:
            case BatterResult.Triple:
            case BatterResult.HomeRun:
                pendingRunOff = true;
                PlayAction(hitClip, "Hit"); // 스윙 끝나면 Update()에서 RunOffScreen()으로 이어짐
                break;
        }
    }

    void TryBunt()
    {
        // 번트는 차후 게임로직 연동 예정
        PlayAction(buntClip, "Bunt");
    }

    // ── 애니메이션 ────────────────────────────────────────
    void PlayAction(AnimationClip clip, string label)
    {
        if (clip == null) { Debug.LogWarning($"[Batter] {label} 클립 없음 — Inspector 확인"); return; }
        SetOverride(clip);
        animator.Play(IDLE_STATE, 0, 0f);
        animator.speed = windupSpeed; // 시작은 항상 빠른 앞부분 구간
        isAnimating = true;
        Debug.Log($"[Batter] {label}");
    }

    // 안타류 결과 후 스윙이 끝나면 호출 — 화면 밖(runOffTarget)으로 실제로 뛰어감.
    // 목표 지점 도착하면 바로 홈으로 복귀 (실제 베이스 주자 표현은 BaseRunnerManager가 별도로 담당)
    IEnumerator RunOffScreen()
    {
        if (runOffTarget == null) { PlayIdle(); yield break; }

        constrainToHome = false; // 이제 자유롭게 이동 — Root Motion 제한 해제
        batAttach?.DropBat();

        if (runClip != null)
        {
            SetOverride(runClip);
            animator.Play(IDLE_STATE, 0, 0f);
            animator.speed = 1f;
        }

        while (Vector3.Distance(transform.position, runOffTarget.position) > 0.2f)
        {
            Vector3 dir = runOffTarget.position - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, runOffTarget.position, runOffSpeed * Time.deltaTime);
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            yield return null;
        }

        // 도착하면 바로 원래 자리(홈)로 복귀 + 대기 자세 + 배트 다시 쥐기
        constrainToHome = true;
        PlayIdle();
        batAttach?.AttachBat();
    }

    void PlayIdle()
    {
        if (idleSlotIndex < 0) return;
        SetOverride(idleClip != null ? idleClip : overridesList[idleSlotIndex].Key);
        animator.Play(IDLE_STATE, 0, 0f);
        animator.speed = 1f; // 대기 동작은 항상 원래 속도

        // Root Motion으로 액션 중 밀린 위치를 원래 자리로 복귀 (드리프트 누적 방지)
        transform.SetPositionAndRotation(homePosition, homeRotation);
    }

    void SetOverride(AnimationClip clip)
    {
        overridesList[idleSlotIndex] = new KeyValuePair<AnimationClip, AnimationClip>(
            overridesList[idleSlotIndex].Key, clip);
        overrideCtrl.ApplyOverrides(overridesList);
    }

    void SetupAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null) { Debug.LogError("[Batter] Animator 없음"); return; }

        overrideCtrl  = new AnimatorOverrideController(animator.runtimeAnimatorController);
        overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideCtrl.overridesCount);
        overrideCtrl.GetOverrides(overridesList);

        for (int i = 0; i < overridesList.Count; i++)
        {
            string keyName = overridesList[i].Key?.name ?? "null";
            if (idleClip != null && keyName == idleClip.name)
                idleSlotIndex = i;
            else if (idleSlotIndex < 0 && overridesList[i].Key != null)
                idleSlotIndex = i;
        }

        if (idleSlotIndex < 0) { Debug.LogError("[Batter] Override 슬롯 없음"); return; }
        animator.runtimeAnimatorController = overrideCtrl;
        Debug.Log("[Batter] 초기화 완료");
    }
}
