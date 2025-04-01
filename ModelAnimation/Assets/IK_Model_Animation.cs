using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class IK_Model_Animation : MonoBehaviour
{
    // --- Constants ---
    private const int JointCount = 17;          // Number of joints from the pose estimation
    private const int BoneCount = 12;           // Number of bones used for normalization
    private const int PoseDataSize = JointCount * 3; // Each joint has 3 values (x, y, z)

    [SerializeField, Range(10, 120)]
    private float frameRate = 30f;              // Target frame rate for updating the model

    // --- References to body part transforms ---
    public List<Transform> boneList = new List<Transform>();
    private GameObject fullBodyIK;              // Root object of the full body IK system

    // --- Pose data storage ---
    private Vector3[] points = new Vector3[JointCount];         // World-space joint positions
    private Vector3[] normalizedBones = new Vector3[BoneCount]; // Unit vectors for bone direction
    private float[] boneDistances = new float[BoneCount];       // Initial bone lengths for scaling

    // --- Defines bones and joints used for IK and normalization ---
    private static readonly int[,] BoneJoint = {
        { 0, 2 }, { 2, 3 }, { 0, 5 }, { 5, 6 }, { 0, 9 },
        { 9, 10 }, { 9, 11 }, { 11, 12 }, { 12, 13 },
        { 9, 14 }, { 14, 15 }, { 15, 16 }
    };

    private static readonly int[,] NormalizeJoint = {
        { 0, 1 }, { 1, 2 }, { 0, 3 }, { 3, 4 }, { 0, 5 },
        { 5, 6 }, { 5, 7 }, { 7, 8 }, { 8, 9 },
        { 5, 10 }, { 10, 11 }, { 11, 12 }
    };

    // --- UDP Networking ---
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private bool dataReceived = false;
    private float timer = 0f;
    private float[] poseData = new float[PoseDataSize]; // Raw data from UDP

    // --- Unity Lifecycle Methods ---
    void Start()
    {
        InitializePoints();    // Set all joint positions to (0,0,0)
        StartUDPListener();    // Begin listening for pose data from Python
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 1f / frameRate)
        {
            timer = 0f;
            if (dataReceived)
            {
                UpdatePoints(); // Parse and update joint positions if new data was received
            }
        }

        if (fullBodyIK == null)
        {
            FindIKComponents(); // Find and store references to bone transforms and lengths
        }
        else
        {
            ApplyIK();          // Apply updated joint positions to the model using IK
        }
    }

    // --- UDP Methods ---

    // Start listening on port 5000 for incoming pose data
    private void StartUDPListener()
    {
        udpClient = new UdpClient(5000);
        remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 5000);
        udpClient.BeginReceive(ReceiveUDPData, null);
    }

    // Callback function when UDP data is received
    private void ReceiveUDPData(IAsyncResult result)
    {
        byte[] receivedBytes = udpClient.EndReceive(result, ref remoteEndPoint);
        string receivedData = Encoding.UTF8.GetString(receivedBytes);
        string[] values = receivedData.Split(',');

        if (values.Length == PoseDataSize)
        {
            for (int i = 0; i < PoseDataSize; i++)
            {
                if (!float.TryParse(values[i], out poseData[i]))
                {
                    Debug.LogWarning($"Failed to parse pose data at index {i}");
                    return;
                }
            }
            dataReceived = true;
        }

        // Continue listening for more data
        udpClient.BeginReceive(ReceiveUDPData, null);
    }

    // --- Pose Data Processing ---

    // Initialize all joint positions to zero
    private void InitializePoints()
    {
        for (int i = 0; i < JointCount; i++)
        {
            points[i] = Vector3.zero;
        }
    }

    // Convert raw pose data into 3D points and normalized bones
    private void UpdatePoints()
    {
        for (int i = 0; i < JointCount; i++)
        {
            // Flip Y-axis to match Unity coordinate system
            points[i] = new Vector3(poseData[i * 3], -poseData[i * 3 + 1], poseData[i * 3 + 2]);
            Debug.Log($"Joint {i}: X={points[i].x}, Y={points[i].y}, Z={points[i].z}");
        }

        // Calculate normalized bone direction vectors
        for (int i = 0; i < BoneCount; i++)
        {
            normalizedBones[i] = (points[BoneJoint[i, 1]] - points[BoneJoint[i, 0]]).normalized;
        }
    }

    // --- Initialization of IK Model ---

    // Find the FullBodyIK GameObject and all bone transforms in the scene
    private void FindIKComponents()
    {
        fullBodyIK = GameObject.Find("FullBodyIK");
        if (fullBodyIK == null) return;

        boneList.Clear();

        foreach (BoneRef bone in Enum.GetValues(typeof(BoneRef)))
        {
            Transform obj = GameObject.Find(bone.ToString())?.transform;
            if (obj != null)
            {
                boneList.Add(obj);
            }
        }

        // Store original bone distances for scale-preserving movement
        for (int i = 0; i < BoneCount; i++)
        {
            boneDistances[i] = Vector3.Distance(
                boneList[NormalizeJoint[i, 0]].position,
                boneList[NormalizeJoint[i, 1]].position
            );
        }
    }

    // --- IK Application ---

    // Apply pose estimation data to move and rotate the IK model
    private void ApplyIK()
    {
        // Make sure pose data is valid (not zeroed)
        if (Mathf.Abs(points[0].x) < 1000 && Mathf.Abs(points[0].y) < 1000 && Mathf.Abs(points[0].z) < 1000)
        {
            // Move hips and root object to match position of joint 0 (hips)
            Vector3 targetPosition = points[0] * 0.001f + Vector3.up * 0.8f;
            boneList[0].position = Vector3.Lerp(boneList[0].position, targetPosition, 0.1f);
            fullBodyIK.transform.position = Vector3.Lerp(fullBodyIK.transform.position, points[0] * 0.001f, 0.01f);

            // Orient the full body based on hip and torso vectors
            Vector3 hipRotation = (normalizedBones[0] + normalizedBones[2] + normalizedBones[4]).normalized;
            fullBodyIK.transform.forward = Vector3.Lerp(fullBodyIK.transform.forward, new Vector3(hipRotation.x, 0, hipRotation.z), 0.1f);
        }

        // Move each bone toward its target direction based on the normalized vectors
        for (int i = 0; i < BoneCount; i++)
        {
            boneList[NormalizeJoint[i, 1]].position = Vector3.Lerp(
                boneList[NormalizeJoint[i, 1]].position,
                boneList[NormalizeJoint[i, 0]].position + boneDistances[i] * normalizedBones[i],
                0.05f
            );

            // Optional: Debug lines to visualize bone movement
            // DrawDebugLine(boneList[NormalizeJoint[i, 0]].position + Vector3.right, boneList[NormalizeJoint[i, 1]].position + Vector3.right, Color.red);
        }

        // Optional: Draw the joint connections
        // for (int i = 0; i < JointCount - 1; i++)
        // {
        //     DrawDebugLine(points[i] * 0.001f + new Vector3(-1, 0.8f, 0), points[i + 1] * 0.001f + new Vector3(-1, 0.8f, 0), Color.blue);
        // }
    }

    // Utility method to draw debug lines in the scene view (disabled by default)
    // private void DrawDebugLine(Vector3 start, Vector3 end, Color color)
    // {
    //     Debug.DrawLine(start, end, color);
    // }
}

// --- Enums for referring to specific bones in the model ---

// Maps to bone GameObjects in the Unity scene
enum BoneRef
{
    Hips,
    RightKnee,
    RightFoot,
    LeftKnee,
    LeftFoot,
    Neck,
    Head,
    LeftArm,
    LeftElbow,
    LeftWrist,
    RightArm,
    RightElbow,
    RightWrist,
}

// Maps the logical bone structure used for normalization and animation
enum NormalizeBoneRef
{
    Hip2LeftKnee,
    LeftKnee2LeftFoot,
    Hip2RightKnee,
    RightKnee2RightFoot,
    Hip2Neck,
    Neck2Head,
    Neck2RightArm,
    RightArm2RightElbow,
    RightElbow2RightWrist,
    Neck2LeftArm,
    LeftArm2LeftElbow,
    LeftElbow2LeftWrist
}
