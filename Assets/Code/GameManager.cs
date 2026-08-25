using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public enum GameState   { Standby, Pitching, ShowResult, GameOver }
public enum PitchType   { Fastball, Curveball, Slider, Changeup }
public enum BatterResult{ None, Strike, Ball, Foul, Single, Double, Triple, HomeRun, Out }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── 설정 ────────────────────────────────────────────
    [Header("게임 설정")]
    [SerializeField] private int   maxInnings        = 9;
    [SerializeField] private float resultDisplayTime = 2.5f;   // 결과 표시 후 다음 투구까지 대기 시간 (투구 사이 텀)
    [SerializeField] private float sideChangeSwapDelay = 1f;   // 공수교대 시 마지막 스윙 애니메이션 끝날 때까지 팀 전환을 늦추는 시간
    [SerializeField] private float postRunnerDelay      = 1f;  // 주자들이 다 도착한 후 다음 투구까지 추가로 기다리는 시간

    [Header("투구 설정 (자동 — 투수 없이)")]
    [SerializeField] private float minPitchDuration    = 1.4f;
    [SerializeField] private float maxPitchDuration    = 2.6f;
    [SerializeField] private float pitchSpeedMultiplier = 1f; // 1보다 크면 공이 더 빠름(시간 단축), 작으면 더 느림
    [SerializeField] private float pitchLeadTime = 0.4f; // 투수 준비동작(와인드업)이 이만큼 먼저 재생된 뒤에 공이 실제로 날아가기 시작함
    [SerializeField, Range(0f, 1f)] private float strikeZoneRate = 0.65f;
    [SerializeField] private float ballOffsetRange = 0.6f; // 볼일 때 스트존 밖으로 벗어나는 정도 (미터) — Ball/판정 UI가 공용으로 씀

    [Header("타이밍 창 (비율 0~1)")]
    [SerializeField] private float perfectWindow = 0.13f;
    [SerializeField] private float goodWindow    = 0.28f;
    [SerializeField] private float reactionBias  = 0.05f; // 사람 반응속도 보정 — 눈으로 보고 누르면 원래 살짝 늦으므로, 그만큼 목표 타이밍을 뒤로 당겨줌

    [Header("참조 — 타자")]
    [SerializeField] private Batter  awayBatter; // 초 공격(선공) 타자 — 예: 자이언트
    [SerializeField] private Batter  homeBatter; // 말 공격(후공) 타자 — 예: 이글스

    [Header("참조 — 투수 (타격 안 하는 팀)")]
    [SerializeField] private Pitcher awayPitcher; // 어웨이 팀이 수비할 때(= 말) 던지는 투수
    [SerializeField] private Pitcher homePitcher; // 홈 팀이 수비할 때(= 초) 던지는 투수

    [Header("참조 — 수비 (타격 안 하는 팀, 투수+수비수 묶음 오브젝트)")]
    [SerializeField] private GameObject awayDefense; // 어웨이 팀이 수비할 때 켜짐 (= 말, 홈팀 타격 중)
    [SerializeField] private GameObject homeDefense; // 홈 팀이 수비할 때 켜짐 (= 초, 어웨이팀 타격 중)

    [Header("참조 — 기타")]
    [SerializeField] private BaseRunnerManager baseRunnerManager; // 비워두면 주자 이동 대기 없이 바로 다음 투구

    Batter  ActiveBatter  => IsTopInning ? awayBatter  : homeBatter;
    Pitcher ActivePitcher => IsTopInning ? homePitcher : awayPitcher; // 타격 안 하는 팀이 투수

    // ── 게임 상태 ────────────────────────────────────────
    public GameState State      { get; private set; } = GameState.Standby;
    public bool      IsTopInning{ get; private set; } = true;

    // ── 카운트 ───────────────────────────────────────────
    public int Balls     { get; private set; }
    public int Strikes   { get; private set; }
    public int Outs      { get; private set; }
    public int Inning    { get; private set; } = 1;
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }

    // ── 주자 ─────────────────────────────────────────────
    public bool RunnerOnFirst  { get; private set; }
    public bool RunnerOnSecond { get; private set; }
    public bool RunnerOnThird  { get; private set; }

    // ── 현재 투구 정보 (Batter에서 읽음) ────────────────
    public PitchType CurrentPitch  { get; private set; }
    public float     PitchDuration { get; private set; }
    public float     PitchTimer    { get; private set; }
    public bool      IsStrikePitch { get; private set; }
    public float     TimingTarget  { get; private set; }   // 최적 스윙 타이밍 비율 (0~1)
    public float     PitchOffsetX  { get; private set; }   // 스트존 중앙 기준 좌우 오프셋 (미터, 스트라이크면 0)
    public float     PitchOffsetY  { get; private set; }   // 스트존 중앙 기준 상하 오프셋 (미터, 스트라이크면 0)

    // ── 이벤트 ───────────────────────────────────────────
    public event Action               OnPitchStart;
    public event Action<BatterResult> OnResult;
    public event Action               OnCountChanged;
    public event Action               OnBasesChanged;
    public event Action<int, int>     OnRunnerMove; // fromBase(0=타자/홈, 1~3=베이스) → toBase(1~3=베이스, 4=득점)
    public event Action               OnSideChanged;
    public event Action               OnGameOver;

    private bool waitingForSwing;

    // ════════════════════════════════════════════════════
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        UpdateActiveTeams(); // 초기 상태: 초(선공) — 어웨이 타격, 홈 수비
        StartCoroutine(DelayThenPitch(1f));
    }

    void UpdateActiveTeams()
    {
        if (awayBatter   != null) awayBatter.gameObject.SetActive(IsTopInning);
        if (homeBatter   != null) homeBatter.gameObject.SetActive(!IsTopInning);
        if (homeDefense  != null) homeDefense.SetActive(IsTopInning);   // 초 = 어웨이 타격 → 홈 수비
        if (awayDefense  != null) awayDefense.SetActive(!IsTopInning);  // 말 = 홈 타격 → 어웨이 수비
        if (homePitcher  != null) homePitcher.gameObject.SetActive(IsTopInning);   // 홈 수비랑 같은 타이밍
        if (awayPitcher  != null) awayPitcher.gameObject.SetActive(!IsTopInning);  // 어웨이 수비랑 같은 타이밍
    }

    void Update()
    {
        if (State != GameState.Pitching || !waitingForSwing) return;

        PitchTimer += Time.deltaTime;
        if (PitchTimer >= PitchDuration)
        {
            waitingForSwing = false;
            // 스윙 안 했을 때 자동 판정
            ApplyResult(IsStrikePitch ? BatterResult.Strike : BatterResult.Ball);
        }
    }

    // ── 투구 시작 ────────────────────────────────────────
    void StartPitch()
    {
        if (State != GameState.Standby) return;

        CurrentPitch  = (PitchType)Random.Range(0, 4);
        IsStrikePitch = Random.value < strikeZoneRate;

        if (IsStrikePitch)
        {
            PitchOffsetX = 0f;
            PitchOffsetY = 0f;
        }
        else
        {
            PitchOffsetX = Random.Range(-ballOffsetRange, ballOffsetRange);
            PitchOffsetY = Random.Range(-ballOffsetRange, ballOffsetRange);
        }

        // 구종별 구속 배율
        float speedMult = CurrentPitch switch
        {
            PitchType.Fastball  => 0.80f,
            PitchType.Slider    => 0.95f,
            PitchType.Curveball => 1.10f,
            PitchType.Changeup  => 1.30f,
            _                   => 1.00f
        };

        PitchDuration   = Random.Range(minPitchDuration, maxPitchDuration) * speedMult / Mathf.Max(pitchSpeedMultiplier, 0.01f);
        TimingTarget    = Random.Range(0.65f, 0.80f);
        State           = GameState.Pitching; // 재진입 방지 — waitingForSwing은 와인드업 끝난 뒤에 켬

        ActiveBatter?.OnPitchBegin();
        ActivePitcher?.Pitch(PitchDuration); // 준비동작(와인드업) 먼저 재생 시작

        StartCoroutine(ReleaseAfterWindup());
    }

    // 투수 와인드업이 pitchLeadTime만큼 먼저 재생된 뒤에 실제로 공을 날린다 (타이머 시작 + OnPitchStart).
    IEnumerator ReleaseAfterWindup()
    {
        yield return new WaitForSeconds(pitchLeadTime);

        PitchTimer      = 0f;
        waitingForSwing = true;
        OnPitchStart?.Invoke();

        Debug.Log($"[GM] {CurrentPitch} | {(IsStrikePitch ? "스트라이크존" : "볼존")} | {PitchDuration:F2}s | target={TimingTarget:F2}");
    }

    // ── Batter 스윙 입력 처리 ────────────────────────────
    public BatterResult RegisterSwing()
    {
        if (State != GameState.Pitching || !waitingForSwing) return BatterResult.None;

        waitingForSwing = false;
        float t    = PitchTimer / PitchDuration;
        float diff = Mathf.Abs(t - (TimingTarget + reactionBias));

        BatterResult result = EvaluateSwing(diff);
        ApplyResult(result);
        return result;
    }

    BatterResult EvaluateSwing(float diff)
    {
        if (!IsStrikePitch)
            // 볼존 스윙: 운 좋으면 파울, 아니면 헛스윙
            return diff < goodWindow * 0.5f ? BatterResult.Foul : BatterResult.Strike;

        if (diff <= perfectWindow)      return RollHit(isPerfect: true);
        if (diff <= goodWindow)          return RollHit(isPerfect: false);
        if (diff <= goodWindow * 1.7f)   return BatterResult.Foul;
        return BatterResult.Strike;
    }

    BatterResult RollHit(bool isPerfect)
    {
        float r = Random.value;
        return isPerfect
            ? r < 0.12f ? BatterResult.HomeRun
            : r < 0.27f ? BatterResult.Triple
            : r < 0.52f ? BatterResult.Double
                        : BatterResult.Single
            : r < 0.04f ? BatterResult.HomeRun
            : r < 0.12f ? BatterResult.Triple
            : r < 0.30f ? BatterResult.Double
            : r < 0.58f ? BatterResult.Single
                        : BatterResult.Out;
    }

    // ── 결과 처리 ────────────────────────────────────────
    void ApplyResult(BatterResult result)
    {
        State = GameState.ShowResult;

        switch (result)
        {
            case BatterResult.Strike:
                Strikes++;
                if (Strikes >= 3) { Debug.Log("[GM] 삼진!"); Outs++; ResetCount(); }
                break;

            case BatterResult.Ball:
                Balls++;
                if (Balls >= 4) { Debug.Log("[GM] 볼넷!"); WalkAdvance(); ResetCount(); }
                break;

            case BatterResult.Foul:
                if (Strikes < 2) Strikes++;
                break;

            case BatterResult.Out:
                Outs++;
                ResetCount();
                break;

            case BatterResult.Single:
                HitAdvance(1);
                ResetCount();
                break;

            case BatterResult.Double:
                HitAdvance(2);
                ResetCount();
                break;

            case BatterResult.Triple:
                HitAdvance(3);
                ResetCount();
                break;

            case BatterResult.HomeRun:
                HitAdvance(4);
                ResetCount();
                break;
        }

        if (Outs >= 3) ChangeSide();

        string label = result switch
        {
            BatterResult.Strike   => "스트라이크",
            BatterResult.Ball     => "볼",
            BatterResult.Foul     => "파울",
            BatterResult.Out      => "아웃",
            BatterResult.Single   => "1루타!",
            BatterResult.Double   => "2루타!",
            BatterResult.Triple   => "3루타!",
            BatterResult.HomeRun  => "홈런!!",
            _                     => result.ToString()
        };
        Debug.Log($"[GM] {label} | {Balls}B {Strikes}S {Outs}O | {Inning}회{(IsTopInning ? "초" : "말")} | A:{AwayScore} H:{HomeScore}");

        OnResult?.Invoke(result);
        OnCountChanged?.Invoke();

        if (State != GameState.GameOver)
            StartCoroutine(DelayThenPitch(resultDisplayTime));
    }

    // ── 주자 진루 ────────────────────────────────────────
    // 안타/홈런: 타자와 기존 주자 모두 타구 종류만큼 베이스 진루 (야수 판단은 생략한 단순화 규칙)
    void HitAdvance(int bases)
    {
        bool r1 = RunnerOnFirst, r2 = RunnerOnSecond, r3 = RunnerOnThird;
        RunnerOnFirst = RunnerOnSecond = RunnerOnThird = false;

        int runs = 0;
        runs += AdvanceRunner(r3, 3, bases);
        runs += AdvanceRunner(r2, 2, bases);
        runs += AdvanceRunner(r1, 1, bases);
        runs += AdvanceRunner(true, 0, bases); // 타자

        AddRuns(runs);
        OnBasesChanged?.Invoke();
    }

    int AdvanceRunner(bool present, int fromBase, int bases)
    {
        if (!present) return 0;

        int newBase = fromBase + bases;
        if (newBase >= 4) { OnRunnerMove?.Invoke(fromBase, 4); return 1; } // 득점

        if (newBase == 3) RunnerOnThird  = true;
        else if (newBase == 2) RunnerOnSecond = true;
        else if (newBase == 1) RunnerOnFirst  = true;

        OnRunnerMove?.Invoke(fromBase, newBase);
        return 0;
    }

    // 볼넷: 강제 진루만 (뒤 베이스가 채워져 있을 때만 앞으로 밀림)
    void WalkAdvance()
    {
        bool r1 = RunnerOnFirst, r2 = RunnerOnSecond, r3 = RunnerOnThird;

        if (r1 && r2 && r3) { AddRuns(1); OnRunnerMove?.Invoke(3, 4); }
        if (r1 && r2) { RunnerOnThird  = true; OnRunnerMove?.Invoke(2, 3); }
        if (r1)       { RunnerOnSecond = true; OnRunnerMove?.Invoke(1, 2); }
        RunnerOnFirst = true;
        OnRunnerMove?.Invoke(0, 1); // 타자 1루로

        OnBasesChanged?.Invoke();
    }

    void AddRuns(int runs)
    {
        if (runs <= 0) return;
        if (IsTopInning) AwayScore += runs;
        else             HomeScore += runs;
    }

    void ClearBases()
    {
        RunnerOnFirst = RunnerOnSecond = RunnerOnThird = false;
        OnBasesChanged?.Invoke();
    }

    void ChangeSide()
    {
        Outs = 0;
        ClearBases();
        if (!IsTopInning)
        {
            Inning++;
            if (Inning > maxInnings) { EndGame(); return; }
        }
        IsTopInning = !IsTopInning;

        Debug.Log($"[GM] 체인지! → {Inning}회 {(IsTopInning ? "초" : "말")}");
        OnSideChanged?.Invoke(); // 배너는 즉시 뜸

        // 팀 전환(오브젝트 켜고 끄기)은 살짝 늦춰서, 방금 아웃된 타자의 스윙 애니메이션이 끊기지 않게 함
        StartCoroutine(SwapTeamsAfterDelay(sideChangeSwapDelay));
    }

    IEnumerator SwapTeamsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UpdateActiveTeams();
        ActiveBatter?.OnPitchBegin(); // 새로 나온 타자를 홈 위치/대기 자세로 즉시 정리
    }

    void EndGame()
    {
        State = GameState.GameOver;
        string winner = HomeScore > AwayScore ? "홈팀 승리"
                      : AwayScore > HomeScore ? "어웨이팀 승리"
                      : "무승부";
        Debug.Log($"[GM] 경기 종료! {winner} | A:{AwayScore} H:{HomeScore}");
        OnGameOver?.Invoke();
    }

    void ResetCount() { Balls = 0; Strikes = 0; }

    IEnumerator DelayThenPitch(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (State == GameState.GameOver) yield break;

        // 주자가 아직 베이스 사이를 이동 중이면 다 도착할 때까지 대기
        while (baseRunnerManager != null && baseRunnerManager.IsAnyRunnerMoving)
            yield return null;

        // 다 도착한 후 잠깐 더 대기했다가 다음 투구 시작
        yield return new WaitForSeconds(postRunnerDelay);
        if (State == GameState.GameOver) yield break;

        State = GameState.Standby;
        StartPitch();
    }
}
