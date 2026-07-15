using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

// 블루투스(SPP)로 페어링된 아두이노는 Windows에서 가상 COM 포트로 보이므로
// 일반 SerialPort로 그대로 읽을 수 있다.
// 아두이노 쪽 배트 스윙 센서(가속도/충격)는 감지 시 한 줄로
//   SWING\n            (세기 값 없이)
//   SWING:0.83\n       (0~1 세기 값 포함)
// 을 전송하도록 구성한다.
public class ArduinoSwingInput : MonoBehaviour
{
    [Header("블루투스(SPP) 연결 설정")]
    [SerializeField] private string portName = "COM5"; // 페어링된 가상 COM 포트 이름
    [SerializeField] private int    baudRate = 9600;
    [SerializeField] private bool   autoConnectOnStart = true;

    [Header("스윙 감지 설정")]
    [SerializeField] private string swingCommand      = "SWING";
    [SerializeField] private float  minSwingInterval  = 0.2f; // 노이즈로 인한 중복 감지 방지

    public static event Action<float> OnSwingDetected; // intensity(0~1 등, 값 없으면 1)

    public bool IsConnected => serialPort != null && serialPort.IsOpen;

    private SerialPort serialPort;
    private Thread readThread;
    private volatile bool keepReading;
    private readonly ConcurrentQueue<string> messageQueue = new();
    private float lastSwingTime = -999f;

    void Start()
    {
        if (autoConnectOnStart) Connect();
    }

    public void Connect()
    {
        if (IsConnected) return;

        try
        {
            serialPort = new SerialPort(portName, baudRate) { ReadTimeout = 500, NewLine = "\n" };
            serialPort.Open();

            keepReading = true;
            readThread = new Thread(ReadLoop) { IsBackground = true };
            readThread.Start();

            Debug.Log($"[ArduinoSwingInput] {portName} 연결됨");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ArduinoSwingInput] {portName} 연결 실패: {e.Message}");
        }
    }

    public void Disconnect()
    {
        keepReading = false;
        readThread?.Join(200);
        if (serialPort != null && serialPort.IsOpen) serialPort.Close();
        serialPort = null;
    }

    // 백그라운드 스레드 — 시리얼 포트 블로킹 읽기
    void ReadLoop()
    {
        while (keepReading && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string line = serialPort.ReadLine();
                if (!string.IsNullOrEmpty(line)) messageQueue.Enqueue(line.Trim());
            }
            catch (TimeoutException) { /* 다음 폴링에서 재시도 */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[ArduinoSwingInput] 읽기 오류: {e.Message}");
            }
        }
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out string msg))
            HandleMessage(msg);
    }

    void HandleMessage(string msg)
    {
        if (!msg.StartsWith(swingCommand, StringComparison.OrdinalIgnoreCase)) return;
        if (Time.time - lastSwingTime < minSwingInterval) return;
        lastSwingTime = Time.time;

        float intensity = 1f;
        int idx = msg.IndexOf(':');
        if (idx >= 0 && float.TryParse(msg[(idx + 1)..], out float parsed)) intensity = parsed;

        OnSwingDetected?.Invoke(intensity);
    }

    void OnDestroy() => Disconnect();
    void OnApplicationQuit() => Disconnect();
}
