using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// GameManager.OnRunnerMove 이벤트를 받아서 실제 주자 오브젝트를 베이스 사이로 이동시킨다.
// 애니메이션(달리는 모션)은 runnerPrefab 쪽에서 직접 설정 — 여기선 위치 이동/베이스 추적만 담당.
public class BaseRunnerManager : MonoBehaviour
{
    [Header("베이스 위치")]
    [SerializeField] private Transform home;
    [SerializeField] private Transform first;
    [SerializeField] private Transform second;
    [SerializeField] private Transform third;

    [Header("주자 스폰 위치 (비워두면 Home 위치 그대로 씀)")]
    [SerializeField] private Transform spawnPoint; // 타자가 안타 쳐서 새 주자가 나타나는 지점

    [Header("주자 프리팹 (팀별 — 애니메이션은 직접 붙이기)")]
    [SerializeField] private GameObject awayRunnerPrefab; // 어웨이 팀 타격 중 생기는 주자 — 예: 자이언트
    [SerializeField] private GameObject homeRunnerPrefab; // 홈 팀 타격 중 생기는 주자 — 예: 이글스

    GameObject CurrentTeamRunnerPrefab => GameManager.Instance.IsTopInning ? awayRunnerPrefab : homeRunnerPrefab;

    [Header("이동 속도")]
    [SerializeField] private float runSpeed = 6f;

    private readonly GameObject[] runnersOnBase = new GameObject[4]; // 인덱스 1~3만 사용
    private readonly Dictionary<GameObject, Coroutine> activeRuns = new(); // 주자별 진행 중인 이동 코루틴 추적

    // 지금 베이스 사이를 이동 중인 주자가 하나라도 있는지 (GameManager가 다음 투구 시작 전에 확인)
    public bool IsAnyRunnerMoving => activeRuns.Count > 0;

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogWarning("[BaseRunnerManager] GameManager.Instance 없음"); return; }

        gm.OnRunnerMove   += HandleRunnerMove;
        gm.OnSideChanged  += ClearAllRunners;
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnRunnerMove  -= HandleRunnerMove;
        gm.OnSideChanged -= ClearAllRunners;
    }

    Transform BasePos(int b) => b switch
    {
        0 => home,
        1 => first,
        2 => second,
        3 => third,
        _ => home // 4(득점) 방향으로 뛸 때 목표
    };

    void HandleRunnerMove(int fromBase, int toBase)
    {
        var prefab = CurrentTeamRunnerPrefab;
        if (prefab == null || home == null) return;

        GameObject runner;
        if (fromBase == 0)
        {
            // 타자가 새 주자가 되어 출발 — Spawn Point 있으면 그 자리에서, 없으면 Home 위치
            Transform spawn = spawnPoint != null ? spawnPoint : home;
            runner = Instantiate(prefab, spawn.position, spawn.rotation);
        }
        else
        {
            runner = runnersOnBase[fromBase];
            runnersOnBase[fromBase] = null;
            if (runner == null) runner = Instantiate(prefab, BasePos(fromBase).position, BasePos(fromBase).rotation);
        }

        if (toBase < 4) runnersOnBase[toBase] = runner;

        // 이 주자가 이미 이전 이동 코루틴을 진행 중이면 먼저 멈추고 새로 시작 (안 그러면 두 코루틴이 동시에 위치를 다투게 됨)
        if (activeRuns.TryGetValue(runner, out Coroutine existing) && existing != null)
            StopCoroutine(existing);

        activeRuns[runner] = StartCoroutine(RunPath(runner, fromBase, toBase));
    }

    // 목표 베이스까지 직선으로 가로지르지 않고, 중간 베이스를 순서대로 다 거쳐서 감
    // (예: 1루 주자가 3루타로 홈까지 갈 때 2루 → 3루 → 홈 순서로 이동)
    // fromBase부터 포함하는 이유: 이전 이동이 중간에 끊긴 경우 아직 fromBase에 도착 전일 수 있어서,
    // 거길 먼저 마저 찍고 넘어가야 대각선으로 질러가는 경로 이탈이 안 생김 (정상 도착 상태면 그냥 0초 통과됨)
    IEnumerator RunPath(GameObject runner, int fromBase, int toBase)
    {
        // 새로 스폰된 타자 주자(fromBase=0)는 홈을 거칠 필요 없이 바로 1루부터 시작
        int startBase = fromBase == 0 ? 1 : fromBase;

        for (int b = startBase; b <= toBase; b++)
        {
            yield return RunTo(runner, BasePos(b));
        }

        activeRuns.Remove(runner); // 이동 종료 — "진행 중" 목록에서 제거

        if (toBase >= 4 && runner != null) Destroy(runner);
    }

    IEnumerator RunTo(GameObject runner, Transform target)
    {
        while (runner != null && target != null &&
               Vector3.Distance(runner.transform.position, target.position) > 0.15f)
        {
            Vector3 dir = target.position - runner.transform.position;
            runner.transform.position = Vector3.MoveTowards(runner.transform.position, target.position, runSpeed * Time.deltaTime);
            if (dir.sqrMagnitude > 0.0001f) runner.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            yield return null;
        }
    }

    void ClearAllRunners()
    {
        for (int i = 1; i <= 3; i++)
        {
            if (runnersOnBase[i] != null)
            {
                if (activeRuns.TryGetValue(runnersOnBase[i], out Coroutine co) && co != null) StopCoroutine(co);
                activeRuns.Remove(runnersOnBase[i]);
                Destroy(runnersOnBase[i]);
            }
            runnersOnBase[i] = null;
        }
    }
}
