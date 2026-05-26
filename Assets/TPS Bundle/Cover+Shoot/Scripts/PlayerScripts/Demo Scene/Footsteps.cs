using UnityEngine;

// Player footsteps using Wwise + AkUnitySoundEngine
// Rewritten from Unity AudioSource implementation

public class Footsteps : MonoBehaviour
{
    private Animator anim;

    private Transform lFoot, rFoot;

    private float dist;

    private int groundedBool,
                coverBool,
                aimBool,
                crouchFloat;

    private bool grounded;

    private enum Foot
    {
        LEFT,
        RIGHT
    }

    private Foot step = Foot.LEFT;

    private float oldDist,
                  maxDist = 0;

    void Awake()
    {
        anim = GetComponent<Animator>();

        lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

        groundedBool = Animator.StringToHash("Grounded");
        coverBool = Animator.StringToHash("Cover");
        aimBool = Animator.StringToHash("Aim");
        crouchFloat = Animator.StringToHash("Crouch");
    }

    private void Update()
    {
        // Landing sound
        if (!grounded && anim.GetBool(groundedBool))
        {
            PlayFootStep();
        }

        grounded = anim.GetBool(groundedBool);

        float factor = 0.15f;

        // Adjust footstep trigger sensitivity
        if (anim.GetBool(coverBool) || anim.GetBool(aimBool))
        {
            if (anim.GetFloat(crouchFloat) < 0.5f
                && !anim.GetBool(aimBool))
            {
                factor = 0.17f;
            }
            else
            {
                factor = 0.11f;
            }
        }

        // Only play footsteps while grounded and moving
        if (grounded && anim.velocity.magnitude > 1.6f)
        {
            oldDist = maxDist;

            switch (step)
            {
                case Foot.LEFT:

                    dist = lFoot.position.y - transform.position.y;

                    maxDist = dist > maxDist
                        ? dist
                        : maxDist;

                    if (dist <= factor)
                    {
                        PlayFootStep();
                        step = Foot.RIGHT;
                    }

                    break;

                case Foot.RIGHT:

                    dist = rFoot.position.y - transform.position.y;

                    maxDist = dist > maxDist
                        ? dist
                        : maxDist;

                    if (dist <= factor)
                    {
                        PlayFootStep();
                        step = Foot.LEFT;
                    }

                    break;
            }
        }
        else
        {
            // Stop walking loop when player stops
            AkUnitySoundEngine.PostEvent("stop_walk", gameObject);
        }
    }

    private void PlayFootStep()
    {
        // Still stepping away
        if (oldDist < maxDist)
            return;

        oldDist = 0;
        maxDist = 0;

        // Wwise footstep event
        AkUnitySoundEngine.PostEvent("Play_walk", gameObject);
    }

    private void OnDisable()
    {
        // Cleanup stop event
        AkUnitySoundEngine.PostEvent("stop_walk", gameObject);
    }
}