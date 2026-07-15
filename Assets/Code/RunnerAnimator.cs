using System.Collections.Generic;
using UnityEngine;

// 주자용 애니메이션 컨트롤러. 실제로 이동 중인지 감지해서 Idle/Run을 자동 전환한다.
// BaseRunnerManager가 transform.position을 직접 옮기는 방식이라, 별도 훅 없이 위치 변화량으로 판단.
[RequireComponent(typeof(Animator))]
public class RunnerAnimator : MonoBehaviour
{
    [Header("애니메이션 클립 (Inspector에서 연결)")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip runClip;

    [Header("움직임 감지 (초당 이 거리 이상 움직이면 Run)")]
    [SerializeField] private float moveThreshold = 0.3f;

    private Animator animator;
    private AnimatorOverrideController overrideCtrl;
    private List<KeyValuePair<AnimationClip, AnimationClip>> overridesList;
    private int slotIndex = -1;

    private Vector3 lastPosition;
    private bool isRunning;
    private const string IDLE_STATE = "Baseball Idle"; // Batter.cs와 동일한 스테이트 이름 가정

    void Start()
    {
        SetupAnimator();
        lastPosition = transform.position;
        PlayIdle();
    }

    void Update()
    {
        if (slotIndex < 0) return;

        float speed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        bool shouldRun = speed > moveThreshold;
        if (shouldRun != isRunning)
        {
            isRunning = shouldRun;
            if (isRunning) PlayRun(); else PlayIdle();
        }
    }

    void PlayRun()
    {
        if (runClip == null) return;
        SetOverride(runClip);
        animator.Play(IDLE_STATE, 0, 0f);
    }

    void PlayIdle()
    {
        if (idleClip == null) return;
        SetOverride(idleClip);
        animator.Play(IDLE_STATE, 0, 0f);
    }

    void SetOverride(AnimationClip clip)
    {
        if (slotIndex < 0) return;
        overridesList[slotIndex] = new KeyValuePair<AnimationClip, AnimationClip>(overridesList[slotIndex].Key, clip);
        overrideCtrl.ApplyOverrides(overridesList);
    }

    void SetupAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null) { Debug.LogError("[RunnerAnimator] Animator 없음"); return; }

        overrideCtrl  = new AnimatorOverrideController(animator.runtimeAnimatorController);
        overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideCtrl.overridesCount);
        overrideCtrl.GetOverrides(overridesList);

        for (int i = 0; i < overridesList.Count; i++)
        {
            if (overridesList[i].Key != null) { slotIndex = i; break; }
        }

        if (slotIndex < 0) { Debug.LogError("[RunnerAnimator] Override 슬롯 없음"); return; }
        animator.runtimeAnimatorController = overrideCtrl;
    }
}
