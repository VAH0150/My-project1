using UnityEngine;
using UnityEngine.UI;

public class InteractiveWeapon : MonoBehaviour
{
    public string label;
    public Sprite sprite;
    public Vector3 rightHandPosition;
    public Vector3 relativeRotation;
    public float bulletDamage = 10f;
    public float recoilAngle;
    public enum WeaponType { NONE, SHORT, LONG }
    public enum WeaponMode { SEMI, BURST, AUTO }

    public WeaponType type = WeaponType.NONE;
    public WeaponMode mode = WeaponMode.SEMI;
    public int burstSize = 0;

    [SerializeField]
    private int mag, totalBullets;
    private int fullMag, maxBullets;
    private GameObject player, gameController;
    private ShootBehaviour playerInventory;
    private SphereCollider interactiveRadius;
    private BoxCollider col;
    private Rigidbody rbody;
    private WeaponUIManager weaponHud;
    private bool pickable;
    private Transform pickupHUD;

    void Awake()
    {
        this.gameObject.name = this.label;
        this.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform t in this.transform)
        {
            t.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }
        player = GameObject.FindGameObjectWithTag("Player");
        playerInventory = player.GetComponent<ShootBehaviour>();
        gameController = GameObject.FindGameObjectWithTag("GameController");

        if (GameObject.Find("ScreenHUD") == null)
        {
            Debug.LogError("No ScreenHUD canvas found. Create ScreenHUD inside the GameController");
        }
        weaponHud = GameObject.Find("ScreenHUD").GetComponent<WeaponUIManager>();
        pickupHUD = gameController.transform.Find("PickupHUD");

        col = this.transform.GetChild(0).gameObject.AddComponent<BoxCollider>();
        CreateInteractiveRadius(col.center);
        this.rbody = this.gameObject.AddComponent<Rigidbody>();

        if (this.type == WeaponType.NONE)
        {
            Debug.LogWarning("Set correct weapon slot ( 1 - small/ 2- big)");
            type = WeaponType.SHORT;
        }

        if (!this.transform.Find("muzzle"))
        {
            Debug.LogError(this.name + " muzzle is not present. Create a game object named 'muzzle' as a child of this game object");
        }

        fullMag = mag;
        maxBullets = totalBullets;
        pickupHUD.gameObject.SetActive(false);
    }

    private void CreateInteractiveRadius(Vector3 center)
    {
        interactiveRadius = this.gameObject.AddComponent<SphereCollider>();
        interactiveRadius.center = center;
        interactiveRadius.radius = 1f;
        interactiveRadius.isTrigger = true;
    }

    void Update()
    {
        if (this.pickable && Input.GetButtonDown(playerInventory.pickButton))
        {
            rbody.isKinematic = true;
            this.col.enabled = false;

            playerInventory.AddWeapon(this);
            Destroy(interactiveRadius);
            this.Toggle(true);
            this.pickable = false;

            TogglePickupHUD(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.gameObject != player && Vector3.Distance(transform.position, player.transform.position) <= 5f)
        {
            AkUnitySoundEngine.PostEvent("Play_gundrop", gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == player)
        {
            pickable = false;
            TogglePickupHUD(false);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.gameObject == player && playerInventory && playerInventory.isActiveAndEnabled)
        {
            pickable = true;
            TogglePickupHUD(true);
        }
    }

    private void TogglePickupHUD(bool toggle)
    {
        pickupHUD.gameObject.SetActive(toggle);
        if (toggle)
        {
            pickupHUD.position = this.transform.position + Vector3.up * 0.5f;
            Vector3 direction = player.GetComponent<BasicBehaviour>().playerCamera.forward;
            direction.y = 0f;
            pickupHUD.rotation = Quaternion.LookRotation(direction);
            pickupHUD.Find("Label").GetComponent<Text>().text = "Pick " + this.gameObject.name;
        }
    }

    public void Toggle(bool active)
    {
        if (active)
        {
            AkUnitySoundEngine.PostEvent("Play_gundrop", gameObject);
        }
        weaponHud.Toggle(active);
        UpdateHud();
    }

    public void Drop()
    {
        this.gameObject.SetActive(true);
        this.transform.position += Vector3.up;
        rbody.isKinematic = false;
        this.transform.parent = null;
        CreateInteractiveRadius(col.center);
        this.col.enabled = true;
        weaponHud.Toggle(false);
    }

    public bool StartReload()
    {
        if (mag == fullMag || totalBullets == 0)
            return false;
        else if (totalBullets < fullMag - mag)
        {
            mag += totalBullets;
            totalBullets = 0;
        }
        else
        {
            totalBullets -= fullMag - mag;
            mag = fullMag;
        }
        return true;
    }

    public void EndReload()
    {
        UpdateHud();
    }

    public bool Shoot(bool firstShot = true)
    {
        if (mag > 0)
        {
            mag--;
            UpdateHud();
            return true;
        }
        return false;
    }

    public void ResetBullets()
    {
        mag = fullMag;
        totalBullets = maxBullets;
    }

    private void UpdateHud()
    {
        weaponHud.UpdateWeaponHUD(sprite, mag, fullMag, totalBullets);
    }
}