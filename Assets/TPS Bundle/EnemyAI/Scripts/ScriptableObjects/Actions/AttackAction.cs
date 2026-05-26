using System.Collections;
using UnityEngine;
using EnemyAI;

[CreateAssetMenu(menuName = "Enemy AI/Actions/Attack")]
public class AttackAction : Action
{
	private readonly float startShootDelay = 0.2f;
	private readonly float aimAngleGap = 30f;

	private static readonly int Shooting = Animator.StringToHash("Shooting");
	private static readonly int Crouch = Animator.StringToHash("Crouch");

	private string distanceRtpc = "Footstep_Distance";

	public override void Act(StateController controller)
	{
		controller.focusSight = true;

		if (CanShoot(controller))
		{
			UpdateShotRTPC(controller);  
			Shoot(controller);
		}

		controller.variables.blindEngageTimer += Time.deltaTime;
	}

	// ---------------- RTPC LOGIC ----------------
	private void UpdateShotRTPC(StateController controller)
	{
		Vector3 playerPos = controller.personalTarget;

		float distance = Vector3.Distance(
			controller.enemyAnimation.gunMuzzle.position,
			playerPos
		);

		float rtpcValue = Mathf.Clamp(distance, 0f, 50f);

		AkUnitySoundEngine.SetRTPCValue(
			distanceRtpc,
			rtpcValue,
			controller.gameObject
		);
	}

	// ---------------- SHOOT CONDITIONS ----------------
	private bool CanShoot(StateController controller)
	{
		if (controller.Aiming &&
			(controller.enemyAnimation.currentAimAngleGap < aimAngleGap ||
			(controller.personalTarget - controller.enemyAnimation.gunMuzzle.position).sqrMagnitude <= 0.25f))
		{
			if (controller.variables.startShootTimer >= startShootDelay)
			{
				return true;
			}
			else
			{
				controller.variables.startShootTimer += Time.deltaTime;
			}
		}
		return false;
	}

	// ---------------- ENABLE ----------------
	public override void OnEnableAction(StateController controller)
	{
		controller.variables.shotsInRound =
			Random.Range(controller.maximumBurst / 2, controller.maximumBurst);

		controller.variables.currentShots = 0;
		controller.variables.startShootTimer = 0f;

		controller.enemyAnimation.anim.ResetTrigger(Shooting);
		controller.enemyAnimation.anim.SetBool(Crouch, false);

		controller.variables.waitInCoverTimer = 0;
		controller.enemyAnimation.ActivatePendingAim();
	}

	// ---------------- SHOOT ----------------
	private void Shoot(StateController controller)
	{
		if (Time.timeScale > 0 && controller.variables.shotTimer == 0f)
		{
			controller.enemyAnimation.anim.SetTrigger(Shooting);
			CastShot(controller);
		}
		else if (controller.variables.shotTimer >= (0.1f + 2 * Time.deltaTime))
		{
			controller.bullets = Mathf.Max(--controller.bullets, 0);
			controller.variables.currentShots++;
			controller.variables.shotTimer = 0f;
			return;
		}

		controller.variables.shotTimer += controller.classStats.shotRateFactor * Time.deltaTime;
	}

	// ---------------- RAYCAST ----------------
	private void CastShot(StateController controller)
	{
		Vector3 imprecision = Random.Range(-controller.classStats.shotErrorRate, controller.classStats.shotErrorRate)
			* controller.transform.right;

		imprecision += Random.Range(-controller.classStats.shotErrorRate, controller.classStats.shotErrorRate)
			* controller.transform.up;

		Vector3 shotDirection =
			controller.personalTarget - controller.enemyAnimation.gunMuzzle.position;

		Ray ray = new Ray(
			controller.enemyAnimation.gunMuzzle.position,
			shotDirection.normalized + imprecision
		);

		if (Physics.Raycast(ray, out RaycastHit hit, controller.viewRadius,
			controller.generalStats.shotMask.value))
		{
			bool isOrganic =
				((1 << hit.transform.root.gameObject.layer) & controller.generalStats.targetMask) != 0;

			DoShot(controller, ray.direction, hit.point, hit.normal, isOrganic, hit.transform);
		}
		else
		{
			DoShot(controller, ray.direction, ray.origin + (ray.direction * 500f));
		}
	}

	// ---------------- EFFECTS ----------------
	private void DoShot(StateController controller, Vector3 direction, Vector3 hitPoint,
		Vector3 hitNormal = default, bool organic = false, Transform target = null)
	{
		GameObject muzzleFlash =
			Object.Instantiate(controller.classStats.muzzleFlash, controller.enemyAnimation.gunMuzzle);

		muzzleFlash.transform.localPosition = Vector3.zero;
		muzzleFlash.transform.localEulerAngles = Vector3.back * 90f;

		controller.StartCoroutine(DestroyFlash(muzzleFlash));

		GameObject shotTracer =
			Object.Instantiate(controller.classStats.shot, controller.enemyAnimation.gunMuzzle);

		Vector3 origin =
			controller.enemyAnimation.gunMuzzle.position - controller.enemyAnimation.gunMuzzle.right * 0.5f;

		shotTracer.transform.position = origin;
		shotTracer.transform.rotation = Quaternion.LookRotation(direction);

		if (target && !organic)
		{
			GameObject bulletHole = Object.Instantiate(controller.classStats.bulletHole);
			bulletHole.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
			bulletHole.transform.position = hitPoint + 0.01f * hitNormal;

			GameObject sparks = Object.Instantiate(controller.classStats.sparks);
			sparks.transform.position = hitPoint;
		}
		else if (target && organic)
		{
			HealthManager targetHealth = target.GetComponent<HealthManager>();
			if (targetHealth)
			{
				targetHealth.TakeDamage(
					hitPoint,
					direction,
					controller.classStats.bulletDamage,
					target.GetComponent<Collider>(),
					controller.gameObject
				);
			}
		}

		AkUnitySoundEngine.PostEvent("Play_pistolShot", controller.gameObject);
	}

	public IEnumerator DestroyFlash(GameObject flash)
	{
		yield return new WaitForSeconds(0.1f);
		Object.Destroy(flash);
	}
}