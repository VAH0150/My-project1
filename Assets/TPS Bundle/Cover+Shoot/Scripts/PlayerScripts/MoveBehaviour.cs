using UnityEngine;

// MoveBehaviour inherits from GenericBehaviour
public class MoveBehaviour : GenericBehaviour
{
	public float walkSpeed = 0.15f;
	public float runSpeed = 1.0f;
	public float sprintSpeed = 2.0f;
	public float speedDampTime = 0.1f;
	public string jumpButton = "Jump";
	public float jumpHeight = 1.5f;
	public float jumpInertialForce = 10f;

	private float speed, speedSeeker;
	private int jumpBool;
	private int groundedBool;
	private bool jump;
	private bool isColliding;

	// ---------------- FOOTSTEP SYSTEM ----------------
	private float footstepTimer = 0f;
	private float footstepInterval = 0.45f;
	private bool wasMoving = false;

	void Start()
	{
		jumpBool = Animator.StringToHash("Jump");
		groundedBool = Animator.StringToHash("Grounded");

		behaviourManager.GetAnim.SetBool(groundedBool, true);

		behaviourManager.SubscribeBehaviour(this);
		behaviourManager.RegisterDefaultBehaviour(this.behaviourCode);

		speedSeeker = runSpeed;
	}

	void Update()
	{
		if (!jump && Input.GetButtonDown(jumpButton)
			&& behaviourManager.IsCurrentBehaviour(this.behaviourCode)
			&& !behaviourManager.IsOverriding())
		{
			jump = true;
		}
	}

	public override void LocalFixedUpdate()
	{
		MovementManagement(behaviourManager.GetH, behaviourManager.GetV);
		JumpManagement();
		HandleFootsteps();
	}

	// ---------------- FOOTSTEPS ----------------
	void HandleFootsteps()
	{
		bool grounded = behaviourManager.IsGrounded();

		Vector2 input = new Vector2(behaviourManager.GetH, behaviourManager.GetV);
		bool isMoving = input.sqrMagnitude > 0.01f;

		bool shouldWalk = grounded && isMoving && !behaviourManager.GetAnim.GetBool(jumpBool);

		// STOP MOVING
		if (!shouldWalk)
		{
			if (wasMoving)
			{
				AkUnitySoundEngine.PostEvent("pl_stp", gameObject);
			}

			wasMoving = false;
			footstepTimer = 0f;
			return;
		}

		// START MOVING
		if (!wasMoving)
		{
			wasMoving = true;
			footstepTimer = footstepInterval;
		}

		// SPEED-BASED STEP FREQUENCY
		float currentSpeed = new Vector2(
			behaviourManager.GetRigidBody.linearVelocity.x,
			behaviourManager.GetRigidBody.linearVelocity.z
		).magnitude;

		float dynamicInterval = Mathf.Clamp(
			footstepInterval / (currentSpeed + 0.1f),
			0.25f,
			0.6f
		);

		footstepTimer += Time.deltaTime;

		if (footstepTimer >= dynamicInterval)
		{
			AkUnitySoundEngine.PostEvent("pl_wlk", gameObject);
			footstepTimer = 0f;
		}
	}

	// ---------------- JUMP ----------------
	void JumpManagement()
	{
		if (jump && !behaviourManager.GetAnim.GetBool(jumpBool) && behaviourManager.IsGrounded())
		{
			AkUnitySoundEngine.PostEvent("play_jump", gameObject);

			behaviourManager.LockTempBehaviour(this.behaviourCode);
			behaviourManager.GetAnim.SetBool(jumpBool, true);

			if (behaviourManager.GetAnim.GetFloat(speedFloat) > 0.1)
			{
				GetComponent<CapsuleCollider>().material.dynamicFriction = 0f;
				GetComponent<CapsuleCollider>().material.staticFriction = 0f;

				RemoveVerticalVelocity();

				float velocity = 2f * Mathf.Abs(Physics.gravity.y) * jumpHeight;
				velocity = Mathf.Sqrt(velocity);

				behaviourManager.GetRigidBody.AddForce(Vector3.up * velocity, ForceMode.VelocityChange);
			}
		}
		else if (behaviourManager.GetAnim.GetBool(jumpBool))
		{
			if (!behaviourManager.IsGrounded() && !isColliding && behaviourManager.GetTempLockStatus())
			{
				behaviourManager.GetRigidBody.AddForce(
					transform.forward * (jumpInertialForce * Physics.gravity.magnitude * sprintSpeed),
					ForceMode.Acceleration
				);
			}

			if ((behaviourManager.GetRigidBody.linearVelocity.y < 0) && behaviourManager.IsGrounded())
			{
				AkUnitySoundEngine.PostEvent("play_land", gameObject);

				behaviourManager.GetAnim.SetBool(groundedBool, true);

				GetComponent<CapsuleCollider>().material.dynamicFriction = 0.6f;
				GetComponent<CapsuleCollider>().material.staticFriction = 0.6f;

				jump = false;
				behaviourManager.GetAnim.SetBool(jumpBool, false);
				behaviourManager.UnlockTempBehaviour(this.behaviourCode);
			}
		}
	}

	// ---------------- MOVEMENT ----------------
	void MovementManagement(float horizontal, float vertical)
	{
		if (behaviourManager.IsGrounded())
			behaviourManager.GetRigidBody.useGravity = true;

		else if (!behaviourManager.GetAnim.GetBool(jumpBool)
			&& behaviourManager.GetRigidBody.linearVelocity.y > 0)
			RemoveVerticalVelocity();

		Rotating(horizontal, vertical);

		Vector2 dir = new Vector2(horizontal, vertical);
		speed = Vector2.ClampMagnitude(dir, 1f).magnitude;

		speedSeeker += Input.GetAxis("Mouse ScrollWheel");
		speedSeeker = Mathf.Clamp(speedSeeker, walkSpeed, runSpeed);

		speed *= speedSeeker;

		if (behaviourManager.IsSprinting())
			speed = sprintSpeed;

		behaviourManager.GetAnim.SetFloat(speedFloat, speed, speedDampTime, Time.deltaTime);
	}

	private void RemoveVerticalVelocity()
	{
		Vector3 v = behaviourManager.GetRigidBody.linearVelocity;
		v.y = 0;
		behaviourManager.GetRigidBody.linearVelocity = v;
	}

	Vector3 Rotating(float horizontal, float vertical)
	{
		Vector3 forward = behaviourManager.playerCamera.TransformDirection(Vector3.forward);
		forward.y = 0;
		forward = forward.normalized;

		Vector3 right = new Vector3(forward.z, 0, -forward.x);
		Vector3 targetDirection = forward * vertical + right * horizontal;

		if (behaviourManager.IsMoving() && targetDirection != Vector3.zero)
		{
			Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

			Quaternion newRotation = Quaternion.Slerp(
				behaviourManager.GetRigidBody.rotation,
				targetRotation,
				behaviourManager.turnSmoothing
			);

			behaviourManager.GetRigidBody.MoveRotation(newRotation);
			behaviourManager.SetLastDirection(targetDirection);
		}

		if (!(Mathf.Abs(horizontal) > 0.9 || Mathf.Abs(vertical) > 0.9))
		{
			behaviourManager.Repositioning();
		}

		return targetDirection;
	}

	private void OnCollisionStay(Collision collision)
	{
		isColliding = true;

		if (behaviourManager.IsCurrentBehaviour(this.GetBehaviourCode())
			&& collision.GetContact(0).normal.y <= 0.1f)
		{
			GetComponent<CapsuleCollider>().material.dynamicFriction = 0f;
			GetComponent<CapsuleCollider>().material.staticFriction = 0f;
		}
	}

	private void OnCollisionExit(Collision collision)
	{
		isColliding = false;

		GetComponent<CapsuleCollider>().material.dynamicFriction = 0.6f;
		GetComponent<CapsuleCollider>().material.staticFriction = 0.6f;
	}
}