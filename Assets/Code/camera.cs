using UnityEngine;

// 타자 옆쪽 위에서 비스듬히 내려다보는 방송 중계 사이드 앵글 카메라.
// 타자가 크게 보이고 투수가 뒤쪽 배경에 작게 보이는 구도.
[RequireComponent(typeof(Camera))]
public class BattingCameraController : MonoBehaviour
{
    [Header("기준 대상")]
    [SerializeField] private Transform batterTarget; // 타자(플레이어) 트랜스폼 — cameraAnchor 없을 때 위치 계산 기준
    [SerializeField] private Transform focusPoint;    // 카메라가 실제로 바라볼 지점 — 비워두면 타자를 봄
    [SerializeField] private Transform cameraAnchor;  // 카메라 위치를 이 오브젝트에 직접 고정 — 채우면 아래 오프셋 수치 전부 무시하고 이 위치를 그대로 씀

    [Header("카메라 오프셋 (멀리 떨어뜨리고 망원으로 당기는 방식 — 실제 방송캠 원리)")]
    [SerializeField] private float distanceBehind = 3f;   // 타자 등 뒤로 떨어지는 거리 — sideOffset보다 훨씬 작아야 측면이 보임
    [SerializeField] private float sideOffset      = 8f;   // 타자 옆으로 떨어지는 거리 — 이게 훨씬 커야 측면 프로필이 나옴 (음수면 반대쪽)
    [SerializeField] private float height          = 1.7f; // 카메라 높이 — 눈높이에 가깝게 낮춰서 땅 대신 하늘이 많이 보이게
    [SerializeField] private float lookHeight      = 1.65f; // 타자의 어느 높이를 바라볼지 (머리/어깨 쪽 — 살짝 위를 보게)

    [Header("렌즈")]
    [SerializeField] private float fieldOfView = 18f; // 멀리서 당겨찍는 망원 — 숫자 작을수록 더 압축된 느낌

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (cam != null) cam.fieldOfView = fieldOfView;

        if (cameraAnchor != null)
        {
            transform.position = cameraAnchor.position;
        }
        else if (batterTarget != null)
        {
            Vector3 forward = batterTarget.forward;
            Vector3 right   = batterTarget.right;

            transform.position = batterTarget.position
                                - forward * distanceBehind
                                + right   * sideOffset
                                + Vector3.up * height;
        }
        else return;

        if (focusPoint != null)
        {
            // 특정 지점이 아니라 focusPoint가 가리키는 "방향"을 그대로 바라봄
            transform.rotation = Quaternion.LookRotation(focusPoint.forward, Vector3.up);
        }
        else if (batterTarget != null)
        {
            transform.LookAt(batterTarget.position + Vector3.up * lookHeight);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 camPos = cameraAnchor != null ? cameraAnchor.position
                        : batterTarget != null ? batterTarget.position - batterTarget.forward * distanceBehind + batterTarget.right * sideOffset + Vector3.up * height
                        : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(camPos, 0.2f);

        if (focusPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(camPos, camPos + focusPoint.forward * 20f);
        }
        else if (batterTarget != null)
        {
            Vector3 lookPos = batterTarget.position + Vector3.up * lookHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lookPos, 0.3f);
            Gizmos.DrawLine(camPos, lookPos);
        }
    }
}
