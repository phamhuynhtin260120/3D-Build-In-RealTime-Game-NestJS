using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 14f, 0f);
    public float smoothTime = 0.12f;

    [Header("Look")]
    public bool lookAtTarget = false;
    public Vector3 fixedEulerAngles = new Vector3(90f, 0f, 0f);

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime
        );

        if (lookAtTarget)
        {
            transform.LookAt(target.position);
        }
        else
        {
            transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}