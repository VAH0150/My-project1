using UnityEngine;

// Player movement using Wwise + AkUnitySoundEngine
// Rewritten from Unity audio implementation

public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 7.5f;

    Vector3 movement;
    Animator anim;
    Rigidbody playerRigidbody;

    float camRayLength = 100f;

    private Transform gun, ball;

    // Wwise walking state tracking
    private bool isWalking = false;

    void Awake()
    {
        // Set up references.
        anim = GetComponent<Animator>();
        playerRigidbody = GetComponent<Rigidbody>();

        gun = transform.Find("gun");
        ball = transform.Find("ball");
    }

    void FixedUpdate()
    {
        // Store the input axes.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Move the player around the scene.
        Move(h, v);

        // Turn the player to face the mouse cursor.
        Turning();

        GetComponent<CapsuleCollider>().enabled = !Input.GetMouseButton(1);
    }

    void Move(float h, float v)
    {
        // Set the movement vector based on input.
        movement.Set(h, 0f, v);

        bool playerMoving = movement.magnitude > 0.1f;

        // WWISE FOOTSTEP LOGIC
        if (playerMoving && !isWalking)
        {
            AkUnitySoundEngine.PostEvent("Play_walk", gameObject);
            isWalking = true;
        }
        else if (!playerMoving && isWalking)
        {
            AkUnitySoundEngine.PostEvent("stop_walk", gameObject);
            isWalking = false;
        }

        // Normalize movement vector and apply movement speed.
        movement = movement.normalized *
                  (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed) *
                  Time.deltaTime;

        // Move the player.
        playerRigidbody.MovePosition(transform.position + movement);
    }

    void Turning()
    {
        // Create a ray from the mouse cursor.
        Ray camRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;

        // Perform raycast.
        if (Physics.Raycast(camRay, out hit, camRayLength))
        {
            if (Vector3.Distance(hit.point, transform.position) < 2.2f)
                return;

            // Create direction vector.
            Vector3 playerToMouse = hit.point - gun.Find("muzzle").position;

            gun.localRotation = Quaternion.LookRotation(playerToMouse);
            gun.localRotation = Quaternion.Euler(
                gun.localRotation.eulerAngles.x,
                0,
                0
            );

            // Keep movement on floor plane.
            playerToMouse.y = 0f;

            // Rotate player.
            Quaternion newRotation = Quaternion.LookRotation(playerToMouse);

            playerRigidbody.MoveRotation(newRotation);
        }
    }

    private void OnDisable()
    {
        // Safety stop for Wwise event cleanup
        AkUnitySoundEngine.PostEvent("stop_walk", gameObject);
    }
}