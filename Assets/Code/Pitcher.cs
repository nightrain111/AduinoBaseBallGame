using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Batter.cs와 동일한 AnimatorOverrideController 스왑 방식.
// Inspector에서 Animator에 베이스 컨트롤러를 미리 연결해두면, 그 안의 슬롯 하나를
// idle/pitch 클립으로 갈아끼우며 재생한다.
[RequireComponent(typeof(Animator))]
public class Pitcher : MonoBehaviour
{
    [Header("애니메이션 클립 (Inspector에서 연결)")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip pitchClip;

    [Header("애니메이터 상태 이름 (Base Layer의 재생 상태명)")]
    [SerializeField] private string idleStateName = "Idle";

    [Header("투구 재생 속도 제한 (구속에 따라 pitchDuration이 크게 달라져도 동작이 과하게 느려지거나 빨라지지 않게)")]
    [SerializeField] private float minPlaySpeed = 0.85f;
    [SerializeField] private float maxPlaySpeed = 1.3f;

    [Header("Root Motion 위치 드리프트 제한 (Batter.cs와 동일)")]
    [SerializeField] private float maxDrift = 0.15f;

    [Header("투구 클립 자체 방향 보정 (클립 안에서 몸이 정면을 안 보고 있을 때, 재생 전 미리 이 각도만큼 돌려서 상쇄)")]
    [SerializeField] private float pitchFacingOffset = 0f; // Y축 기준 도(degree). 실제로 재생해보면서 딱 정면으로 보이는 값 찾기

    // ── Animator Override ────────────────────────────────
    private Animator animator;
    private AnimatorOverrideController overrideCtrl;
    private List<KeyValuePair<AnimationClip, AnimationClip>> overridesList;
    private int idleSlotIndex = -1;

    private bool  isPitching;
    private float pitchStartTime;
    private float pitchExpectedDuration;

    private Vector3    homePosition;
    private Quaternion homeRotation;
    private Quaternion lockRotation; // OnAnimatorMove가 매 프레임 고정할 목표 회전 (평소=homeRotation, 투구 중=보정 적용)

    // ════════════════════════════════════════════════════
    void Start()
    {
        StartCoroutine(InitAfterPlacement());
    }

    // DefenseSetup.Start()가 투수를 마운드로 옮기는데, 다른 오브젝트의 Start() 실행 순서는 보장되지 않으므로
    // 한 프레임 기다렸다가 홈 위치를 캡처한다 (모든 오브젝트의 Start()는 첫 Update() 전에 끝나 있음).
    IEnumerator InitAfterPlacement()
    {
        yield return null;

        homePosition = transform.position;
        homeRotation = transform.rotation;
        lockRotation = homeRotation;

        SetupAnimator();
        PlayIdle();
    }

    // Batter.cs와 동일한 이유 — Root Motion 회전이 그대로 적용되면 투구 동작(상체/골반 회전)에
    // 몸 전체가 딸려 돌아가서 옆을 보고 던지는 것처럼 보임. 원래 자리/방향 기준으로 제한한다.
    void OnAnimatorMove()
    {
        Vector3 newPos = transform.position + animator.deltaPosition;
        Vector3 offset = newPos - homePosition;
        if (offset.magnitude > maxDrift)
            newPos = homePosition + offset.normalized * maxDrift;

        transform.position = newPos;
        transform.rotation = lockRotation;
    }

    // GameManager.StartPitch()에서 호출. pitchDuration(공이 도착하기까지 걸리는 시간)에 맞춰
    // 투구 애니메이션 재생 속도를 조절해 타이밍과 동작이 어긋나지 않게 한다.
    public void Pitch(float pitchDuration)
    {
        if (pitchClip == null) { Debug.LogWarning("[Pitcher] pitchClip 없음 — Inspector 확인"); return; }
        if (idleSlotIndex < 0) return;

        SetOverride(pitchClip);
        float rawSpeed = pitchDuration > 0f ? pitchClip.length / pitchDuration : 1f;
        animator.speed = Mathf.Clamp(rawSpeed, minPlaySpeed, maxPlaySpeed);

        lockRotation = homeRotation * Quaternion.AngleAxis(pitchFacingOffset, Vector3.up);
        transform.rotation = lockRotation; // OnAnimatorMove가 다음 프레임에 잡아주기 전에 미리 반영

        ForceRestartState();

        isPitching            = true;
        pitchStartTime         = Time.time;
        pitchExpectedDuration  = pitchClip.length / animator.speed;
        Debug.Log($"[Pitcher] 투구 애니메이션 재생 (speed x{animator.speed:F2}, {pitchExpectedDuration:F2}s, " +
                  $"재생 직후 normalizedTime={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3})");
    }

    // 같은 스테이트("Idle")를 계속 재사용하는 구조라, 그냥 Play(0f)만 호출하면 Unity가
    // "이미 그 스테이트에 있다"고 판단해 재생 위치 리셋을 씹는 경우가 있다.
    // 일부러 다른 지점(0.999)으로 먼저 Play해서 강제로 상태를 "변경"시킨 뒤, 다시 0으로 Play하면
    // 확실히 리셋됨 (Mecanim에서 널리 쓰이는 우회법).
    void ForceRestartState()
    {
        animator.Play(idleStateName, 0, 0.999f);
        animator.Update(0f);
        animator.Play(idleStateName, 0, 0f);
        animator.Update(0f);
    }

    void Update()
    {
        if (animator == null || idleSlotIndex < 0 || !isPitching) return;

        // Idle 상태를 재사용하는 구조라 GetCurrentAnimatorStateInfo().normalizedTime은 대기 중에도 계속 누적됨 —
        // Play() 직후 Animator가 갱신되기 전에 그 누적값을 읽으면 즉시 "끝남"으로 오판하는 문제가 있어서
        // 실제 경과 시간을 직접 재는 방식으로 판정한다.
        if (Time.time - pitchStartTime >= pitchExpectedDuration * 0.95f)
        {
            isPitching = false;
            animator.speed = 1f;
            PlayIdle();
        }
    }

    // ── 애니메이션 ────────────────────────────────────────
    void PlayIdle()
    {
        if (idleSlotIndex < 0) return;
        SetOverride(idleClip != null ? idleClip : overridesList[idleSlotIndex].Key);
        ForceRestartState();

        lockRotation = homeRotation;
        // Root Motion으로 밀리거나 돌아간 위치/방향을 원래 자리로 복귀 (드리프트 누적 방지)
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
        if (animator == null) { Debug.LogError("[Pitcher] Animator 없음"); return; }

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

        if (idleSlotIndex < 0) { Debug.LogError("[Pitcher] Override 슬롯 없음"); return; }
        animator.runtimeAnimatorController = overrideCtrl;
        Debug.Log("[Pitcher] 초기화 완료");
    }
}
