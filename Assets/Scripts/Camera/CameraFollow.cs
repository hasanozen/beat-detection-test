using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow current { get; private set; }

    [SerializeField] private Transform target;
    [SerializeField] private float smooth;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float minYHeight;
    [SerializeField] private float maxYHeight;

    private void Awake()
    {
        if (current == null)
            current = this;
    }

    private void Update()
    {
        Follow();
    }

    void Follow()
    {
        Vector3 temp = transform.position;
        float clampedY = Mathf.Clamp(transform.position.y, minYHeight, maxYHeight);

        Vector3 desiredPos = target.position + offset;
        Vector3 clampedPos = new Vector3(transform.position.x, clampedY);
        Vector3 smoothPos = Vector3.Lerp(clampedPos, desiredPos, smooth * Time.deltaTime);

        temp.x = smoothPos.x;
        temp.y = smoothPos.y;

        transform.position = temp;
        transform.LookAt(target);
    }
}
