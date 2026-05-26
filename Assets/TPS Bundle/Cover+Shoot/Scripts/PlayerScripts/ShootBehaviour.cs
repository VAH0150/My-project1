using System.Collections.Generic;
using UnityEngine;

public class ShootBehaviour : GenericBehaviour
{
	// Updated to use KeyCode instead of a string axis
	public KeyCode shootKey = KeyCode.F;

	public string pickButton = "Interact",
		changeButton = "Change",
		reloadButton = "Reload",
		dropButton = "Drop";
	public Texture2D aimCrosshair, shootCrosshair;
	public GameObject muzzleFlash, shot, sparks;
	public Material bulletHole;
	public int maxBulletHoles = 50;
	public float shotErrorRate = 0.01f;
	public float shotRateFactor = 1f;
	public float armsRotation = 8f;
	public LayerMask shotMask = ~((1 << 2) | (1 << 9) |
								(1 << 10) | (1 << 11));
	public LayerMask organicMask;

	[Header("Advanced Rotation Adjustments")]
	public Vector3 LeftArmShortAim;
	public Vector3 RightHandCover;
	public Vector3 LeftArmLongGuard;

	private int activeWeapon = 0;
	private int weaponTypeInt;
	private int changeWeaponTrigger;
	private int shootingTrigger;
	private List<InteractiveWeapon> weapons;
	private int coveringBool, aimBool,
		blockedAimBool,
		reloadBool;
	private bool isAiming,
		isAimBlocked;
	private Transform gunMuzzle;
	private float distToHand;
	private Vector3 castRelativeOrigin;
	private Dictionary<InteractiveWeapon.WeaponType, int> slotMap;
	private Transform hips, spine, chest, rightHand, leftArm;
	private Vector3 initialRootRotation;
	private Vector3 initialHipsRotation;
	private Vector3 initialSpineRotation;
	private Vector3 initialChestRotation;
	private float shotDecay, originalShotDecay = 0.5f;
	private List<GameObject> bulletHoles;
	private int bulletHoleSlot = 0;
	private int burstShotCount = 0;
	private AimBehaviour aimBehaviour;
	private Texture2D originalCrosshair;
	private bool isShooting = false;
	private bool isChangingWeapon = false;
	private bool isShotAlive = false;

	void Start()
	{
		weaponTypeInt = Animator.StringToHash("Weapon");
		aimBool = Animator.StringToHash("Aim");
		coveringBool = Animator.StringToHash("Cover");
		blockedAimBool = Animator.StringToHash("BlockedAim");
		changeWeaponTrigger = Animator.StringToHash("ChangeWeapon");
		shootingTrigger = Animator.StringToHash("Shooting");
		reloadBool = Animator.StringToHash("Reload");
		weapons = new List<InteractiveWeapon>(new InteractiveWeapon[3]);
		aimBehaviour = this.GetComponent<AimBehaviour>();
		bulletHoles = new List<GameObject>();

		muzzleFlash.SetActive(false);
		shot.SetActive(false);
		sparks.SetActive(false);

		slotMap = new Dictionary<InteractiveWeapon.WeaponType, int>
		{
			{ InteractiveWeapon.WeaponType.SHORT, 1 },
			{ InteractiveWeapon.WeaponType.LONG, 2 }
		};

		Transform neck = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.Neck);
		if (!neck)
		{
			neck = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.Head).parent;
		}
		hips = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.Hips);
		spine = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.Spine);
		chest = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.Chest);
		rightHand = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.RightHand);
		leftArm = behaviourManager.GetAnim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
		Transform root = hips.parent;

		if (spine.parent != hips)
		{
			root = hips;
			hips = spine.parent;
		}

		initialRootRotation = (root == transform) ? Vector3.zero : root.localEulerAngles;
		initialHipsRotation = hips.localEulerAngles;
		initialSpineRotation = spine.localEulerAngles;
		initialChestRotation = chest.localEulerAngles;
		originalCrosshair = aimBehaviour.crosshair;
		shotDecay = originalShotDecay;
		castRelativeOrigin = neck.position - this.transform.position;
		distToHand = (rightHand.position - neck.position).magnitude * 1.5f;
	}

	private void Update()
	{
		// Shooting logic
		if (Input.GetKey(shootKey) && !isShooting && activeWeapon > 0 && burstShotCount == 0)
		{
			isShooting = true;
			ShootWeapon(activeWeapon);
		}
		else if (isShooting && !Input.GetKey(shootKey))
		{
			isShooting = false;
		}
		// Reload logic
		else if (Input.GetButtonUp(reloadButton) && activeWeapon > 0)
		{
			if (weapons[activeWeapon].StartReload())
			{
				// WWISE: Dynamic reload events
				string reloadEvent =
					(weapons[activeWeapon].type == InteractiveWeapon.WeaponType.LONG)
					? "play_ar_reload"
					: "Play_reload";

				AkUnitySoundEngine.PostEvent(reloadEvent, gameObject);

				behaviourManager.GetAnim.SetBool(reloadBool, true);
			}
		}
		else if (Input.GetButtonDown(dropButton) && activeWeapon > 0)
		{
			EndReloadWeapon();
			int weaponToDrop = activeWeapon;
			ChangeWeapon(activeWeapon, 0);
			weapons[weaponToDrop].Drop();
			weapons[weaponToDrop] = null;
		}
		else
		{
			if ((Input.GetAxisRaw(changeButton) != 0 && !isChangingWeapon))
			{
				isChangingWeapon = true;
				int nextWeapon = activeWeapon + 1;
				ChangeWeapon(activeWeapon, (nextWeapon) % weapons.Count);
			}
			else if (Input.GetAxisRaw(changeButton) == 0)
			{
				isChangingWeapon = false;
			}
		}

		if (isShotAlive)
			ShotDecay();

		isAiming = behaviourManager.GetAnim.GetBool(aimBool);
	}

	private void ShootWeapon(int weapon, bool firstShot = true)
	{
		if (!isAiming || isAimBlocked || behaviourManager.GetAnim.GetBool(reloadBool) || !weapons[weapon].Shoot(firstShot))
			return;

		burstShotCount++;
		behaviourManager.GetAnim.SetTrigger(shootingTrigger);
		aimBehaviour.crosshair = shootCrosshair;
		behaviourManager.GetCamScript.BounceVertical(weapons[weapon].recoilAngle);

		Vector3 imprecision = Random.Range(-shotErrorRate, shotErrorRate) * behaviourManager.playerCamera.right;
		Ray ray = new Ray(behaviourManager.playerCamera.position, behaviourManager.playerCamera.forward + imprecision);

		if (Physics.Raycast(ray, out RaycastHit hit, 500f, shotMask))
		{
			if (hit.collider.transform != transform)
			{
				bool isOrganic = (organicMask == (organicMask | (1 << hit.transform.root.gameObject.layer)));
				DrawShoot(hit.point, hit.normal, hit.collider.transform, !isOrganic, !isOrganic);

				if (hit.collider)
					hit.collider.SendMessageUpwards("HitCallback", new HealthManager.DamageInfo(
						hit.point, ray.direction, weapons[weapon].bulletDamage, hit.collider), SendMessageOptions.DontRequireReceiver);
			}
		}
		else
		{
			Vector3 destination = (ray.direction * 500f) + ray.origin;
			DrawShoot(destination, Vector3.up, null, false, false);
		}

		// WWISE: Dynamic weapon shot events
		string shotEvent =
			(weapons[weapon].type == InteractiveWeapon.WeaponType.LONG)
			? "play_ar_shot"
			: "Play_pistolShot";

		AkUnitySoundEngine.PostEvent(shotEvent, gameObject);

		GameObject.FindGameObjectWithTag("GameController").SendMessage("RootAlertNearby", ray.origin, SendMessageOptions.DontRequireReceiver);
		shotDecay = originalShotDecay;
		isShotAlive = true;
	}

	private void DrawShoot(Vector3 destination, Vector3 targetNormal, Transform parent,
		bool placeSparks = true, bool placeBulletHole = true)
	{
		Vector3 origin = gunMuzzle.position - gunMuzzle.right * 0.5f;

		muzzleFlash.SetActive(true);
		muzzleFlash.transform.SetParent(gunMuzzle);
		muzzleFlash.transform.localPosition = Vector3.zero;
		muzzleFlash.transform.localEulerAngles = Vector3.back * 90f;

		GameObject instantShot = Instantiate(shot);
		instantShot.SetActive(true);
		instantShot.transform.position = origin;
		instantShot.transform.rotation = Quaternion.LookRotation(destination - origin);
		instantShot.transform.parent = shot.transform.parent;

		if (placeSparks)
		{
			GameObject instantSparks = Instantiate(sparks);
			instantSparks.SetActive(true);
			instantSparks.transform.position = destination;
			instantSparks.transform.parent = sparks.transform.parent;
		}

		if (placeBulletHole)
		{
			Quaternion hitRotation = Quaternion.FromToRotation(Vector3.back, targetNormal);
			GameObject bullet;
			if (bulletHoles.Count < maxBulletHoles)
			{
				bullet = GameObject.CreatePrimitive(PrimitiveType.Quad);
				bullet.GetComponent<MeshRenderer>().material = bulletHole;
				bullet.GetComponent<Collider>().enabled = false;
				bullet.transform.localScale = Vector3.one * 0.07f;
				bullet.name = "BulletHole";
				bulletHoles.Add(bullet);
			}
			else
			{
				bullet = bulletHoles[bulletHoleSlot];
				bulletHoleSlot++;
				bulletHoleSlot %= maxBulletHoles;
			}
			bullet.transform.position = destination + 0.01f * targetNormal;
			bullet.transform.rotation = hitRotation;
			bullet.transform.SetParent(parent);
		}
	}

	private void ChangeWeapon(int oldWeapon, int newWeapon)
	{
		if (oldWeapon > 0)
		{
			weapons[oldWeapon].gameObject.SetActive(false);
			gunMuzzle = null;
			weapons[oldWeapon].Toggle(false);
		}
		while (weapons[newWeapon] == null && newWeapon > 0)
		{
			newWeapon = (newWeapon + 1) % weapons.Count;
		}
		if (newWeapon > 0)
		{
			weapons[newWeapon].gameObject.SetActive(true);
			gunMuzzle = weapons[newWeapon].transform.Find("muzzle");
			weapons[newWeapon].Toggle(true);
		}

		activeWeapon = newWeapon;

		if (oldWeapon != newWeapon)
		{
			behaviourManager.GetAnim.SetTrigger(changeWeaponTrigger);
			behaviourManager.GetAnim.SetInteger(weaponTypeInt, weapons[newWeapon] ? (int)weapons[newWeapon].type : 0);
		}

		SetWeaponCrosshair(newWeapon > 0);
	}

	private void ShotDecay()
	{
		if (shotDecay > 0.2)
		{
			shotDecay -= shotRateFactor * Time.deltaTime;
			if (shotDecay <= 0.4f)
			{
				SetWeaponCrosshair(activeWeapon > 0);
				muzzleFlash.SetActive(false);

				if (activeWeapon > 0)
				{
					behaviourManager.GetCamScript.BounceVertical(-weapons[activeWeapon].recoilAngle * 0.1f);

					if (shotDecay <= (0.4f - 2 * Time.deltaTime))
					{
						// Updated to Input.GetKey(shootKey)
						if (weapons[activeWeapon].mode == InteractiveWeapon.WeaponMode.AUTO && Input.GetKey(shootKey))
						{
							ShootWeapon(activeWeapon, false);
						}
						else if (weapons[activeWeapon].mode == InteractiveWeapon.WeaponMode.BURST && burstShotCount < weapons[activeWeapon].burstSize)
						{
							ShootWeapon(activeWeapon, false);
						}
						else if (weapons[activeWeapon].mode != InteractiveWeapon.WeaponMode.BURST)
						{
							burstShotCount = 0;
						}
					}
				}
			}
		}
		else
		{
			isShotAlive = false;
			behaviourManager.GetCamScript.BounceVertical(0);
			burstShotCount = 0;
		}
	}

	public void AddWeapon(InteractiveWeapon newWeapon)
	{
		newWeapon.gameObject.transform.SetParent(rightHand);
		newWeapon.transform.localPosition = newWeapon.rightHandPosition;
		newWeapon.transform.localRotation = Quaternion.Euler(newWeapon.relativeRotation);

		if (this.weapons[slotMap[newWeapon.type]])
		{
			if (this.weapons[slotMap[newWeapon.type]].label == newWeapon.label)
			{
				this.weapons[slotMap[newWeapon.type]].ResetBullets();
				ChangeWeapon(activeWeapon, slotMap[newWeapon.type]);
				GameObject.Destroy(newWeapon.gameObject);
				return;
			}
			else
			{
				this.weapons[slotMap[newWeapon.type]].Drop();
			}
		}

		this.weapons[slotMap[newWeapon.type]] = newWeapon;
		ChangeWeapon(activeWeapon, slotMap[newWeapon.type]);
	}

	public void EndReloadWeapon()
	{
		behaviourManager.GetAnim.SetBool(reloadBool, false);
		weapons[activeWeapon].EndReload();
	}

	private void SetWeaponCrosshair(bool armed)
	{
		aimBehaviour.crosshair = armed ? aimCrosshair : originalCrosshair;
	}

	private bool CheckForBlockedAim()
	{
		isAimBlocked = Physics.SphereCast(this.transform.position + castRelativeOrigin, 0.1f, behaviourManager.GetCamScript.transform.forward, out RaycastHit hit, distToHand - 0.1f);
		isAimBlocked = isAimBlocked && hit.collider.transform != this.transform;
		behaviourManager.GetAnim.SetBool(blockedAimBool, isAimBlocked);
		Debug.DrawRay(this.transform.position + castRelativeOrigin, behaviourManager.GetCamScript.transform.forward * distToHand, isAimBlocked ? Color.red : Color.cyan);

		return isAimBlocked;
	}

	public void OnAnimatorIK(int layerIndex)
	{
		if (isAiming && activeWeapon > 0)
		{
			if (CheckForBlockedAim())
				return;

			Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
			targetRot *= Quaternion.Euler(initialRootRotation);
			targetRot *= Quaternion.Euler(initialHipsRotation);
			targetRot *= Quaternion.Euler(initialSpineRotation);
			behaviourManager.GetAnim.SetBoneLocalRotation(HumanBodyBones.Spine, Quaternion.Inverse(hips.rotation) * targetRot);

			float xCamRot = Quaternion.LookRotation(behaviourManager.playerCamera.forward).eulerAngles.x;
			targetRot = Quaternion.AngleAxis(xCamRot + armsRotation, this.transform.right);
			if (weapons[activeWeapon] && weapons[activeWeapon].type == InteractiveWeapon.WeaponType.LONG)
			{
				targetRot *= Quaternion.AngleAxis(9f, this.transform.right);
				targetRot *= Quaternion.AngleAxis(20f, this.transform.up);
			}
			targetRot *= spine.rotation;
			targetRot *= Quaternion.Euler(initialChestRotation);
			behaviourManager.GetAnim.SetBoneLocalRotation(HumanBodyBones.Chest, Quaternion.Inverse(spine.rotation) * targetRot);
		}
	}

	private void LateUpdate()
	{
		if (!isAiming || isAimBlocked)
		{
			if (behaviourManager.GetAnim.GetBool(coveringBool)
			&& behaviourManager.GetAnim.GetFloat(Animator.StringToHash("Crouch")) > 0.5f)
			{
				rightHand.Rotate(RightHandCover);
			}
			else if (weapons[activeWeapon] && weapons[activeWeapon].type == InteractiveWeapon.WeaponType.LONG)
			{
				leftArm.localEulerAngles += LeftArmLongGuard;
			}
		}
		else if (isAiming && weapons[activeWeapon] && weapons[activeWeapon].type == InteractiveWeapon.WeaponType.SHORT)
		{
			leftArm.localEulerAngles += LeftArmShortAim;
		}
	}
}