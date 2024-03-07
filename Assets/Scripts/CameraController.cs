using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 20f;
    public float panBorderThickness = 10f;
    public Vector2 panLimit;  // Limits for panning.

    public float scrollSpeed = 20f;
    public float minY = 20f;
    public float maxY = 120f;

    public float rotationSpeed = 50f; // Speed for free rotation.
    public Vector2 rotationLimit = new Vector2(-80, 80); // Min and Max pitch angles (tilt limits).

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleRotation();
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetMouseButton(1))
        {
            // Calculate mouse movement
            float horizontalMovement = -Input.GetAxis("Mouse X");
            float verticalMovement = -Input.GetAxis("Mouse Y");

            moveDirection = new Vector3(horizontalMovement, 0, verticalMovement) * panSpeed;
        }
        else
        {
            if (Input.GetKey("w"))
            {
                moveDirection += transform.forward;
            }
            if (Input.GetKey("s"))
            {
                moveDirection -= transform.forward;
            }
            if (Input.GetKey("d"))
            {
                moveDirection += transform.right;
            }
            if (Input.GetKey("a"))
            {
                moveDirection -= transform.right;
            }
        }

        // Ensure the movement is purely horizontal
        moveDirection.y = 0;

        Vector3 newPos = transform.position + moveDirection * panSpeed * Time.fixedDeltaTime;

        // Clamp the position values
        newPos.x = Mathf.Clamp(newPos.x, -panLimit.x, panLimit.x);
        newPos.z = Mathf.Clamp(newPos.z, -panLimit.y, panLimit.y);

        transform.position = newPos;
    }



    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 pos = transform.position;
        pos.y -= scroll * scrollSpeed * 100f * Time.fixedDeltaTime;

        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(2)) // Middle Mouse Button for rotation.
        {
            float horizontalRotation = Input.GetAxis("Mouse X") * rotationSpeed * Time.fixedDeltaTime;
            float verticalRotation = Input.GetAxis("Mouse Y") * rotationSpeed * Time.fixedDeltaTime;

            transform.Rotate(Vector3.up, horizontalRotation, Space.World);

            // Handle pitch rotation (tilt) with clamping.
            float currentEulerX = transform.eulerAngles.x;
            if (currentEulerX > 180f) currentEulerX -= 360; // Convert angle to -180 to 180 for easier calculations.

            float newRotation = Mathf.Clamp(currentEulerX - verticalRotation, rotationLimit.x, rotationLimit.y);
            transform.rotation = Quaternion.Euler(newRotation, transform.eulerAngles.y, 0);
        }
    }
}
