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

    // ── Animator Override ────────────────────────────────
    private Animator animator;
    private AnimatorOverrideController overrideCtrl;
    private List<KeyValuePair<AnimationClip, AnimationClip>> overridesList;
    private int idleSlotIndex = -1;

    private bool isPitching;

    private Vector3    homePosition;
    private Quaternion homeRotation;

    // ════════════════════════════════════════════════════
    void Start()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;

        SetupAnimator();
        PlayIdle();
    }

    // GameManager.StartPitch()에서 호출. pitchDuration(공이 도착하기까지 걸리는 시간)에 맞춰
    // 투구 애니메이션 재생 속도를 조절해 타이밍과 동작이 어긋나지 않게 한다.
    public void Pitch(float pitchDuration)
    {
        if (pitchClip == null) { Debug.LogWarning("[Pitcher] pitchClip 없음 — Inspector 확인"); return; }
        if (idleSlotIndex < 0) return;

        SetOverride(pitchClip);
        animator.speed = pitchDuration > 0f ? pitchClip.length / pitchDuration : 1f;
        animator.Play(idleStateName, 0, 0f);
        isPitching = true;
        Debug.Log($"[Pitcher] 투구 애니메이션 재생 (speed x{animator.speed:F2})");
    }

    void Update()
    {
        if (animator == null || idleSlotIndex < 0 || !isPitching) return;

        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!animator.IsInTransition(0) && info.normalizedTime >= 0.95f)
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
        animator.Play(idleStateName, 0, 0f);
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
