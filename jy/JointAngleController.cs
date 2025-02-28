using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using ChartAndGraph;

public class JointAngleController : MonoBehaviour
{
    public GraphChart chart;

    [Header("Dropdown")]
    public TMP_Dropdown jointDropdown; // 조인트 목록
    public TMP_Dropdown axisDropdown; // 축 목록

    [Header("Buttons")]
    public UnityEngine.UI.Button increaseButton; // +0.1 버튼
    public UnityEngine.UI.Button decreaseButton; // -0.1 버튼
    public UnityEngine.UI.Button shiftLeftButton; // Shift Left
    public UnityEngine.UI.Button shiftRightButton; // Shift Right
    public UnityEngine.UI.Button saveCSVButton; // Save CSV

    [Header("File Path")]
    public string RootFilePath;
    public string FileName;

    [Header("Edit Value Settings")]
    public int startFrame;
    public int endFrame;
    public int midFrame;
    public float midFrameIncrement = 1.0f;

    [Header("Body Joint Names")]
    private static List<List<Vector3>> jointPositions; // 각 관절의 위치 데이터를 저장. 여러 그래프가 공유하는 데이터
    private static List<float> times; // 각 프레임의 시간을 저장
    private static Dictionary<string, int> jointNameToIndex; // 조인트 이름 -> 인덱스 매핑
    public string selectedJoint; // 선택된 조인트 이름. 각 그래프 개별 선택 가능
    string[] _bodyJointNames = new string[]
    {
        "pelvis", "left_hip", "right_hip", "spine1", "left_knee",
        "right_knee", "spine2", "left_ankle", "right_ankle", "spine3",
        "left_foot", "right_foot", "neck", "left_collar", "right_collar",
        "head", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
        "left_wrist", "right_wrist"
    };

    // static 이벤트로 데이터 변경 시 모든 인스턴스가 업데이트되도록 함
    public static event System.Action OnJointDataChanged;

    public List<List<Vector3>> GetJointPositions()
    {
        return jointPositions;
    }

    void Start()
    {
        // 0) 시간 리스트, SMPL 조인트 데이터 구조 초기화
        if (times == null) { times = new List<float>(); }
        if (jointPositions == null) { jointPositions = new List<List<Vector3>>(); }
        if (jointNameToIndex == null)
        {
            jointNameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < _bodyJointNames.Length; i++)
            {
                jointNameToIndex[_bodyJointNames[i]] = i;
            }
        }

        // 1) CSV 파일 로드 -> jointPositions에 프레임별 (x,y,z) 좌표들 저장
        string fullPath = Path.Combine(RootFilePath, FileName);
        if(jointPositions.Count == 0) LoadCSV(fullPath);

        // 2) Dropdown 초기화
        InitializeDropdown(); // 조인트 목록
        InitializeAxisDropdown(); // 축 목록

        // 3) 기본 선택 조인트 설정 (인덱스 0: "pelvis")
        selectedJoint = _bodyJointNames[0];

        // 4) 버튼 이벤트 연결
        increaseButton.onClick.AddListener(() => OnValueChangeButtonClicked(midFrameIncrement));
        decreaseButton.onClick.AddListener(() => OnValueChangeButtonClicked(-midFrameIncrement));
        shiftLeftButton.onClick.AddListener(() => OnFrameShiftButtonClicked(-1));
        shiftRightButton.onClick.AddListener(() => OnFrameShiftButtonClicked(1));
        saveCSVButton.onClick.AddListener(OnSaveCSVButtonClicked);
        
        OnJointDataChanged += HandleJointDataChanged; // 각 인스턴스가 데이터 변경 이벤트에 구독하여 자신의 차트를 업데이트
    }

    void OnDestroy()
    {
        OnJointDataChanged -= HandleJointDataChanged;
    }

    void Update()
    {
        // startFrame ~ endFrame 범위에 대한 마커 표시
        PlotVerticalMarker(startFrame, "StartMarker");
        PlotVerticalMarker(endFrame, "EndMarker");
    }

    // 데이터 변경시 호출. 자신의 차트를 갱신
    void HandleJointDataChanged()
    {
        PlotJoint(selectedJoint);
    }

    /// <summary>
    /// CSV Load
    /// </summary>
    void LoadCSV(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogWarning("CSV file path not set!");
            return;
        }
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("CSV file does not exist: " + filePath);
            return;
        }

        string[] lines = File.ReadAllLines(filePath);
        string[] headers = lines[0].Split(','); // 첫 번째 줄은 헤더

        // 헤더에서 조인트 이름 추출
        for (int i = 2; i < headers.Length; i += 3)
        {
            string jointName = headers[i].Replace("_wx", "");
            jointNameToIndex[jointName] = (i - 2) / 3;
        }

        // 데이터 읽기
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            times.Add(float.Parse(values[1]));
            List<Vector3> framePositions = new List<Vector3>();

            for (int j = 2; j < values.Length; j += 3)
            {
                float x = float.Parse(values[j]);
                float y = float.Parse(values[j + 1]);
                float z = float.Parse(values[j + 2]);
                framePositions.Add(new Vector3(x, y, z));
            }
            jointPositions.Add(framePositions);
        }
        Debug.Log($"CSV Load 완료. 총 프레임 수: {jointPositions.Count}");
    }

    /// <summary>
    /// Dropdown (조인트 목록 추가)
    /// </summary>
    void InitializeDropdown()
    {
        if (jointDropdown == null)
        {
            Debug.LogWarning("TMP_Dropdown is not assigned in the Inspector!");
            return;
        }
        jointDropdown.ClearOptions(); // 기존 옵션 초기화
        List<string> options = new List<string>(_bodyJointNames); // bodyJointNames -> 리스트 변환
        jointDropdown.AddOptions(options); // 드롭다운에 옵션 추가
        jointDropdown.onValueChanged.AddListener(OnJointSelected); // 이벤트 연결
        jointDropdown.value = 0; // 초기값 설정(인덱스 0)
        OnJointSelected(0);
    }
    public void OnJointSelected(int index)
    {
        selectedJoint = _bodyJointNames[index];
        Debug.Log($"[JointAngleController] 조인트 선택: {selectedJoint}");

        if (jointPositions.Count > 0)
        {
            int jointIndex = jointNameToIndex[selectedJoint];
            Vector3 firstFrameValue = jointPositions[0][jointIndex];
            for (int frame = startFrame; frame <= endFrame && frame < jointPositions.Count; frame++)
            {
                Vector3 frameValue = jointPositions[frame][jointIndex];
            }
        }
        else
        {
            Debug.LogWarning("jointPositions is empty. CSV not loaded?");
        }
        PlotJoint(selectedJoint);
    }

    /// <summary>
    /// Dropdown (Axis Angle 목록 추가)
    /// </summary>
    public void InitializeAxisDropdown()
    {
        if (axisDropdown == null)
        {
            Debug.LogWarning("Axis TMP_Dropdown is not assigned in the Inspector!");
            return;
        }
        axisDropdown.ClearOptions(); // 기존 옵션 초기화
        List<string> options = new List<string> { "X", "Y", "Z" }; // 축 옵션 추가
        axisDropdown.AddOptions(options);
        axisDropdown.value = 1; // 초기값 설정(인덱스 1: "Y")
    }

    /// <summary>
    /// Button (y value increase/decrease)
    /// </summary>
    private void OnValueChangeButtonClicked(float increment)
    {
        if (string.IsNullOrEmpty(selectedJoint)) return;

        int jointIndex = jointNameToIndex[selectedJoint];
        int selectedAxis = axisDropdown.value; // 0: X, 1: Y, 2: Z
        int range = endFrame - startFrame;

        // 선택된 범위 내의 모든 프레임 Y값을 increment만큼 변경
        for (int frame = startFrame; frame <= endFrame && frame < jointPositions.Count; frame++)
        {
            Vector3 currentPos = jointPositions[frame][jointIndex];
            float weight = Mathf.Max(1.0f - Mathf.Abs((float)(frame - midFrame) / (range / 2f)), 0.0f);
            float weightedIncrement = increment * weight;

            switch (selectedAxis)
            {
                case 0: // X
                    jointPositions[frame][jointIndex] = new Vector3(currentPos.x + weightedIncrement, currentPos.y, currentPos.z);
                    break;
                case 1: // Y
                    jointPositions[frame][jointIndex] = new Vector3(currentPos.x, currentPos.y + weightedIncrement, currentPos.z);
                    break;
                case 2: // Z
                    jointPositions[frame][jointIndex] = new Vector3(currentPos.x, currentPos.y, currentPos.z + weightedIncrement);
                    break;
            }
        }
        // 단일 그래프만 갱신하는 대신, 전체 그래프 업데이트 이벤트를 발생시키도록 함
        BroadcastJointDataChanged();
        Debug.Log($"Applied {increment:F1} increment to {selectedJoint} from frame {startFrame} to {endFrame} with weighted adjustment.");
    }

    /// <summary>
    /// Button (X value shift)
    /// </summary>
    private void OnFrameShiftButtonClicked(int shiftAmount)
    {
        int range = endFrame - startFrame + 1; // 현재 프레임 범위 (startFrame ~ endFrame)
        int totalFrames = jointPositions.Count; // 전체 프레임 수
        int newRange; // 새로운 프레임 범위

        // Decrease (Scaling down)
        if (shiftAmount == -1)
        {
            newRange = Mathf.Max(1, range / 2); // 새로운 프레임 범위 = 현재 프레임 범위의 절반 (최소값은 1)

            // 1) startFrame ~ endFrame scaling down
            for (int i = 0; i < newRange; i++)
            {
                jointPositions[startFrame + i] = jointPositions[startFrame + (i * 2)];
            }

            // 2) endFrame ~ totalFrame translate left
            jointPositions.RemoveRange(startFrame + newRange, range - newRange);

            // 3) Update the frame range (startFrame ~ endFrame)
            endFrame = startFrame + newRange;

            // 4) Update the graph
            BroadcastJointDataChanged();
            Debug.Log($"Scaled down frames from {startFrame} to {endFrame}. New range: {startFrame} to {endFrame}");
        }

        // Increase (Scaling up)
        else if (shiftAmount == 1)
        {
            newRange = Mathf.Min(totalFrames - startFrame, range * 2); // 새로운 프레임 범위는 현재 프레임 범위의 2배
            List<List<Vector3>> interpolatedFrames = new List<List<Vector3>>();
            List<List<Vector3>> preservedFrames = new List<List<Vector3>>();

            // 1) endFrame ~ totalFrame 보존
            for (int i = endFrame + 1; i < totalFrames; i++)
            {
                preservedFrames.Add(jointPositions[i]);
            }

            // 2) startFrame ~ endFrame scaling up -> Linear interpolation
            for (int i = 0; i < range; i++)
            {
                interpolatedFrames.Add(jointPositions[startFrame + i]);
                if (i < range - 1)
                {
                    List<Vector3> interpolatedFrame = new List<Vector3>();
                    for (int j = 0; j < jointPositions[startFrame + i].Count; j++)
                    {
                        Vector3 start = jointPositions[startFrame + i][j];
                        Vector3 end = jointPositions[startFrame + i + 1][j];
                        Vector3 mid = Vector3.Slerp(start, end, 0.5f); // Linear interpolation Coeff = 0.5
                        interpolatedFrame.Add(mid);
                    }
                    interpolatedFrames.Add(interpolatedFrame);
                }
            }

            // 3) Update the frame range (startFrame ~ endFrame)
            for (int i = 0; i < interpolatedFrames.Count; i++)
            {
                if (startFrame + i < jointPositions.Count)
                {
                    jointPositions[startFrame + i] = interpolatedFrames[i];
                }
                else
                {
                    jointPositions.Add(interpolatedFrames[i]);
                }
            }
            endFrame = startFrame + newRange;

            // 4) 보존해놓은 endFrame ~ totalFrame을 덮어쓰기
            for (int i = 0; i < preservedFrames.Count; i++)
            {
                if (startFrame + interpolatedFrames.Count + i < jointPositions.Count)
                {
                    jointPositions[startFrame + interpolatedFrames.Count + i] = preservedFrames[i];
                }
                else
                {
                    jointPositions.Add(preservedFrames[i]);
                }
            }

            // 5) Update the graph
            BroadcastJointDataChanged();
            Debug.Log($"Scaled up frames from {startFrame} to {endFrame}. New range: {startFrame} to {endFrame}");
        }

        // Regularize the frame range
        startFrame = Mathf.Max(0, startFrame);
        endFrame = Mathf.Min(totalFrames - 1, endFrame);
    }

    // 모든 인스턴스에 데이터 변경됨을 알림
    private void BroadcastJointDataChanged()
    {
        OnJointDataChanged?.Invoke();
    }

    /// <summary>
    /// Plot the selected joint on the graph
    /// </summary>
    public void PlotJoint(string jointName)
    {
        if (chart == null)
        {
            Debug.LogWarning("Chart reference not set!");
            return;
        }

        // 1) jointName -> jointIndex
        int jIndex = jointNameToIndex[jointName];

        // 2) 카테고리 준비
        chart.DataSource.StartBatch();
            chart.DataSource.ClearCategory("X Angle");
            chart.DataSource.ClearCategory("Y Angle");
            chart.DataSource.ClearCategory("Z Angle");
        
            // 3) 프레임 반복하며 (x축=프레임, y축=pos.y) 형태로 Plot
            for (int frame = 0; frame < jointPositions.Count; frame++)
            {
                Vector3 pos = jointPositions[frame][jIndex];
                chart.DataSource.AddPointToCategory("X Angle", frame, pos.x);
                chart.DataSource.AddPointToCategory("Y Angle", frame, pos.y);
                chart.DataSource.AddPointToCategory("Z Angle", frame, pos.z);
            }
        chart.DataSource.EndBatch();
    }   

    /// <summary>
    /// 현재 편집중인 프레임 범위에 마커 표시
    /// </summary>
    private void PlotVerticalMarker(int markerFrame, string markerCategory)
    {
        if (chart == null) return;
        chart.DataSource.StartBatch();
        chart.DataSource.ClearCategory(markerCategory);
        chart.DataSource.AddPointToCategory(markerCategory, markerFrame, -10);
        chart.DataSource.AddPointToCategory(markerCategory, markerFrame, 10);
        chart.DataSource.EndBatch();
    }

    /// <summary>
    /// Save Button
    /// </summary>
    private void OnSaveCSVButtonClicked()
    {
        string savePath = Path.Combine(RootFilePath, "Edited_" + FileName);
        SaveCSV(savePath);
    }
    private void SaveCSV(string filePath)
    {
        List<string> lines = new List<string>();
        string header = "Frame,Time";
        foreach (var jointName in _bodyJointNames)
        {
            header += $",{jointName}_wx,{jointName}_wy,{jointName}_wz";
        }
        lines.Add(header);
        while (times.Count < jointPositions.Count)
        {
            times.Add(times.Count > 0 ? times[times.Count - 1] + (times[1] - times[0]) : 0f);
        }
        for (int i = 0; i < jointPositions.Count; i++)
        {
            string line = $"{i},{times[i]}";
            foreach (var pos in jointPositions[i])
            {
                line += $",{pos.x},{pos.y},{pos.z}";
            }
            lines.Add(line);
        }
        File.WriteAllLines(filePath, lines);
        Debug.Log($"CSV saved to {filePath}");
    }
}