using UnityEngine;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;


// This class is created for the example scene. There is no support for this script.
namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public bool m_IsWalking;
        public float m_WalkSpeed;
        public float m_RunSpeed;
        [Range(0f, 1f)] public float m_RunstepLenghten;
        public float m_JumpSpeed;
        public float m_StickToGroundForce;
        public float m_GravityMultiplier;
        public MouseLook m_MouseLook;
        public bool m_UseFovKick;
        public FOVKick m_FovKick = new FOVKick();
        public bool m_UseHeadBob;
        public CurveControlledBob m_HeadBob = new CurveControlledBob();
        public LerpControlledBob m_JumpBob = new LerpControlledBob();
        public float m_StepInterval;

        private Camera m_Camera;
        private bool m_Jump;
        private float m_YRotation;
        private Vector2 m_Input;
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;
        private float m_StepCycle;
        private float m_NextStep;
        private bool m_Jumping;

        private float m_crouchFactor = 2f;

        // Wwise walking state
        private bool m_WalkEventPlaying = false;

        private void Start()
        {
            m_CharacterController = GetComponent<CharacterController>();
            m_Camera = Camera.main;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;

            m_FovKick.Setup(m_Camera);
            m_HeadBob.Setup(m_Camera, m_StepInterval);

            m_StepCycle = 0f;
            m_NextStep = m_StepCycle / 2f;
            m_Jumping = false;

            m_MouseLook.Init(transform, m_Camera.transform);
        }

        private void Update()
        {
            RotateView();

            if (!m_Jump)
                m_Jump = Input.GetButtonDown("Jump");

            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayLandingSound();
                m_MoveDir.y = 0f;
                m_Jumping = false;
            }

            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
            {
                m_MoveDir.y = 0f;
            }

            m_PreviouslyGrounded = m_CharacterController.isGrounded;

            // crouch
            if (Input.GetMouseButtonDown(1))
            {
                m_CharacterController.height /= m_crouchFactor;
                m_WalkSpeed /= m_crouchFactor;
                m_RunSpeed /= m_crouchFactor;
                transform.Find("eyes").localPosition /= m_crouchFactor;
                transform.Find("target").localPosition /= m_crouchFactor;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                m_CharacterController.height *= m_crouchFactor;
                m_WalkSpeed *= m_crouchFactor;
                m_RunSpeed *= m_crouchFactor;
                transform.Find("eyes").localPosition *= m_crouchFactor;
                transform.Find("target").localPosition *= m_crouchFactor;
            }
        }

        private void FixedUpdate()
        {
            float speed;
            GetInput(out speed);

            Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

            RaycastHit hitInfo;
            Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down,
                out hitInfo, m_CharacterController.height / 2f, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

            m_MoveDir.x = desiredMove.x * speed;
            m_MoveDir.z = desiredMove.z * speed;

            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_Jump)
                {
                    m_MoveDir.y = m_JumpSpeed;
                    PlayJumpSound();
                    m_Jump = false;
                    m_Jumping = true;
                }
            }
            else
            {
                m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
            }

            m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

            ProgressStepCycle(speed);
            UpdateCameraPosition(speed);

            HandleFootstepState();

            m_MouseLook.UpdateCursorLock();
        }

        // ---------------- WWISE AUDIO ----------------

        private void PlayJumpSound()
        {
            AkUnitySoundEngine.PostEvent("play_jump", gameObject);
            Debug.Log("Jump");
        }

        private void PlayLandingSound()
        {
            AkUnitySoundEngine.PostEvent("play_land", gameObject);
            m_NextStep = m_StepCycle + .5f;
            Debug.Log("Land");

        }

        private void HandleFootstepState()
        {
            bool isMoving = m_CharacterController.isGrounded &&
                            new Vector2(m_CharacterController.velocity.x, m_CharacterController.velocity.z).magnitude > 0.1f &&
                            (m_Input.x != 0 || m_Input.y != 0);

            if (isMoving)
            {
                if (!m_WalkEventPlaying)
                {
                    AkUnitySoundEngine.PostEvent("Play_walk", gameObject);
                    m_WalkEventPlaying = true;
                }
            }
            else
            {
                if (m_WalkEventPlaying)
                {
                    AkUnitySoundEngine.PostEvent("stop_walk", gameObject);
                    m_WalkEventPlaying = false;
                }
            }
        }

        // ---------------- MOVEMENT ----------------

        private void ProgressStepCycle(float speed)
        {
            if (m_CharacterController.velocity.sqrMagnitude > 0 &&
                (m_Input.x != 0 || m_Input.y != 0))
            {
                m_StepCycle += (m_CharacterController.velocity.magnitude +
                                (speed * (m_IsWalking ? 1f : m_RunstepLenghten)))
                                * Time.fixedDeltaTime;
            }
        }

        private void UpdateCameraPosition(float speed)
        {
            if (!m_UseHeadBob)
                return;

            Vector3 newCameraPosition;

            if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
            {
                m_Camera.transform.localPosition =
                    m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude +
                    (speed * (m_IsWalking ? 1f : m_RunstepLenghten)));

                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y -= m_JumpBob.Offset();
            }
            else
            {
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
            }

            m_Camera.transform.localPosition = newCameraPosition;
        }

        private void GetInput(out float speed)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            bool wasWalking = m_IsWalking;

#if !MOBILE_INPUT
            m_IsWalking = !Input.GetKey(KeyCode.LeftShift);
#endif

            speed = m_IsWalking ? m_WalkSpeed : m_RunSpeed;
            m_Input = new Vector2(horizontal, vertical);

            if (m_Input.sqrMagnitude > 1)
                m_Input.Normalize();

            if (m_IsWalking != wasWalking && m_UseFovKick &&
                m_CharacterController.velocity.sqrMagnitude > 0)
            {
                StopAllCoroutines();
                StartCoroutine(!m_IsWalking ?
                    m_FovKick.FOVKickUp() :
                    m_FovKick.FOVKickDown());
            }
        }

        private void RotateView()
        {
            m_MouseLook.LookRotation(transform, m_Camera.transform);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            if (m_CollisionFlags == CollisionFlags.Below)
                return;

            if (body == null || body.isKinematic)
                return;

            body.AddForceAtPosition(m_CharacterController.velocity * 0.1f,
                hit.point, ForceMode.Impulse);
        }
    }
}