using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// FeederDirector: Điều phối danh sách các feeder (8 máy).
// - Mỗi 5 giây gọi ngẫu nhiên 1 feeder.FireOnce().
// - Có thể auto-collect feeder trong 1 parent, hoặc kéo thả thủ công.
// - Có tùy chọn tránh bắn 2 lần liên tiếp cùng 1 máy.

public class FeederDirector : MonoBehaviour
{
    [Header("Collect Feeders")]
    [Tooltip("Nếu bật, Director sẽ tự tìm feeder dưới parentContainer. Nếu tắt, dùng danh sách 'feeders' phía dưới.")]
    public bool autoCollectFromParent = true;
    public Transform parentContainer;
    public List<SimpleOpponentFeeder> feeders = new List<SimpleOpponentFeeder>();

    [Header("Schedule")]
    [Tooltip("Cooldown toàn cục giữa 2 lần bắn (giây).")]
    public float cooldownSeconds = 5f;
    [Tooltip("Tránh bắn 2 lần liên tiếp cùng 1 máy.")]
    public bool avoidImmediateRepeat = true;
    [Tooltip("Tự chạy ngay khi Play.")]
    public bool startOnPlay = true;

    [Header("Manual Controls")]
    [Tooltip("Phím bắn thử ngay (bỏ qua timer), chọn feeder ngẫu nhiên.")]
    public KeyCode manualFireKey = KeyCode.R;

    [Header("Debug")]
    public bool logDirector = true;

    private Coroutine loop;
    private int lastIndex = -1;

    void Awake()
    {
        if (autoCollectFromParent)
        {
            CollectFromParent();
        }
    }

    void Start()
    {
        if (startOnPlay) StartSchedule();
    }

    void Update()
    {
        if (manualFireKey != KeyCode.None && Input.GetKeyDown(manualFireKey))
        {
            FireRandomOnce();
        }
    }

    public void StartSchedule()
    {
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(FireLoop());
        if (logDirector) Debug.Log("[FeederDirector] Schedule started.");
    }

    public void StopSchedule()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
        if (logDirector) Debug.Log("[FeederDirector] Schedule stopped.");
    }

    IEnumerator FireLoop()
    {
        var wait = new WaitForSeconds(cooldownSeconds);
        while (true)
        {
            FireRandomOnce();
            yield return wait;
        }
    }

    void FireRandomOnce()
    {
        var pool = GetReadyFeeders();
        if (pool.Count == 0)
        {
            if (logDirector) Debug.LogWarning("[FeederDirector] No ready feeders to fire.");
            return;
        }

        int idx = PickRandomIndex(pool.Count);
        var feeder = pool[idx];
        feeder.FireOnce();

        // Cập nhật lastIndex tương ứng với index trong danh sách "feeders" gốc (nếu có)
        lastIndex = feeders.IndexOf(feeder);
        if (logDirector)
        {
            Debug.Log($"[FeederDirector] Fired feeder: {(feeder != null ? feeder.name : "null")}");
        }
    }

    int PickRandomIndex(int count)
    {
        if (!avoidImmediateRepeat || count <= 1 || lastIndex < 0)
        {
            return Random.Range(0, count);
        }

        // Tránh trùng với lastIndex (map theo pool)
        // Vì pool là 'ready feeders', cần ánh xạ lastIndex (global) sang index trong pool, hoặc chọn lại nếu trùng.
        // Đơn giản: chọn ngẫu nhiên, nếu feeder cùng với 'lastIndex' global, bốc lại (tối đa 10 lần).
        for (int i = 0; i < 10; i++)
        {
            int r = Random.Range(0, count);
            var candidate = GetReadyFeeders()[r];
            if (feeders.IndexOf(candidate) != lastIndex)
                return r;
        }
        // Nếu vẫn trùng, chấp nhận
        return Random.Range(0, count);
    }

    List<SimpleOpponentFeeder> GetReadyFeeders()
    {
        var list = new List<SimpleOpponentFeeder>();
        foreach (var f in feeders)
        {
            if (f != null && f.IsReady())
                list.Add(f);
        }
        return list;
    }

    [ContextMenu("Collect From Parent")]
    public void CollectFromParent()
    {
        feeders.Clear();
        if (parentContainer == null)
        {
            // Nếu không set parent, tìm toàn scene (ít khuyến nghị cho performance)

            var found = FindObjectsByType<SimpleOpponentFeeder>(FindObjectsSortMode.None);
            feeders.AddRange(found);
        }
        else
        {
            var found = parentContainer.GetComponentsInChildren<SimpleOpponentFeeder>(includeInactive: false);
            feeders.AddRange(found);
        }
        if (logDirector) Debug.Log($"[FeederDirector] Collected feeders: {feeders.Count}");
    }
}