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
    [SerializeField] private float minStateHoldTime = 0.25f; // 상태 전환 후 최소 이 시간 동안은 다시 안 바뀌게 (떨림 방지)

    private Animator animator;
    private AnimatorOverrideController overrideCtrl;
    private List<KeyValuePair<AnimationClip, AnimationClip>> overridesList;
    private int slotIndex = -1;

    private Vector3 lastPosition;
    private bool isRunning;
    private float lastStateChangeTime = -999f;
    private const string IDLE_STATE = "Baseball Idle"; // Batter.cs와 동일한 스테이트 이름 가정

    void Start()
    {
        SetupAnimator();
        lastPosition = transform.position;
        Debug.Log($"[RunnerAnimator] Start 완료 — idleClip={(idleClip==null?"NULL":idleClip.name)}, runClip={(runClip==null?"NULL":runClip.name)}, slotIndex={slotIndex}");
        PlayIdle();
    }

    void Update()
    {
        if (slotIndex < 0) return;

        float speed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        bool shouldRun = speed > moveThreshold;

        // 최근에 막 전환했으면(떨림 방지용 최소 유지시간 이내) 이번 프레임은 무시
        if (shouldRun != isRunning && Time.time - lastStateChangeTime >= minStateHoldTime)
        {
            Debug.Log($"[RunnerAnimator] 상태 전환: isRunning {isRunning} → {shouldRun} (speed={speed:F2}, threshold={moveThreshold})");
            isRunning = shouldRun;
            lastStateChangeTime = Time.time;
            if (isRunning) PlayRun(); else PlayIdle();
        }
    }

    // Play 모드에서 컴포넌트 우클릭 → 이 항목으로 이동/스폰 로직 다 건너뛰고 강제 재생 테스트 가능
    [ContextMenu("TEST: Force Play Run")]
    void TestForcePlayRun() => PlayRun();

    void PlayRun()
    {
        Debug.Log($"[RunnerAnimator] PlayRun() 호출됨, runClip={(runClip==null?"NULL — 여기서 멈춤":runClip.name)}, 호출 전 animator.speed={animator.speed}");
        if (runClip == null) return;
        SetOverride(runClip);
        animator.Play(IDLE_STATE, 0, 0f);
        animator.speed = 1f;
        Debug.Log($"[RunnerAnimator] Play('{IDLE_STATE}') 호출 완료, 현재 스테이트: {animator.GetCurrentAnimatorStateInfo(0).IsName(IDLE_STATE)}, speed={animator.speed}");
    }

    void PlayIdle()
    {
        if (idleClip == null) return;
        SetOverride(idleClip);
        animator.Play(IDLE_STATE, 0, 0f);
        animator.speed = 1f;
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

        var baseController = animator.runtimeAnimatorController;
        Debug.Log($"[RunnerAnimator] baseController = {(baseController == null ? "NULL" : baseController.name)} " +
                   $"(type: {baseController?.GetType().Name})");

        overrideCtrl  = new AnimatorOverrideController(baseController);
        overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideCtrl.overridesCount);
        overrideCtrl.GetOverrides(overridesList);

        Debug.Log($"[RunnerAnimator] overridesCount = {overrideCtrl.overridesCount}, overridesList.Count = {overridesList.Count}");
        for (int i = 0; i < overridesList.Count; i++)
            Debug.Log($"[RunnerAnimator]   slot {i}: Key = {(overridesList[i].Key == null ? "null" : overridesList[i].Key.name)}");

        for (int i = 0; i < overridesList.Count; i++)
        {
            if (overridesList[i].Key != null) { slotIndex = i; break; }
        }

        if (slotIndex < 0) { Debug.LogError("[RunnerAnimator] Override 슬롯 없음"); return; }
        animator.runtimeAnimatorController = overrideCtrl;
    }
}
