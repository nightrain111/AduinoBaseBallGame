using UnityEngine;
using UnityEngine.UI;

// 화면에 표시되는 판정 칸 테두리 (Screen Space Overlay). 격자/판정 색변화 없이
// 반투명 사각 테두리 하나만 표시 — 컴투스 프로야구류 UI 참고.
// 실제 판정 기준(Ball.cs가 쓰는 strikeZoneCenter + ballOffsetRange)을 카메라로 화면에 투영해서
// 배치하기 때문에, 카메라 구도가 바뀌어도 항상 실제 스트존과 맞는 위치/크기로 나온다.
public class StrikeZoneUI : MonoBehaviour
{
    [Header("판정 기준 (Ball.cs / GameManager와 동일하게 맞추기)")]
    [SerializeField] private Transform strikeZoneCenter; // Ball.cs와 동일한 오브젝트 권장
    [SerializeField] private float heightOffset  = 1.0f;  // Ball.cs의 Strike Zone Height와 동일하게
    [SerializeField] private float zoneHalfWidth  = 0.6f;  // GameManager의 Ball Offset Range와 동일하게
    [SerializeField] private float zoneHalfHeight = 0.6f;

    [Header("카메라 (비워두면 Camera.main)")]
    [SerializeField] private Camera targetCamera;

    [Header("미세 조정 (계산된 위치에서 몇 px 더 밀고 싶을 때)")]
    [SerializeField] private Vector2 pixelNudge = Vector2.zero;

    [Header("테두리")]
    [SerializeField] private float borderThickness = 4f;
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.55f);

    private RectTransform boxRT;

    void Start()
    {
        BuildUI();
    }

    void LateUpdate()
    {
        if (boxRT == null || strikeZoneCenter == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        Vector3 center = strikeZoneCenter.position + Vector3.up * heightOffset;
        Vector3 screenCenter = cam.WorldToScreenPoint(center);
        Vector3 screenRight  = cam.WorldToScreenPoint(center + strikeZoneCenter.right * zoneHalfWidth);
        Vector3 screenUp     = cam.WorldToScreenPoint(center + strikeZoneCenter.up * zoneHalfHeight);

        // 화면 중앙 기준 anchoredPosition으로 변환 (Canvas는 화면 정중앙이 원점인 anchoredPosition을 씀)
        boxRT.anchoredPosition = new Vector2(screenCenter.x - Screen.width * 0.5f, screenCenter.y - Screen.height * 0.5f) + pixelNudge;
        boxRT.sizeDelta = new Vector2(
            Mathf.Abs(screenRight.x - screenCenter.x) * 2f,
            Mathf.Abs(screenUp.y    - screenCenter.y) * 2f
        );
    }

    // ── UI 생성 ────────────────────────────────────────────
    void BuildUI()
    {
        var canvasGO = new GameObject("StrikeZoneCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(canvasGO.transform, false);
        boxRT = box.GetComponent<RectTransform>();
        boxRT.anchorMin = boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        boxRT.pivot = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta = new Vector2(zoneHalfWidth, zoneHalfHeight) * 200f; // LateUpdate에서 바로 덮어씀 — 첫 프레임 임시값

        CreateBorderPanel(boxRT, "BorderTop");
        CreateBorderPanel(boxRT, "BorderBottom");
        CreateBorderPanel(boxRT, "BorderLeft");
        CreateBorderPanel(boxRT, "BorderRight");
    }

    // 테두리 4개는 부모(Box)의 크기를 매 프레임 따라가야 하므로, 앵커를 부모 변에 딱 붙여서 자동으로 늘어나게 구성
    void CreateBorderPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        go.GetComponent<Image>().color = borderColor;

        switch (name)
        {
            case "BorderTop":
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, borderThickness);
                rt.anchoredPosition = Vector2.zero;
                break;
            case "BorderBottom":
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, borderThickness);
                rt.anchoredPosition = Vector2.zero;
                break;
            case "BorderLeft":
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(borderThickness, 0f);
                rt.anchoredPosition = Vector2.zero;
                break;
            case "BorderRight":
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(borderThickness, 0f);
                rt.anchoredPosition = Vector2.zero;
                break;
        }
    }
}
