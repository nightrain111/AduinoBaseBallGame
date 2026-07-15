using UnityEngine;

// 배트를 타자 캐릭터의 손 본(bone)에 자동으로 붙인다. 휴머노이드(Humanoid) 아바타 전제.
// DropBat()으로 손에서 놓아 물리로 떨어뜨리고, AttachBat()으로 다시 손에 붙일 수 있다.
public class BatAttach : MonoBehaviour
{
    [SerializeField] private Animator animator;   // 타자 캐릭터의 Animator
    [SerializeField] private Transform bat;       // 씬에 있는 배트 오브젝트
    [SerializeField] private bool useRightHand = true;

    [Header("손잡이 위치/각도 보정 (배트 모델 피벗에 맞게 조정)")]
    [SerializeField] private Vector3 localPosition;
    [SerializeField] private Vector3 localRotationEuler;

    private Transform hand;

    void Start()
    {
        if (animator == null || bat == null) return;

        hand = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (hand == null) { Debug.LogWarning("[BatAttach] 손 본을 찾을 수 없음 — Humanoid 아바타 설정 확인"); return; }

        AttachBat();
    }

    // 배트를 손에 붙임 (원래 손잡이 위치/각도로)
    public void AttachBat()
    {
        if (bat == null || hand == null) return;

        var rb = bat.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        var col = bat.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        bat.SetParent(hand, false);
        bat.localPosition = localPosition;
        bat.localRotation = Quaternion.Euler(localRotationEuler);
    }

    // 손에서 놓아서 물리로 떨어지게 함 (뛸 때 등)
    public void DropBat()
    {
        if (bat == null) return;

        bat.SetParent(null, true); // 월드 위치/회전 유지한 채로 부모 해제

        var rb = bat.GetComponent<Rigidbody>();
        if (rb == null) rb = bat.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;

        var col = bat.GetComponent<Collider>();
        if (col == null) col = bat.gameObject.AddComponent<BoxCollider>();
        col.enabled = true;
    }
}
