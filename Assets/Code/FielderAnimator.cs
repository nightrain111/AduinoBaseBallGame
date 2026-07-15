using System.Collections.Generic;
using UnityEngine;

// 수비수(포수/내야수/외야수/투수)용 애니메이션 — 액션 없이 대기 자세 하나만 재생.
[RequireComponent(typeof(Animator))]
public class FielderAnimator : MonoBehaviour
{
    [SerializeField] private AnimationClip idleClip;

    private const string IDLE_STATE = "Baseball Idle"; // Batter.cs와 동일한 스테이트 이름 가정

    void Start()
    {
        if (idleClip == null) { Debug.LogWarning("[FielderAnimator] Idle Clip 없음"); return; }

        var animator = GetComponent<Animator>();
        if (animator == null) { Debug.LogError("[FielderAnimator] Animator 없음"); return; }

        var overrideCtrl  = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideCtrl.overridesCount);
        overrideCtrl.GetOverrides(overridesList);

        for (int i = 0; i < overridesList.Count; i++)
        {
            if (overridesList[i].Key != null)
            {
                overridesList[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridesList[i].Key, idleClip);
                break;
            }
        }

        overrideCtrl.ApplyOverrides(overridesList);
        animator.runtimeAnimatorController = overrideCtrl;
        animator.Play(IDLE_STATE, 0, 0f);
    }
}
