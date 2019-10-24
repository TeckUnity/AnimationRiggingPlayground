using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Animations;
using Unity.Collections;

using Unity.Burst;
using Unity.Mathematics;

// Adapted from https://meuse.co.jp/?p=885

[BurstCompile]
public struct RoboticArmIKJob : IWeightedAnimationJob
{
    public ReadWriteTransformHandle target;
    public NativeArray<ReadWriteTransformHandle> joints;
    public NativeArray<double> theta;
    public NativeArray<float> l;
    public float C3;
    public Vector3 offset;
    float ax, ay, az, bx, by, bz;
    float asx, asy, asz, bsx, bsy, bsz;
    float p5x, p5y, p5z;
    float C1, C23, S1, S23;
    Vector3 p;
    Vector3 r;
    public FloatProperty jobWeight { get; set; }

    public void ProcessRootMotion(UnityEngine.Animations.AnimationStream stream) { }

    public void ProcessAnimation(UnityEngine.Animations.AnimationStream stream)
    {
        float w = jobWeight.Get(stream);
        if (w > 0f)
        {
            p = target.GetPosition(stream) - offset;
            r = target.GetRotation(stream).eulerAngles;
            p = new Vector3(p.z, p.x, p.y);
            r = new Vector3(r.z, r.x, r.y);
            ax = math.cos(math.radians(r.z)) * math.cos(math.radians(r.y));
            ay = math.sin(math.radians(r.z)) * math.cos(math.radians(r.y));
            az = -math.sin(math.radians(r.y));

            p5x = p.x - (l[5] + l[6]) * ax;
            p5y = p.y - (l[5] + l[6]) * ay;
            p5z = p.z - (l[5] + l[6]) * az;

            theta[0] = math.atan2(p5y, p5x);

            C3 = (math.pow(p5x, 2) + math.pow(p5y, 2) + math.pow(p5z - l[1], 2) - math.pow(l[2], 2) - math.pow(l[3] + l[4], 2))
                / (2 * l[2] * (l[3] + l[4]));
            theta[2] = math.atan2(math.pow(1 - math.pow(C3, 2), 0.5f), C3);

            float M = l[2] + (l[3] + l[4]) * C3;
            float N = (l[3] + l[4]) * math.sin((float)theta[2]);
            float A = math.pow(p5x * p5x + p5y * p5y, 0.5f);
            float B = p5z - l[1];
            theta[1] = math.atan2(M * A - N * B, N * A + M * B);

            C1 = math.cos((float)theta[0]);
            C23 = math.cos((float)theta[1] + (float)theta[2]);
            S1 = math.sin((float)theta[0]);
            S23 = math.sin((float)theta[1] + (float)theta[2]);

            bx = math.cos(math.radians(r.x)) * math.sin(math.radians(r.y)) * math.cos(math.radians(r.z))
                - math.sin(math.radians(r.x)) * math.sin(math.radians(r.z));
            by = math.cos(math.radians(r.x)) * math.sin(math.radians(r.y)) * math.sin(math.radians(r.z))
                - math.sin(math.radians(r.x)) * math.cos(math.radians(r.z));
            bz = math.cos(math.radians(r.x)) * math.cos(math.radians(r.y));

            asx = C23 * (C1 * ax + S1 * ay) - S23 * az;
            asy = -S1 * ax + C1 * ay;
            asz = S23 * (C1 * ax + S1 * ay) + C23 * az;
            bsx = C23 * (C1 * bx + S1 * by) - S23 * bz;
            bsy = -S1 * bx + C1 * by;
            bsz = S23 * (C1 * bx + S1 * by) + C23 * bz;

            theta[3] = math.atan2(asy, asx);
            theta[4] = math.atan2(math.cos((float)theta[3]) * asx + math.sin((float)theta[3]) * asy, asz);
            theta[5] = math.atan2(math.cos((float)theta[3]) * bsy - math.sin((float)theta[3]) * bsx, -bsz / math.sin((float)theta[4]));

            if (!double.IsNaN(theta[0]))
                joints[1].SetLocalRotation(stream, quaternion.Euler(Vector3.forward * (float)(theta[0] * w)));
            if (!double.IsNaN(theta[1]))
                joints[2].SetLocalRotation(stream, quaternion.Euler(Vector3.right * (float)(theta[1] * w)));
            if (!double.IsNaN(theta[2]))
                joints[3].SetLocalRotation(stream, quaternion.Euler(Vector3.right * (float)(theta[2] * w)));
            if (!double.IsNaN(theta[3]))
                joints[4].SetLocalRotation(stream, quaternion.Euler(Vector3.forward * (float)(theta[3] * w)));
            if (!double.IsNaN(theta[4]))
                joints[5].SetLocalRotation(stream, quaternion.Euler(Vector3.right * (float)(theta[4] * w)));
            if (!double.IsNaN(theta[5]))
                joints[6].SetLocalRotation(stream, quaternion.Euler(Vector3.forward * (float)(theta[5] * w)));
        }
    }
}

[System.Serializable]
public struct RoboticArmIKData : IAnimationJobData
{
    [SerializeField] public Transform[] m_Joints;
    [SyncSceneToStream, SerializeField] public Transform m_Target;
    public float[] l;
    public float C3;
    public double[] theta;

    public bool IsValid()
    {
        return true;
        // return !(m_Target == null || m_Joints.Length == 7);
    }

    public void SetDefaultValues()
    {
        theta = new double[6];
        l = new float[7];
        theta[0] = theta[1] = theta[2] = theta[3] = theta[4] = theta[5] = 0.0;
        l[1] = l[2] = l[3] = l[4] = l[5] = l[6] = l[6] = 0;
        C3 = 0.0f;
    }
}

public class RoboticArmIKBinder : AnimationJobBinder<RoboticArmIKJob, RoboticArmIKData>
{
    public override RoboticArmIKJob Create(Animator animator, ref RoboticArmIKData data, Component component)
    {
        var jointArray = new NativeArray<ReadWriteTransformHandle>(data.m_Joints.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < data.m_Joints.Length - 1; i++)
        {
            jointArray[i] = ReadWriteTransformHandle.Bind(animator, data.m_Joints[i]);
            data.l[i] = Vector3.Distance(data.m_Joints[i + 1].position, data.m_Joints[i].position);
        }
        var thetaArray = new NativeArray<double>(data.theta.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var lArray = new NativeArray<float>(data.l.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < data.l.Length; i++)
        {
            lArray[i] = data.l[i];
        }
        for (int i = 0; i < data.theta.Length; i++)
        {
            thetaArray[i] = data.theta[i];
        }

        return new RoboticArmIKJob()
        {
            target = ReadWriteTransformHandle.Bind(animator, data.m_Target),
            joints = jointArray,
            theta = thetaArray,
            l = lArray,
            C3 = data.C3,
            offset = data.m_Joints[1].position
        };
    }

    public override void Destroy(RoboticArmIKJob job)
    {
        job.joints.Dispose();
        job.theta.Dispose();
        job.l.Dispose();
    }
}

[DisallowMultipleComponent, AddComponentMenu("Animation Rigging/Robotic Arm IK")]
public class RoboticArmIK : RigConstraint<
    RoboticArmIKJob,
    RoboticArmIKData,
    RoboticArmIKBinder
    >
{
}