using System;
using UnityEngine;

namespace TrickSaber2.Components;

/// <summary>
/// Used to calculate how fast an object is moving.
/// This should not be attached to a <see cref="TrickObject"/> as it will also track the trick movement.
/// </summary>
internal class MovementTracker : MonoBehaviour
{
    private readonly Vector3[] velocityBuffer = new Vector3[5];
    private readonly Vector3[] angularVelocityBuffer = new Vector3[5];
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private int currentProbeIndex;
    
    public Vector3 AngularVelocity { get; private set; }

    private void OnEnable()
    {
        previousPosition = transform.position;
        previousRotation = transform.rotation;
        currentProbeIndex = 0;
        AngularVelocity = Vector3.zero;
        Array.Clear(velocityBuffer, 0, velocityBuffer.Length);
        Array.Clear(angularVelocityBuffer, 0, angularVelocityBuffer.Length);
    }

    public void Update()
    {
        var velocity = (transform.position - previousPosition) / Time.deltaTime;
        AngularVelocity = GetAngularVelocity(previousRotation, transform.rotation, Time.deltaTime);
        UpdateBuffers(velocity, AngularVelocity);
        previousPosition = transform.position;
        previousRotation = transform.rotation;
    }
    
    public Vector3 GetAverageAngularVelocity()
    {
        var sum = Vector3.zero;
        foreach (var velocity in angularVelocityBuffer) sum += velocity;
        return sum / angularVelocityBuffer.Length;
    }
    
    public Vector3 GetAverageVelocity()
    {
        var sum = Vector3.zero;
        foreach (var velocity in velocityBuffer) sum += velocity;
        var avg = sum / velocityBuffer.Length;
        
        return avg;
    }

    private void UpdateBuffers(Vector3 velocity, Vector3 angularVelocity)
    {
        if (currentProbeIndex == velocityBuffer.Length) currentProbeIndex = 0;
        velocityBuffer[currentProbeIndex] = velocity;
        angularVelocityBuffer[currentProbeIndex] = angularVelocity;
        currentProbeIndex++;
    }

    private static Vector3 GetAngularVelocity(Quaternion previousRotation, Quaternion rotation, float time)
    {
        var deltaRotation = rotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out var angle, out var axis);
        angle *= Mathf.Deg2Rad;
        return (1.0f / time) * angle * axis;
    }
}
