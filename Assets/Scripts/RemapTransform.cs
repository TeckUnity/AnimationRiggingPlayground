using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Animations;

using Unity.Burst;
using Unity.Mathematics;

public enum TransformMapping { Location, Rotation, Scale };

[BurstCompile]
public struct RemapTransformJob : IWeightedAnimationJob
{
    public ReadWriteTransformHandle destination;
    public ReadOnlyTransformHandle source;
    public bool extrapolate;
    public TransformMapping sourceMapping;
    public TransformMapping destinationMapping;
    public Vector3 toX;
    public Vector3 toY;
    public Vector3 toZ;
    //xy => from min/max, zw => to min/max
    public Vector4 xMapping;
    public Vector4 yMapping;
    public Vector4 zMapping;

    public Space fromSpace;
    public Space toSpace;

    public float3 offsetPos;
    public float3 offsetRot;

    public FloatProperty jobWeight { get; set; }

    public float3 lastRotation;
    private float3 revolutions;

    public void ProcessRootMotion(UnityEngine.Animations.AnimationStream stream) { }

    public void ProcessAnimation(UnityEngine.Animations.AnimationStream stream)
    {
        float w = jobWeight.Get(stream);
        if (w > 0f)
        {
            float3 v = sourceMapping == TransformMapping.Location ? source.GetLocalPosition(stream) : sourceMapping == TransformMapping.Rotation ? source.GetLocalRotation(stream).eulerAngles : source.GetLocalScale(stream);
            if (sourceMapping == TransformMapping.Rotation)
            {
                if (math.abs(v.x - lastRotation.x) > 180)
                {
                    revolutions += math.sign(v.x - lastRotation.x);
                }
                if (math.abs(v.y - lastRotation.y) > 180)
                {
                    revolutions += math.sign(v.y - lastRotation.y);
                }
                if (math.abs(v.z - lastRotation.z) > 180)
                {
                    revolutions += math.sign(v.z - lastRotation.z);
                }
            }
            v -= revolutions * 360;
            var x = xMapping.sqrMagnitude > 0 ? math.remap(xMapping.x, xMapping.y, xMapping.z, xMapping.w, math.dot(v, toX)) : 0;
            var y = yMapping.sqrMagnitude > 0 ? math.remap(yMapping.x, yMapping.y, yMapping.z, yMapping.w, math.dot(v, toY)) : 0;
            var z = zMapping.sqrMagnitude > 0 ? math.remap(zMapping.x, zMapping.y, zMapping.z, zMapping.w, math.dot(v, toZ)) : 0;
            if (!extrapolate)
            {
                x = math.clamp(x, math.min(xMapping.z, xMapping.w), math.max(xMapping.z, xMapping.w));
                y = math.clamp(y, math.min(yMapping.z, yMapping.w), math.max(yMapping.z, yMapping.w));
                z = math.clamp(z, math.min(zMapping.z, zMapping.w), math.max(zMapping.z, zMapping.w));
            }
            switch (destinationMapping)
            {
                case TransformMapping.Location:
                    destination.SetLocalPosition(stream, offsetPos + new float3(x, y, z));
                    break;
                case TransformMapping.Rotation:
                    destination.SetLocalRotation(stream, Quaternion.Euler(offsetRot + new float3(x, y, z)));
                    break;
                case TransformMapping.Scale:
                    destination.SetLocalScale(stream, new float3(x, y, z));
                    break;
            }
            lastRotation = source.GetLocalRotation(stream).eulerAngles;
        }
    }
}

[System.Serializable]
public struct RemapTransformData : IAnimationJobData
{
    public enum Axis { X, Y, Z };

    [SyncSceneToStream] public Transform sourceObject;
    public Transform destinationObject;

    [NotKeyable, SerializeField] public bool m_Extrapolate;

    [Header("Source")]
    [NotKeyable, SerializeField] public TransformMapping sourceMapping;
    [NotKeyable, SerializeField] public Vector2 fromXRange;
    [NotKeyable, SerializeField] public Vector2 fromYRange;
    [NotKeyable, SerializeField] public Vector2 fromZRange;

    [NotKeyable, SerializeField] public Space fromSpace;

    [Header("Axis Mapping")]
    [NotKeyable, SerializeField] public Axis toX;
    [NotKeyable, SerializeField] public Axis toY;
    [NotKeyable, SerializeField] public Axis toZ;

    [Header("Destination")]
    [NotKeyable, SerializeField] public TransformMapping destinationMapping;
    [NotKeyable, SerializeField] public Vector2 toXRange;
    [NotKeyable, SerializeField] public Vector2 toYRange;
    [NotKeyable, SerializeField] public Vector2 toZRange;

    [NotKeyable, SerializeField] public Space toSpace;

    public bool IsValid()
    {
        return !(destinationObject == null || sourceObject == null);
    }

    public void SetDefaultValues()
    {
        destinationObject = null;
        sourceObject = null;
        toX = Axis.X;
        toY = Axis.Y;
        toZ = Axis.Z;
        fromSpace = Space.Self;
        toSpace = Space.Self;
    }
}

public class RemapTransformBinder : AnimationJobBinder<RemapTransformJob, RemapTransformData>
{
    public override RemapTransformJob Create(Animator animator, ref RemapTransformData data, Component component)
    {
        Vector2[] fromRange = new Vector2[] { data.fromXRange, data.fromYRange, data.fromZRange };
        return new RemapTransformJob()
        {
            destination = ReadWriteTransformHandle.Bind(animator, data.destinationObject),
            source = ReadOnlyTransformHandle.Bind(animator, data.sourceObject),

            sourceMapping = data.sourceMapping,
            destinationMapping = data.destinationMapping,

            offsetPos = data.destinationObject.localPosition,
            offsetRot = data.destinationObject.localEulerAngles,
            lastRotation = data.sourceObject.localEulerAngles,

            extrapolate = data.m_Extrapolate,
            toX = Convert(data.toX),
            toY = Convert(data.toY),
            toZ = Convert(data.toZ),
            xMapping = math.abs(fromRange[(int)data.toX].x - fromRange[(int)data.toX].y) > 0 ? new float4(fromRange[(int)data.toX].x, fromRange[(int)data.toX].y, data.toXRange.x, data.toXRange.y) : new float4(0, 0, 0, 0),
            yMapping = math.abs(fromRange[(int)data.toY].x - fromRange[(int)data.toY].y) > 0 ? new float4(fromRange[(int)data.toY].x, fromRange[(int)data.toY].y, data.toYRange.x, data.toYRange.y) : new float4(0, 0, 0, 0),
            zMapping = math.abs(fromRange[(int)data.toZ].x - fromRange[(int)data.toZ].y) > 0 ? new float4(fromRange[(int)data.toZ].x, fromRange[(int)data.toZ].y, data.toZRange.x, data.toZRange.y) : new float4(0, 0, 0, 0)
        };
    }

    Vector3 Convert(RemapTransformData.Axis axis)
    {
        switch (axis)
        {
            case RemapTransformData.Axis.X:
                return Vector3.right;
            case RemapTransformData.Axis.Y:
                return Vector3.up;
            case RemapTransformData.Axis.Z:
                return Vector3.forward;
        }
        return Vector3.right;
    }

    public override void Destroy(RemapTransformJob job) { }
}

[DisallowMultipleComponent, AddComponentMenu("Animation Rigging/Remap Transform")]
public class RemapTransform : RigConstraint<
    RemapTransformJob,
    RemapTransformData,
    RemapTransformBinder
    >
{
}