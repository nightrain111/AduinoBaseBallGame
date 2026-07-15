using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 임시(스켈레톤) 게임 UI.
// 씬에 직접 배치할 필요 없이 게임 시작 시 자동으로 생성되어 GameManager 이벤트를 구독한다.
// 디자인 시안이 나오면 BuildUI()의 레이아웃/스타일만 교체하면 됨.
public class GameUI : MonoBehaviour
{
    private static readonly Color EmptyColor    = new Color(1f, 1f, 1f, 0.25f);
    private static readonly Color OccupiedColor = new Color(1f, 0.85f, 0.1f, 1f);

    [SerializeField] private float maxResultDisplayTime = 3f; // 결과 텍스트 최대 표시 시간 (그 전에 다음 투구 시작하면 그때 사라짐)

    private Text scoreText;
    private Text inningText;
    private Text countText;
    private Text resultText;
    private Text sideChangeText;
    private Image firstBaseIcon, secondBaseIcon, thirdBaseIcon;

    private Coroutine resultClearCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        new GameObject("GameUI (Auto)", typeof(GameUI));
    }

    void Awake() => BuildUI();

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogWarning("[GameUI] GameManager.Instance 없음"); return; }

        gm.OnCountChanged += RefreshCount;
        gm.OnBasesChanged += RefreshBases;
        gm.OnResult        += ShowResult;
        gm.OnPitchStart    += ClearResult;
        gm.OnSideChanged   += ShowSideChanged;
        gm.OnGameOver      += ShowGameOver;

        RefreshCount();
        RefreshBases();
        resultText.text = "";
        sideChangeText.text = "";
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnCountChanged -= RefreshCount;
        gm.OnBasesChanged -= RefreshBases;
        gm.OnResult        -= ShowResult;
        gm.OnPitchStart    -= ClearResult;
        gm.OnSideChanged   -= ShowSideChanged;
        gm.OnGameOver      -= ShowGameOver;
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────
    void RefreshCount()
    {
        var gm = GameManager.Instance;
        scoreText.text  = $"AWAY {gm.AwayScore}  :  {gm.HomeScore} HOME";
        inningText.text = $"{gm.Inning}회 {(gm.IsTopInning ? "초" : "말")}";
        countText.text  = $"B {gm.Balls}   S {gm.Strikes}   O {gm.Outs}";
    }

    void RefreshBases()
    {
        var gm = GameManager.Instance;
        firstBaseIcon.color  = gm.RunnerOnFirst  ? OccupiedColor : EmptyColor;
        secondBaseIcon.color = gm.RunnerOnSecond ? OccupiedColor : EmptyColor;
        thirdBaseIcon.color  = gm.RunnerOnThird  ? OccupiedColor : EmptyColor;
    }

    void ShowResult(BatterResult result)
    {
        resultText.text = result switch
        {
            BatterResult.Strike  => "스트라이크",
            BatterResult.Ball    => "볼",
            BatterResult.Foul    => "파울",
            BatterResult.Out     => "아웃",
            BatterResult.Single  => "1루타!",
            BatterResult.Double  => "2루타!",
            BatterResult.Triple  => "3루타!",
            BatterResult.HomeRun => "홈런!!",
            _                    => ""
        };

        if (resultClearCoroutine != null) StopCoroutine(resultClearCoroutine);
        resultClearCoroutine = StartCoroutine(ClearResultAfterDelay(maxResultDisplayTime));
    }

    IEnumerator ClearResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        resultText.text = "";
        resultClearCoroutine = null;
    }

    void ClearResult()
    {
        if (resultClearCoroutine != null) { StopCoroutine(resultClearCoroutine); resultClearCoroutine = null; }
        resultText.text = "";
        sideChangeText.text = "";
    }

    void ShowSideChanged()
    {
        var gm = GameManager.Instance;
        sideChangeText.text = $"공수교대\n{gm.Inning}회 {(gm.IsTopInning ? "초" : "말")}";
    }

    void ShowGameOver()
    {
        var gm = GameManager.Instance;
        string winner = gm.HomeScore > gm.AwayScore ? "홈팀 승리"
                      : gm.AwayScore > gm.HomeScore ? "어웨이팀 승리"
                      : "무승부";
        resultText.text = $"경기 종료\n{winner}";
    }

    // ── UI 생성 (임시 레이아웃) ────────────────────────────
    void BuildUI()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        scoreText  = CreateText(canvas.transform, "ScoreText",  32, new Vector2(0.5f, 1f), new Vector2(0, -30),  new Vector2(500, 50));
        inningText = CreateText(canvas.transform, "InningText", 24, new Vector2(0.5f, 1f), new Vector2(0, -80),  new Vector2(500, 40));
        countText  = CreateText(canvas.transform, "CountText",  24, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(500, 40));

        resultText = CreateText(canvas.transform, "ResultText", 48, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 100));
        resultText.fontStyle = FontStyle.Bold;
        resultText.color = new Color(1f, 0.9f, 0.2f);

        sideChangeText = CreateText(canvas.transform, "SideChangeText", 56, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(900, 140));
        sideChangeText.fontStyle = FontStyle.Bold;
        sideChangeText.color = Color.white;

        var basesGO = new GameObject("Bases", typeof(RectTransform));
        basesGO.transform.SetParent(canvas.transform, false);
        var basesRT = basesGO.GetComponent<RectTransform>();
        basesRT.anchorMin = basesRT.anchorMax = new Vector2(1f, 0f);
        basesRT.pivot     = new Vector2(1f, 0f);
        basesRT.anchoredPosition = new Vector2(-40, 40);
        basesRT.sizeDelta = new Vector2(160, 160);

        secondBaseIcon = CreateBaseIcon(basesRT, "SecondBase", new Vector2(80, 140));
        firstBaseIcon  = CreateBaseIcon(basesRT, "FirstBase",  new Vector2(140, 70));
        thirdBaseIcon  = CreateBaseIcon(basesRT, "ThirdBase",  new Vector2(20, 70));
    }

    Text CreateText(Transform parent, string name, int size, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "";
        return text;
    }

    Image CreateBaseIcon(Transform parent, string name, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(36, 36);
        rt.localRotation = Quaternion.Euler(0, 0, 45f); // 다이아몬드 모양

        var img = go.GetComponent<Image>();
        img.color = EmptyColor;
        return img;
    }
}
