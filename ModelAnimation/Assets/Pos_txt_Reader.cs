
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPPosReceiver : MonoBehaviour
{
    public int port = 5000; // Port to listen on
    private UdpClient udpClient;
    private IPEndPoint endPoint;
    private bool receiving = true;

    float scale_ratio = 0.001f;
    float heal_position = 0.05f;
    float head_angle = 15f;

    Transform[] bone_t;
    int bone_num = 17;
    int[] bones = new int[] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 };
    int[] child_bones = new int[] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 };
    Quaternion[] init_inv;
    Vector3 init_position;
    Animator anim;

    void Start()
    {
        udpClient = new UdpClient(port);
        endPoint = new IPEndPoint(IPAddress.Any, port);
        anim = GetComponent<Animator>();

        StartCoroutine(ReceiveData());
        InitializeBones();
    }

    IEnumerator ReceiveData()
    {
        while (receiving)
        {
            if (udpClient.Available > 0)
            {
                byte[] receivedBytes = udpClient.Receive(ref endPoint);
                string receivedString = Encoding.UTF8.GetString(receivedBytes);
                ParsePoseData(receivedString);
            }
            yield return null;
        }
    }

    void ParsePoseData(string data)
    {
        string[] values = data.Split(',');
        if (values.Length != 51) return; // Ensure 17 joints * 3 coordinates

        Vector3[] now_pos = new Vector3[bone_num];
        for (int i = 0; i < bone_num; i++)
        {
            float x = float.Parse(values[i * 3]);
            float y = float.Parse(values[i * 3 + 1]);
            float z = float.Parse(values[i * 3 + 2]);
            now_pos[i] = new Vector3(x, -y, z) * scale_ratio;
        }
        UpdateModel(now_pos);
    }

    void UpdateModel(Vector3[] now_pos)
    {
        Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);

        if (pos_forward.magnitude < 0.0001f)
        {
            Debug.LogWarning("pos_forward is zero, using default forward vector.");
            pos_forward = Vector3.forward; // Provide a default vector
        }

        bone_t[0].position = now_pos[0] + new Vector3(init_position.x, heal_position, init_position.z);

        if ((now_pos[7] - now_pos[4]).magnitude < 0.0001f)
        {
            Debug.LogWarning("Bone positions too close, skipping rotation.");
            return; // Skip rotation update to prevent errors
        }

        bone_t[0].rotation = Quaternion.LookRotation(pos_forward) * init_inv[0] * bone_t[0].rotation;

        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = child_bones[i];

            Vector3 direction = now_pos[b] - now_pos[cb];

            if (direction.magnitude < 0.0001f)
            {
                Debug.LogWarning($"Bone {b} and {cb} are too close, skipping rotation.");
                continue; // Skip if the vector is zero
            }

            bone_t[b].rotation = Quaternion.LookRotation(direction, pos_forward) * init_inv[b] * bone_t[b].rotation;
        }

        Vector3 head_direction = bone_t[11].position - bone_t[14].position;
        if (head_direction.magnitude > 0.0001f)
        {
            bone_t[8].rotation = Quaternion.AngleAxis(head_angle, head_direction) * bone_t[8].rotation;
        }
        else
        {
            Debug.LogWarning("Head direction is too small, skipping head rotation.");
        }
    }

    void InitializeBones()
    {
        bone_t = new Transform[bone_num];
        init_inv = new Quaternion[bone_num];

        bone_t[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
        bone_t[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        bone_t[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        bone_t[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        bone_t[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        bone_t[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        bone_t[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        bone_t[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
        bone_t[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
        bone_t[10] = anim.GetBoneTransform(HumanBodyBones.Head);
        bone_t[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        bone_t[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        bone_t[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        bone_t[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        bone_t[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        bone_t[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        Vector3 init_forward = TriangleNormal(bone_t[7].position, bone_t[4].position, bone_t[1].position);
        init_inv[0] = Quaternion.Inverse(Quaternion.LookRotation(init_forward));
        init_position = bone_t[0].position;

        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = child_bones[i];

            Vector3 direction = bone_t[b].position - bone_t[cb].position;

            if (direction.magnitude < 0.0001f)
            {
                Debug.LogWarning($"Initialization: Bone {b} and {cb} are too close, skipping inverse quaternion.");
                init_inv[b] = Quaternion.identity; // Default to identity
            }
            else
            {
                init_inv[b] = Quaternion.Inverse(Quaternion.LookRotation(direction, init_forward));
            }
        }
    }

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;
        Vector3 normal = Vector3.Cross(d1, d2);

        if (normal.magnitude < 0.0001f)  // Avoid zero vector
        {
            Debug.LogWarning("Triangle normal is zero, using default normal.");
            return Vector3.forward; // Default to forward vector
        }

        return normal.normalized;
    }

    void OnApplicationQuit()
    {
        receiving = false;
        udpClient.Close();
    }
}


// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Net;
// using System.Net.Sockets;
// using System.Text;
// using UnityEngine;
//
// public class UDPPosReceiver : MonoBehaviour
// {
//     public int port = 5000; // Port to listen on
//     private UdpClient udpClient;
//     private IPEndPoint endPoint;
//     private bool receiving = true;
//
//     float scale_ratio = 0.001f;
//     float heal_position = 0.05f;
//     float head_angle = 15f;
//
//     Transform[] bone_t;
//     int bone_num = 17;
//     int[] bones = new int[] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 };
//     int[] child_bones = new int[] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 };
//     Quaternion[] init_inv;
//     Vector3 init_position;
//     Animator anim;
//
//     void Start()
//     {
//         udpClient = new UdpClient(port);
//         endPoint = new IPEndPoint(IPAddress.Any, port);
//         anim = GetComponent<Animator>();
//
//         StartCoroutine(ReceiveData());
//         InitializeBones();
//     }
//
//     IEnumerator ReceiveData()
//     {
//         while (receiving)
//         {
//             if (udpClient.Available > 0)
//             {
//                 byte[] receivedBytes = udpClient.Receive(ref endPoint);
//                 string receivedString = Encoding.UTF8.GetString(receivedBytes);
//                 ParsePoseData(receivedString);
//             }
//             yield return null;
//         }
//     }
//
//     void ParsePoseData(string data)
//     {
//         string[] values = data.Split(',');
//         if (values.Length != 51) return; // Ensure 17 joints * 3 coordinates
//
//         Vector3[] now_pos = new Vector3[bone_num];
//         for (int i = 0; i < bone_num; i++)
//         {
//             float x = float.Parse(values[i * 3]);
//             float y = float.Parse(values[i * 3 + 1]);
//             float z = float.Parse(values[i * 3 + 2]);
//             now_pos[i] = new Vector3(-x, y, -z) * scale_ratio;
//         }
//         UpdateModel(now_pos);
//     }
//
//     void UpdateModel(Vector3[] now_pos)
//     {
//         Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);
//         bone_t[0].position = now_pos[0] + new Vector3(init_position.x, heal_position, init_position.z);
//         bone_t[0].rotation = Quaternion.LookRotation(pos_forward) * init_inv[0] * bone_t[0].rotation;
//
//         for (int i = 0; i < bones.Length; i++)
//         {
//             int b = bones[i];
//             int cb = child_bones[i];
//             bone_t[b].rotation = Quaternion.LookRotation(now_pos[b] - now_pos[cb], pos_forward) * init_inv[b] * bone_t[b].rotation;
//         }
//
//         bone_t[8].rotation = Quaternion.AngleAxis(head_angle, bone_t[11].position - bone_t[14].position) * bone_t[8].rotation;
//     }
//
//     void InitializeBones()
//     {
//         bone_t = new Transform[bone_num];
//         init_inv = new Quaternion[bone_num];
//
//         bone_t[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
//         bone_t[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
//         bone_t[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
//         bone_t[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
//         bone_t[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
//         bone_t[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
//         bone_t[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
//         bone_t[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
//         bone_t[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
//         bone_t[10] = anim.GetBoneTransform(HumanBodyBones.Head);
//         bone_t[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
//         bone_t[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
//         bone_t[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
//         bone_t[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
//         bone_t[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
//         bone_t[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);
//
//         Vector3 init_forward = TriangleNormal(bone_t[7].position, bone_t[4].position, bone_t[1].position);
//         init_inv[0] = Quaternion.Inverse(Quaternion.LookRotation(init_forward));
//         init_position = bone_t[0].position;
//
//         for (int i = 0; i < bones.Length; i++)
//         {
//             int b = bones[i];
//             int cb = child_bones[i];
//             init_inv[b] = Quaternion.Inverse(Quaternion.LookRotation(bone_t[b].position - bone_t[cb].position, init_forward));
//         }
//     }
//
//     Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
//     {
//         Vector3 d1 = a - b;
//         Vector3 d2 = a - c;
//         Vector3 normal = Vector3.Cross(d1, d2);
//         return normal.normalized;
//     }
//
//     void OnApplicationQuit()
//     {
//         receiving = false;
//         udpClient.Close();
//     }
// }
