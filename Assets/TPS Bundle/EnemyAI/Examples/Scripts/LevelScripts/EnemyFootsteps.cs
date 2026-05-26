using UnityEngine;

// Enemy footsteps using Wwise + distance-based attenuation RTPC
public class EnemyFootsteps : MonoBehaviour
{
    private Animator anim;
    private Transform listener; // usually player/camera

    private bool playedLeftFoot;
    private bool playedRightFoot;

    private Vector3 leftFootIKPos;
    private Vector3 rightFootIKPos;

    [Header("Wwise RTPC")]
    public string distanceRtpc = "Footstep_Distance";

    [Header("Movement threshold")]
    public float moveThreshold = 1.4f;

    void Awake()
    {
        anim = GetComponent<Animator>();

        // Find player automatically (tag-based)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            listener = player.transform;
    }

    void Update()
    {
        if (listener == null)
            return;

        float factor = 0.15f;

        float speed = anim.velocity.magnitude;

        // Send distance-based RTPC continuously
        float distance = Vector3.Distance(transform.position, listener.position);

        // Normalize (adjust range as needed)
        float rtpcValue = Mathf.Clamp(distance, 0f, 50f);

        AkUnitySoundEngine.SetRTPCValue(distanceRtpc, rtpcValue, gameObject);

        // Movement check
        if (speed > moveThreshold)
        {
            if (Vector3.Distance(leftFootIKPos, anim.pivotPosition) <= factor && !playedLeftFoot)
            {
                PlayFootStep();
                playedLeftFoot = true;
                playedRightFoot = false;
            }

            if (Vector3.Distance(rightFootIKPos, anim.pivotPosition) <= factor && !playedRightFoot)
            {
                PlayFootStep();
                playedRightFoot = true;
                playedLeftFoot = false;
            }
        }
        else
        {
            AkUnitySoundEngine.PostEvent("stop_walk", gameObject);

            playedLeftFoot = false;
            playedRightFoot = false;
        }
    }

    void PlayFootStep()
    {
        AkUnitySoundEngine.PostEvent("Play_walk", gameObject);
    }

    void OnAnimatorIK()
    {
        leftFootIKPos = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
        rightFootIKPos = anim.GetIKPosition(AvatarIKGoal.RightFoot);
    }
}